using FleetDelivery.BuildingBlocks.Domain;

namespace FleetDelivery.Modules.Shipments.Domain;

/// <summary>
/// Aggregate root for a shipment's full lifecycle. All state changes go
/// through the explicit transition methods below, each validated against
/// <see cref="AllowedTransitions"/> (an explicit table, not scattered
/// if/else) — an invalid transition throws <see cref="InvalidShipmentTransitionException"/>,
/// mapped to HTTP 409 at the API layer. Every transition appends exactly one
/// <see cref="TrackingEvent"/> and raises the matching domain event; the
/// audit trail is a side effect of the state machine, not a bolt-on.
///
/// Concurrency: <see cref="Version"/> is a hand-rolled counter incremented by
/// every transition, mapped as an EF Core concurrency token (see
/// <c>ShipmentConfiguration.IsConcurrencyToken()</c>) rather than Postgres's
/// <c>xmin</c> system column. A hand-rolled counter keeps the concurrency
/// token a plain domain-owned <c>int</c> that Application-layer handlers can
/// compare against a client-supplied <c>ExpectedVersion</c> *before* even
/// attempting a transition (a fast, clear "stale write" error path), while
/// EF's own optimistic-concurrency check (a real <c>WHERE version = @original</c>
/// on <c>UPDATE</c>, surfaced as <see cref="Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException"/>
/// on a lost race) still backstops true concurrent writes. Wiring
/// <c>xmin</c> through the repository abstraction without leaking EF Core
/// into Application/Domain would have been materially more plumbing for the
/// same guarantee.
///
/// Rescheduling has two explicit hops rather than one: <see cref="Reschedule"/>
/// (<c>DeliveryFailed -> Rescheduled</c>) and <see cref="BackToReadyForDispatch"/>
/// (<c>Rescheduled -> ReadyForDispatch</c>), kept as separate methods instead
/// of folded together so each hop stays independently callable/testable and
/// mirrors exactly the two edges the transition table needs anyway (a
/// dispatcher may want to leave a shipment in <c>Rescheduled</c> for a while
/// before actually requeuing it).
/// </summary>
public sealed class Shipment : AggregateRoot<Guid>
{
    private static readonly IReadOnlyDictionary<ShipmentStatus, ShipmentStatus[]> AllowedTransitions =
        new Dictionary<ShipmentStatus, ShipmentStatus[]>
        {
            [ShipmentStatus.Draft] = [ShipmentStatus.ReadyForDispatch, ShipmentStatus.Cancelled],
            [ShipmentStatus.ReadyForDispatch] = [ShipmentStatus.Assigned, ShipmentStatus.Cancelled],
            [ShipmentStatus.Assigned] = [ShipmentStatus.PickedUp, ShipmentStatus.Cancelled],
            [ShipmentStatus.PickedUp] = [ShipmentStatus.InTransit],
            [ShipmentStatus.InTransit] = [ShipmentStatus.OutForDelivery],
            [ShipmentStatus.OutForDelivery] = [ShipmentStatus.Delivered, ShipmentStatus.DeliveryFailed],
            [ShipmentStatus.DeliveryFailed] = [ShipmentStatus.Rescheduled, ShipmentStatus.Returned],
            [ShipmentStatus.Rescheduled] = [ShipmentStatus.ReadyForDispatch, ShipmentStatus.Returned],
            [ShipmentStatus.Delivered] = [],
            [ShipmentStatus.Returned] = [],
            [ShipmentStatus.Cancelled] = [],
        };

    private const string TrackingNumberAlphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";

    private readonly List<TrackingEvent> _trackingEvents = [];
    private readonly List<DeliveryAttempt> _deliveryAttempts = [];

    // Reserved for EF Core materialization.
    private Shipment()
    {
    }

    private Shipment(
        Guid id,
        string trackingNumber,
        string recipientName,
        string recipientPhone,
        Address origin,
        Address destination,
        Guid createdByUserId,
        DateTimeOffset createdAt)
        : base(id)
    {
        TrackingNumber = trackingNumber;
        Status = ShipmentStatus.Draft;
        RecipientName = recipientName;
        RecipientPhone = recipientPhone;
        OriginAddress = origin;
        DestinationAddress = destination;
        CreatedByUserId = createdByUserId;
        CreatedAt = createdAt;
        Version = 0;
    }

    /// <summary>Server-generated, e.g. "FD-7K2QX9A4". Unique constraint enforced in the DB (see <c>ShipmentConfiguration</c>).</summary>
    public string TrackingNumber { get; private set; } = string.Empty;

    public ShipmentStatus Status { get; private set; }

    public string RecipientName { get; private set; } = string.Empty;

    public string RecipientPhone { get; private set; } = string.Empty;

    public Address OriginAddress { get; private set; } = null!;

    public Address DestinationAddress { get; private set; } = null!;

    public Guid? AssignedDriverId { get; private set; }

    public Guid? AssignedVehicleId { get; private set; }

    public Guid CreatedByUserId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>Cheap flag — the photo itself lives in <see cref="ProofOfDeliveryPhoto"/>, a separate on-demand entity, never loaded with the shipment. See that type's doc comment for why.</summary>
    public bool HasProofOfDelivery { get; private set; }

    /// <summary>Optimistic-concurrency token. See the class-level doc comment for why this is hand-rolled rather than Postgres's <c>xmin</c>.</summary>
    public int Version { get; private set; }

    public IReadOnlyCollection<TrackingEvent> TrackingEvents => _trackingEvents.AsReadOnly();

    public IReadOnlyCollection<DeliveryAttempt> DeliveryAttempts => _deliveryAttempts.AsReadOnly();

    /// <summary>Creates a new shipment in <see cref="ShipmentStatus.Draft"/>. Raises <see cref="ShipmentCreated"/> and appends a "Created" tracking event.</summary>
    public static Shipment Create(string recipientName, string recipientPhone, Address origin, Address destination, Guid createdByUserId)
    {
        if (string.IsNullOrWhiteSpace(recipientName))
        {
            throw new ArgumentException("Recipient name cannot be empty.", nameof(recipientName));
        }

        if (string.IsNullOrWhiteSpace(recipientPhone))
        {
            throw new ArgumentException("Recipient phone cannot be empty.", nameof(recipientPhone));
        }

        ArgumentNullException.ThrowIfNull(origin);
        ArgumentNullException.ThrowIfNull(destination);

        var now = DateTimeOffset.UtcNow;

        var shipment = new Shipment(
            Guid.NewGuid(),
            GenerateTrackingNumber(),
            recipientName.Trim(),
            recipientPhone.Trim(),
            origin,
            destination,
            createdByUserId,
            now);

        shipment.AppendTrackingEvent("Created", createdByUserId, notes: null);
        shipment.AddDomainEvent(new ShipmentCreated(shipment.Id, shipment.TrackingNumber, createdByUserId, now));

        return shipment;
    }

    /// <summary><c>Draft -> ReadyForDispatch</c>.</summary>
    public void MarkReadyForDispatch(Guid actorUserId)
    {
        TransitionTo(ShipmentStatus.ReadyForDispatch, "ReadyForDispatch", actorUserId);
        AddDomainEvent(new ShipmentReadyForDispatch(Id, DateTimeOffset.UtcNow));
    }

    /// <summary><c>ReadyForDispatch -> Assigned</c>. Sets <see cref="AssignedDriverId"/> and <see cref="AssignedVehicleId"/>.</summary>
    public void Assign(Guid driverId, Guid vehicleId, Guid assignedByUserId)
    {
        TransitionTo(ShipmentStatus.Assigned, "Assigned", assignedByUserId, $"Assigned to driver {driverId} with vehicle {vehicleId}.");
        AssignedDriverId = driverId;
        AssignedVehicleId = vehicleId;
        AddDomainEvent(new DriverAssigned(Id, driverId, vehicleId, assignedByUserId, DateTimeOffset.UtcNow));
    }

    /// <summary><c>Assigned -> PickedUp</c>. <paramref name="driverId"/> must be the currently assigned driver.</summary>
    public void MarkPickedUp(Guid driverId)
    {
        // Transition-validity is checked (pure, no mutation) before the
        // ownership check: from e.g. Draft, "wrong state" is the more useful
        // error than "wrong driver" for a shipment that isn't assigned to
        // anyone yet.
        EnsureTransitionAllowed(ShipmentStatus.PickedUp);
        EnsureAssignedDriver(driverId);
        ApplyTransition(ShipmentStatus.PickedUp, "PickedUp", driverId);
        AddDomainEvent(new ShipmentPickedUp(Id, driverId, DateTimeOffset.UtcNow));
    }

    /// <summary><c>PickedUp -> InTransit</c>. <paramref name="driverId"/> must be the currently assigned driver.</summary>
    public void MarkInTransit(Guid driverId)
    {
        EnsureTransitionAllowed(ShipmentStatus.InTransit);
        EnsureAssignedDriver(driverId);
        ApplyTransition(ShipmentStatus.InTransit, "InTransit", driverId);
        AddDomainEvent(new ShipmentInTransit(Id, driverId, DateTimeOffset.UtcNow));
    }

    /// <summary><c>InTransit -> OutForDelivery</c>. <paramref name="driverId"/> must be the currently assigned driver.</summary>
    public void MarkOutForDelivery(Guid driverId)
    {
        EnsureTransitionAllowed(ShipmentStatus.OutForDelivery);
        EnsureAssignedDriver(driverId);
        ApplyTransition(ShipmentStatus.OutForDelivery, "OutForDelivery", driverId);
        AddDomainEvent(new ShipmentOutForDelivery(Id, driverId, DateTimeOffset.UtcNow));
    }

    /// <summary><c>OutForDelivery -> Delivered</c> (terminal). <paramref name="driverId"/> must be the currently assigned driver.</summary>
    public void MarkDelivered(Guid driverId, string recipientName, string? notes)
    {
        EnsureTransitionAllowed(ShipmentStatus.Delivered);
        EnsureAssignedDriver(driverId);

        if (string.IsNullOrWhiteSpace(recipientName))
        {
            throw new ArgumentException("Recipient name cannot be empty.", nameof(recipientName));
        }

        ApplyTransition(ShipmentStatus.Delivered, "Delivered", driverId, notes);
        _deliveryAttempts.Add(DeliveryAttempt.Successful(Id, driverId, notes));
        AddDomainEvent(new DeliveryCompleted(Id, driverId, recipientName.Trim(), DateTimeOffset.UtcNow));
    }

    /// <summary>
    /// Records that a Proof of Delivery photo now exists for this shipment —
    /// the actual bytes are persisted separately (see <see cref="ProofOfDeliveryPhoto"/>);
    /// this just flips the cheap flag other reads can check without paying
    /// for the blob. Only the assigned driver, and only after the shipment
    /// is actually <see cref="ShipmentStatus.Delivered"/> — a photo can't
    /// retroactively prove a delivery that hasn't happened, and can't be
    /// attached by anyone else.
    /// </summary>
    public void AttachProofOfDelivery(Guid driverId)
    {
        if (Status != ShipmentStatus.Delivered)
        {
            throw new ShipmentNotDeliveredException(Id, Status);
        }

        EnsureAssignedDriver(driverId);

        if (HasProofOfDelivery)
        {
            throw new ProofOfDeliveryAlreadyAttachedException(Id);
        }

        HasProofOfDelivery = true;
    }

    /// <summary><c>OutForDelivery -> DeliveryFailed</c>. Also records a failed <see cref="DeliveryAttempt"/>. <paramref name="driverId"/> must be the currently assigned driver.</summary>
    public void MarkFailed(Guid driverId, string reason)
    {
        EnsureTransitionAllowed(ShipmentStatus.DeliveryFailed);
        EnsureAssignedDriver(driverId);

        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException("Reason cannot be empty.", nameof(reason));
        }

        ApplyTransition(ShipmentStatus.DeliveryFailed, "DeliveryFailed", driverId, reason);
        _deliveryAttempts.Add(DeliveryAttempt.Failed(Id, driverId, reason));
        AddDomainEvent(new DeliveryFailed(Id, driverId, reason.Trim(), DateTimeOffset.UtcNow));
    }

    /// <summary><c>DeliveryFailed -> Rescheduled</c>. Clears <see cref="AssignedDriverId"/> and <see cref="AssignedVehicleId"/> — goes back to needing dispatch.</summary>
    public void Reschedule(Guid rescheduledByUserId)
    {
        TransitionTo(ShipmentStatus.Rescheduled, "Rescheduled", rescheduledByUserId);
        AssignedDriverId = null;
        AssignedVehicleId = null;
        AddDomainEvent(new DeliveryRescheduled(Id, rescheduledByUserId, DateTimeOffset.UtcNow));
    }

    /// <summary><c>Rescheduled -> ReadyForDispatch</c>. See the class-level doc comment for why this is a separate method from <see cref="Reschedule"/>.</summary>
    public void BackToReadyForDispatch(Guid actorUserId)
    {
        TransitionTo(ShipmentStatus.ReadyForDispatch, "ReadyForDispatch", actorUserId);
        AddDomainEvent(new ShipmentReadyForDispatch(Id, DateTimeOffset.UtcNow));
    }

    /// <summary><c>DeliveryFailed</c> or <c>Rescheduled</c> -> <c>Returned</c> (terminal).</summary>
    public void ReturnToOrigin(Guid returnedByUserId, string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException("Reason cannot be empty.", nameof(reason));
        }

        TransitionTo(ShipmentStatus.Returned, "Returned", returnedByUserId, reason);
        AddDomainEvent(new ShipmentReturned(Id, returnedByUserId, reason.Trim(), DateTimeOffset.UtcNow));
    }

    /// <summary><c>Draft</c>, <c>ReadyForDispatch</c>, or <c>Assigned</c> -> <c>Cancelled</c> (terminal). Never allowed once <c>PickedUp</c> — a shipment already in motion can't be cancelled.</summary>
    public void Cancel(Guid cancelledByUserId, string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException("Reason cannot be empty.", nameof(reason));
        }

        TransitionTo(ShipmentStatus.Cancelled, "Cancelled", cancelledByUserId, reason);
        AddDomainEvent(new ShipmentCancelled(Id, cancelledByUserId, reason.Trim(), DateTimeOffset.UtcNow));
    }

    /// <summary>Validates then applies — used by transitions that have no separate ownership check to interleave.</summary>
    private void TransitionTo(ShipmentStatus newStatus, string trackingEventType, Guid? actorUserId, string? notes = null)
    {
        EnsureTransitionAllowed(newStatus);
        ApplyTransition(newStatus, trackingEventType, actorUserId, notes);
    }

    /// <summary>Pure mutation, no validation — callers that also need <see cref="EnsureAssignedDriver"/> call <see cref="EnsureTransitionAllowed"/> and that check themselves first, in that order, before calling this.</summary>
    private void ApplyTransition(ShipmentStatus newStatus, string trackingEventType, Guid? actorUserId, string? notes = null)
    {
        Status = newStatus;
        Version++;
        AppendTrackingEvent(trackingEventType, actorUserId, notes);
    }

    private void EnsureTransitionAllowed(ShipmentStatus newStatus)
    {
        if (!AllowedTransitions.TryGetValue(Status, out var allowed) || !allowed.Contains(newStatus))
        {
            throw new InvalidShipmentTransitionException(Status, newStatus);
        }
    }

    /// <summary>"A driver can only transition a shipment currently assigned to them" — enforced here as a domain invariant, not only at the API layer.</summary>
    private void EnsureAssignedDriver(Guid driverId)
    {
        if (AssignedDriverId != driverId)
        {
            throw new ShipmentDriverMismatchException(Id, AssignedDriverId ?? Guid.Empty, driverId);
        }
    }

    private void AppendTrackingEvent(string type, Guid? actorUserId, string? notes) =>
        _trackingEvents.Add(TrackingEvent.Create(Id, type, actorUserId, notes));

    /// <summary>
    /// "FD-" + 8 random uppercase alphanumeric characters (36^8 ≈ 2.8e12
    /// combinations). No collision-retry: at this project's scale the
    /// birthday-paradox odds of a collision against the unique DB index are
    /// negligible, and a genuine collision simply surfaces as an insert
    /// failure the caller can retry the whole command for — deliberately not
    /// engineered further here.
    /// </summary>
    private static string GenerateTrackingNumber()
    {
        Span<char> buffer = stackalloc char[8];

        for (var i = 0; i < buffer.Length; i++)
        {
            buffer[i] = TrackingNumberAlphabet[Random.Shared.Next(TrackingNumberAlphabet.Length)];
        }

        return "FD-" + new string(buffer);
    }
}

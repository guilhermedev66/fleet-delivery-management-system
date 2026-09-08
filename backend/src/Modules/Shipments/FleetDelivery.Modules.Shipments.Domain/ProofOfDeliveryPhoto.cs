using FleetDelivery.BuildingBlocks.Domain;

namespace FleetDelivery.Modules.Shipments.Domain;

/// <summary>
/// The actual Proof of Delivery photo bytes — keyed directly by
/// <see cref="ShipmentId"/> (one per shipment, at most), mapped as its own
/// top-level table via its own <c>DbSet</c>, deliberately NOT an EF owned
/// entity of <see cref="Shipment"/>. See the doc comment on
/// <see cref="DeliveryAttempt"/> for why: owned collections load eagerly
/// with their owner, and nothing that lists or reads a shipment's core
/// fields should pay the cost of hauling a multi-megabyte blob along for
/// the ride. <see cref="Shipment.HasProofOfDelivery"/> is the cheap flag
/// callers check instead; this entity is fetched only by its own
/// dedicated, on-demand repository call.
/// </summary>
public sealed class ProofOfDeliveryPhoto : Entity<Guid>
{
    /// <summary>Enforced ≤ 5 MB by <c>AttachProofOfDeliveryCommand</c> before this is ever constructed — not re-validated here, Application already did the real work.</summary>
    public const int MaxContentBytes = 5 * 1024 * 1024;

    // Reserved for EF Core materialization.
    private ProofOfDeliveryPhoto()
    {
    }

    private ProofOfDeliveryPhoto(Guid shipmentId, byte[] content, string contentType, DateTimeOffset uploadedAt)
        : base(shipmentId)
    {
        ShipmentId = shipmentId;
        Content = content;
        ContentType = contentType;
        SizeBytes = content.LongLength;
        UploadedAt = uploadedAt;
    }

    /// <summary>Same value as <see cref="Entity{TId}.Id"/> — a shipment has at most one photo, so the shipment's own id is the natural key. Kept as an explicit named property for readability at call sites.</summary>
    public Guid ShipmentId { get; private set; }

    public byte[] Content { get; private set; } = [];

    public string ContentType { get; private set; } = string.Empty;

    public long SizeBytes { get; private set; }

    public DateTimeOffset UploadedAt { get; private set; }

    public static ProofOfDeliveryPhoto Create(Guid shipmentId, byte[] content, string contentType) =>
        new(shipmentId, content, contentType, DateTimeOffset.UtcNow);
}

using FleetDelivery.Modules.Shipments.Domain;
using FluentAssertions;

namespace FleetDelivery.UnitTests.Shipments;

// Exercises Shipment's own state-transition methods directly — no DB involved.
public class ShipmentTests
{
    private static Address SampleAddress(string city = "Springfield") =>
        Address.Create("123 Main St", city, "IL", "62701", "USA");

    private static Shipment CreateDraftShipment(Guid? createdByUserId = null) =>
        Shipment.Create("Jane Recipient", "+1-555-0100", SampleAddress("Origin City"), SampleAddress("Destination City"), createdByUserId ?? Guid.NewGuid());

    [Fact]
    public void Create_returns_a_shipment_in_Draft_with_a_Created_tracking_event_and_domain_event()
    {
        var createdBy = Guid.NewGuid();

        var shipment = CreateDraftShipment(createdBy);

        shipment.Status.Should().Be(ShipmentStatus.Draft);
        shipment.TrackingNumber.Should().StartWith("FD-");
        shipment.TrackingNumber.Length.Should().Be(11); // "FD-" + 8 chars
        shipment.Version.Should().Be(0);
        shipment.TrackingEvents.Should().ContainSingle(e => e.Type == "Created" && e.ActorUserId == createdBy);
        shipment.DomainEvents.Should().ContainSingle(e => e is ShipmentCreated);
    }

    [Fact]
    public void MarkReadyForDispatch_from_Draft_transitions_and_appends_event()
    {
        var dispatcher = Guid.NewGuid();
        var shipment = CreateDraftShipment();

        shipment.MarkReadyForDispatch(dispatcher);

        shipment.Status.Should().Be(ShipmentStatus.ReadyForDispatch);
        shipment.Version.Should().Be(1);
        shipment.TrackingEvents.Should().Contain(e => e.Type == "ReadyForDispatch" && e.ActorUserId == dispatcher);
        shipment.DomainEvents.Should().Contain(e => e is ShipmentReadyForDispatch);
    }

    [Fact]
    public void Full_happy_path_lifecycle_reaches_Delivered_with_one_tracking_event_per_transition()
    {
        var dispatcher = Guid.NewGuid();
        var driver = Guid.NewGuid();
        var shipment = CreateDraftShipment();

        shipment.MarkReadyForDispatch(Guid.NewGuid());
        shipment.Assign(driver, dispatcher);
        shipment.MarkPickedUp(driver);
        shipment.MarkInTransit(driver);
        shipment.MarkOutForDelivery(driver);
        shipment.MarkDelivered(driver, "Jane Recipient", "Left at front door");

        shipment.Status.Should().Be(ShipmentStatus.Delivered);
        shipment.AssignedDriverId.Should().Be(driver);
        shipment.Version.Should().Be(6); // Created doesn't bump Version, the 6 transitions above do.
        shipment.TrackingEvents.Select(e => e.Type).Should().Equal(
            "Created", "ReadyForDispatch", "Assigned", "PickedUp", "InTransit", "OutForDelivery", "Delivered");
        shipment.DomainEvents.Should().Contain(e => e is DeliveryCompleted);
    }

    [Fact]
    public void MarkPickedUp_before_Assigned_throws_InvalidShipmentTransitionException()
    {
        var shipment = CreateDraftShipment();
        shipment.MarkReadyForDispatch(Guid.NewGuid());

        var act = () => shipment.MarkPickedUp(Guid.NewGuid());

        act.Should().Throw<InvalidShipmentTransitionException>();
    }

    [Fact]
    public void MarkDelivered_from_Draft_throws_InvalidShipmentTransitionException()
    {
        var shipment = CreateDraftShipment();

        var act = () => shipment.MarkDelivered(Guid.NewGuid(), "Someone", null);

        act.Should().Throw<InvalidShipmentTransitionException>();
    }

    [Fact]
    public void Delivered_is_terminal_and_rejects_every_further_transition()
    {
        var driver = Guid.NewGuid();
        var shipment = CreateDraftShipment();
        shipment.MarkReadyForDispatch(Guid.NewGuid());
        shipment.Assign(driver, Guid.NewGuid());
        shipment.MarkPickedUp(driver);
        shipment.MarkInTransit(driver);
        shipment.MarkOutForDelivery(driver);
        shipment.MarkDelivered(driver, "Jane Recipient", null);

        FluentActions.Invoking(() => shipment.MarkFailed(driver, "too late")).Should().Throw<InvalidShipmentTransitionException>();
        FluentActions.Invoking(() => shipment.Cancel(Guid.NewGuid(), "changed my mind")).Should().Throw<InvalidShipmentTransitionException>();
        FluentActions.Invoking(() => shipment.Reschedule(Guid.NewGuid())).Should().Throw<InvalidShipmentTransitionException>();
        FluentActions.Invoking(() => shipment.MarkReadyForDispatch(Guid.NewGuid())).Should().Throw<InvalidShipmentTransitionException>();
    }

    [Theory]
    [InlineData(ShipmentStatus.Draft)]
    [InlineData(ShipmentStatus.ReadyForDispatch)]
    [InlineData(ShipmentStatus.Assigned)]
    public void Cancel_is_allowed_from_Draft_ReadyForDispatch_or_Assigned(ShipmentStatus fromStatus)
    {
        var driver = Guid.NewGuid();
        var shipment = CreateDraftShipment();

        if (fromStatus is ShipmentStatus.ReadyForDispatch or ShipmentStatus.Assigned)
        {
            shipment.MarkReadyForDispatch(Guid.NewGuid());
        }

        if (fromStatus is ShipmentStatus.Assigned)
        {
            shipment.Assign(driver, Guid.NewGuid());
        }

        shipment.Cancel(Guid.NewGuid(), "customer request");

        shipment.Status.Should().Be(ShipmentStatus.Cancelled);
        shipment.DomainEvents.Should().Contain(e => e is ShipmentCancelled);
    }

    [Fact]
    public void Cancel_once_PickedUp_throws_InvalidShipmentTransitionException()
    {
        var driver = Guid.NewGuid();
        var shipment = CreateDraftShipment();
        shipment.MarkReadyForDispatch(Guid.NewGuid());
        shipment.Assign(driver, Guid.NewGuid());
        shipment.MarkPickedUp(driver);

        var act = () => shipment.Cancel(Guid.NewGuid(), "too late now");

        act.Should().Throw<InvalidShipmentTransitionException>();
    }

    [Fact]
    public void MarkPickedUp_with_a_driverId_that_is_not_the_assigned_driver_throws()
    {
        var assignedDriver = Guid.NewGuid();
        var someoneElse = Guid.NewGuid();
        var shipment = CreateDraftShipment();
        shipment.MarkReadyForDispatch(Guid.NewGuid());
        shipment.Assign(assignedDriver, Guid.NewGuid());

        var act = () => shipment.MarkPickedUp(someoneElse);

        act.Should().Throw<ShipmentDriverMismatchException>();
        shipment.Status.Should().Be(ShipmentStatus.Assigned, "a rejected ownership check must not mutate state");
    }

    [Fact]
    public void MarkInTransit_with_the_wrong_driver_throws_and_MarkOutForDelivery_with_the_wrong_driver_throws()
    {
        var assignedDriver = Guid.NewGuid();
        var someoneElse = Guid.NewGuid();
        var shipment = CreateDraftShipment();
        shipment.MarkReadyForDispatch(Guid.NewGuid());
        shipment.Assign(assignedDriver, Guid.NewGuid());
        shipment.MarkPickedUp(assignedDriver);

        FluentActions.Invoking(() => shipment.MarkInTransit(someoneElse)).Should().Throw<ShipmentDriverMismatchException>();

        shipment.MarkInTransit(assignedDriver);

        FluentActions.Invoking(() => shipment.MarkOutForDelivery(someoneElse)).Should().Throw<ShipmentDriverMismatchException>();
    }

    [Fact]
    public void MarkFailed_with_the_wrong_driver_throws_and_does_not_record_a_delivery_attempt()
    {
        var assignedDriver = Guid.NewGuid();
        var someoneElse = Guid.NewGuid();
        var shipment = CreateDraftShipment();
        shipment.MarkReadyForDispatch(Guid.NewGuid());
        shipment.Assign(assignedDriver, Guid.NewGuid());
        shipment.MarkPickedUp(assignedDriver);
        shipment.MarkInTransit(assignedDriver);
        shipment.MarkOutForDelivery(assignedDriver);

        var act = () => shipment.MarkFailed(someoneElse, "not home");

        act.Should().Throw<ShipmentDriverMismatchException>();
        shipment.DeliveryAttempts.Should().BeEmpty();
    }

    [Fact]
    public void MarkFailed_by_the_assigned_driver_transitions_to_DeliveryFailed_and_records_a_failed_delivery_attempt()
    {
        var driver = Guid.NewGuid();
        var shipment = CreateDraftShipment();
        shipment.MarkReadyForDispatch(Guid.NewGuid());
        shipment.Assign(driver, Guid.NewGuid());
        shipment.MarkPickedUp(driver);
        shipment.MarkInTransit(driver);
        shipment.MarkOutForDelivery(driver);

        shipment.MarkFailed(driver, "recipient not home");

        shipment.Status.Should().Be(ShipmentStatus.DeliveryFailed);
        shipment.DeliveryAttempts.Should().ContainSingle(a => a.DriverId == driver && a.Outcome == DeliveryAttemptOutcome.Failed);
        shipment.DomainEvents.Should().Contain(e => e is DeliveryFailed);
    }

    [Fact]
    public void Reschedule_clears_the_assigned_driver_and_BackToReadyForDispatch_requeues_it()
    {
        var driver = Guid.NewGuid();
        var shipment = CreateDraftShipment();
        shipment.MarkReadyForDispatch(Guid.NewGuid());
        shipment.Assign(driver, Guid.NewGuid());
        shipment.MarkPickedUp(driver);
        shipment.MarkInTransit(driver);
        shipment.MarkOutForDelivery(driver);
        shipment.MarkFailed(driver, "not home");

        shipment.Reschedule(Guid.NewGuid());

        shipment.Status.Should().Be(ShipmentStatus.Rescheduled);
        shipment.AssignedDriverId.Should().BeNull();

        shipment.BackToReadyForDispatch(Guid.NewGuid());

        shipment.Status.Should().Be(ShipmentStatus.ReadyForDispatch);
    }

    [Fact]
    public void ReturnToOrigin_is_allowed_from_DeliveryFailed_and_from_Rescheduled()
    {
        var driver = Guid.NewGuid();

        var shipmentFromFailed = CreateDraftShipment();
        shipmentFromFailed.MarkReadyForDispatch(Guid.NewGuid());
        shipmentFromFailed.Assign(driver, Guid.NewGuid());
        shipmentFromFailed.MarkPickedUp(driver);
        shipmentFromFailed.MarkInTransit(driver);
        shipmentFromFailed.MarkOutForDelivery(driver);
        shipmentFromFailed.MarkFailed(driver, "not home");
        shipmentFromFailed.ReturnToOrigin(Guid.NewGuid(), "customer unreachable");
        shipmentFromFailed.Status.Should().Be(ShipmentStatus.Returned);

        var shipmentFromRescheduled = CreateDraftShipment();
        shipmentFromRescheduled.MarkReadyForDispatch(Guid.NewGuid());
        shipmentFromRescheduled.Assign(driver, Guid.NewGuid());
        shipmentFromRescheduled.MarkPickedUp(driver);
        shipmentFromRescheduled.MarkInTransit(driver);
        shipmentFromRescheduled.MarkOutForDelivery(driver);
        shipmentFromRescheduled.MarkFailed(driver, "not home");
        shipmentFromRescheduled.Reschedule(Guid.NewGuid());
        shipmentFromRescheduled.ReturnToOrigin(Guid.NewGuid(), "customer cancelled order");
        shipmentFromRescheduled.Status.Should().Be(ShipmentStatus.Returned);
    }

    [Fact]
    public void Cancel_with_an_empty_reason_throws_ArgumentException()
    {
        var shipment = CreateDraftShipment();

        var act = () => shipment.Cancel(Guid.NewGuid(), " ");

        act.Should().Throw<ArgumentException>();
    }
}

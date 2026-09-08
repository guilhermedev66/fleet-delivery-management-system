using FleetDelivery.Modules.Vehicles.Domain;
using FluentAssertions;

namespace FleetDelivery.UnitTests.Vehicles;

// Exercises Vehicle.Register directly — no DB involved.
public class VehicleTests
{
    [Fact]
    public void Register_returns_an_Active_vehicle_with_a_normalized_plate_number()
    {
        var vehicle = Vehicle.Register("  abc-123  ", VehicleType.Van, 500m);

        vehicle.PlateNumber.Should().Be("ABC-123");
        vehicle.Type.Should().Be(VehicleType.Van);
        vehicle.CapacityKg.Should().Be(500m);
        vehicle.Status.Should().Be(VehicleStatus.Active);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Register_rejects_an_empty_plate_number(string plateNumber)
    {
        var act = () => Vehicle.Register(plateNumber, VehicleType.Truck, 1000m);

        act.Should().Throw<ArgumentException>().WithParameterName("plateNumber");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Register_rejects_a_non_positive_capacity(decimal capacityKg)
    {
        var act = () => Vehicle.Register("XYZ-789", VehicleType.Car, capacityKg);

        act.Should().Throw<ArgumentException>().WithParameterName("capacityKg");
    }
}

using FleetDelivery.BuildingBlocks.Domain;

namespace FleetDelivery.Modules.Vehicles.Domain;

/// <summary>
/// Aggregate root for a fleet vehicle. Currently create-and-read only — there
/// is no status-transition method yet because nothing in the product calls
/// for one (dispatch only reads <see cref="Status"/> to offer Active
/// vehicles for assignment). Add one (with the same explicit-transition-table
/// discipline as <c>Shipment</c>) when a real "send to maintenance" / "retire"
/// workflow is actually needed, instead of speculatively exposing a public
/// status setter now.
/// </summary>
public sealed class Vehicle : AggregateRoot<Guid>
{
    // Reserved for EF Core materialization.
    private Vehicle()
    {
    }

    private Vehicle(Guid id, string plateNumber, VehicleType type, decimal capacityKg, DateTimeOffset createdAt)
        : base(id)
    {
        PlateNumber = plateNumber;
        Type = type;
        CapacityKg = capacityKg;
        Status = VehicleStatus.Active;
        CreatedAt = createdAt;
    }

    /// <summary>Normalized (trimmed, uppercased) on <see cref="Register"/> — plate numbers are physically unique regardless of how a dispatcher happens to type case, so uniqueness is enforced on the normalized form (DB unique index; see <c>VehicleConfiguration</c>).</summary>
    public string PlateNumber { get; private set; } = string.Empty;

    public VehicleType Type { get; private set; }

    public decimal CapacityKg { get; private set; }

    public VehicleStatus Status { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>Registers a new vehicle. Always starts <see cref="VehicleStatus.Active"/> — there is no "register directly into Maintenance/Retired" use case.</summary>
    public static Vehicle Register(string plateNumber, VehicleType type, decimal capacityKg)
    {
        if (string.IsNullOrWhiteSpace(plateNumber))
        {
            throw new ArgumentException("Plate number cannot be empty.", nameof(plateNumber));
        }

        if (capacityKg <= 0)
        {
            throw new ArgumentException("Capacity must be greater than zero.", nameof(capacityKg));
        }

        return new Vehicle(Guid.NewGuid(), plateNumber.Trim().ToUpperInvariant(), type, capacityKg, DateTimeOffset.UtcNow);
    }
}

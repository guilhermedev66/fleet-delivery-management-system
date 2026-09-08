namespace FleetDelivery.Modules.Vehicles.Application.Abstractions;

/// <summary>
/// Thrown by Infrastructure (translated from the Postgres unique-constraint
/// violation on <c>vehicles.plate_number</c>) when a save loses a race
/// against another concurrent registration of the same plate number — the
/// backstop behind <see cref="IVehicleRepository.ExistsByPlateNumberAsync"/>'s
/// pre-check, which only catches the common non-concurrent case.
/// </summary>
public sealed class DuplicatePlateNumberException(Exception innerException)
    : Exception("A vehicle with this plate number already exists.", innerException)
{
}

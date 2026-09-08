namespace FleetDelivery.Modules.Shipments.Application.Shipments;

/// <summary>
/// Plain-string status name, matching <c>FleetDelivery.Modules.Vehicles.Domain.VehicleStatus.ToString()</c>
/// exactly. Mirrors <see cref="RoleNames"/>'s rationale: Shipments.Application
/// validates an assignable vehicleId against Vehicles' own Application public
/// contract (<c>GetVehicleByIdQuery</c>, via MediatR), whose <c>VehicleDto.Status</c>
/// is already a string for the same cross-module-decoupling reason — no need
/// to reference Vehicles.Domain for a single enum.
/// </summary>
public static class VehicleStatusNames
{
    public const string Active = "Active";
}

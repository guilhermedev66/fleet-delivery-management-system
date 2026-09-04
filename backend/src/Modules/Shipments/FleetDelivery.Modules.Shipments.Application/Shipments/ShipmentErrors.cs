using FleetDelivery.BuildingBlocks.Results;

namespace FleetDelivery.Modules.Shipments.Application.Shipments;

/// <summary>Expected-failure errors for the Shipments module.</summary>
public static class ShipmentErrors
{
    /// <summary>
    /// Also returned when a Driver requests a shipment that exists but isn't
    /// assigned to them — deliberately indistinguishable from a genuine
    /// 404, so the response never confirms the shipment's existence to a
    /// caller who shouldn't see it (IDOR hardening).
    /// </summary>
    public static readonly Error NotFound = new("Shipment.NotFound", "Shipment not found.");

    public static Error InvalidTransition(string message) => new("Shipment.InvalidTransition", message);

    public static readonly Error ConcurrencyConflict = new(
        "Shipment.ConcurrencyConflict",
        "The shipment was modified by someone else since it was last read. Reload and try again.");

    public static readonly Error InvalidDriver = new(
        "Shipment.InvalidDriver",
        "The specified driverId does not reference an active user with the Driver role.");

    public static Error Validation(string message) => new("Shipment.Validation", message);
}

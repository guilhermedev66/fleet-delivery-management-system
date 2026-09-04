namespace FleetDelivery.BuildingBlocks.Results;

/// <summary>
/// A business/validation error carried by a failed <see cref="Result"/> or
/// <see cref="Result{T}"/>. Distinct from exceptions, which remain reserved
/// for truly exceptional/unexpected failures.
/// </summary>
/// <param name="Code">A short, stable, machine-readable code (e.g. "Shipment.NotFound").</param>
/// <param name="Message">A human-readable description of the failure.</param>
public sealed record Error(string Code, string Message)
{
    public static readonly Error None = new(string.Empty, string.Empty);
}

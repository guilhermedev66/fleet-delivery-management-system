namespace FleetDelivery.Modules.Shipments.Application.Abstractions;

/// <summary>
/// Thrown by <see cref="IUnitOfWork.SaveChangesAsync"/> when the DB-level
/// optimistic-concurrency check (a real <c>WHERE version = @original</c> on
/// <c>UPDATE</c>) finds the row already changed — the true-concurrent-write
/// backstop behind the fast, explicit <c>shipment.Version != ExpectedVersion</c>
/// check every command handler does before attempting a transition. Kept as
/// a plain Application-layer exception (rather than handlers catching EF
/// Core's <c>DbUpdateConcurrencyException</c> directly) so Application has
/// zero EF Core references — <c>ShipmentsDbContext</c> translates the EF
/// exception into this one.
/// </summary>
public sealed class ConcurrencyConflictException : Exception
{
    public ConcurrencyConflictException()
        : base("The row was modified by another writer since it was read.")
    {
    }

    public ConcurrencyConflictException(Exception innerException)
        : base("The row was modified by another writer since it was read.", innerException)
    {
    }
}

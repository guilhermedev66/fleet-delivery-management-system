namespace FleetDelivery.Modules.Shipments.Application.Abstractions;

/// <summary>Wraps <c>ShipmentsDbContext.SaveChangesAsync</c> so Application handlers never reference EF Core directly.</summary>
public interface IUnitOfWork
{
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

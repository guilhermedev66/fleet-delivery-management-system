namespace FleetDelivery.Modules.Identity.Application.Abstractions;

/// <summary>Wraps <c>IdentityDbContext.SaveChangesAsync</c> so Application handlers never reference EF Core directly.</summary>
public interface IUnitOfWork
{
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

using FleetDelivery.Modules.Identity.Domain;

namespace FleetDelivery.Modules.Identity.Application.Abstractions;

/// <summary>Persistence abstraction for <see cref="RefreshToken"/>, implemented against <c>IdentityDbContext</c> in Infrastructure.</summary>
public interface IRefreshTokenRepository
{
    Task<RefreshToken?> GetByTokenHashAsync(string tokenHash, CancellationToken cancellationToken = default);

    /// <summary>All currently-active (not revoked, not expired) tokens for a user — used for theft-detection chain revocation.</summary>
    Task<IReadOnlyCollection<RefreshToken>> GetActiveByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);

    void Add(RefreshToken refreshToken);
}

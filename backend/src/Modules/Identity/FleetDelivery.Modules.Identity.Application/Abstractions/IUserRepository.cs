using FleetDelivery.Modules.Identity.Domain;

namespace FleetDelivery.Modules.Identity.Application.Abstractions;

/// <summary>Persistence abstraction for <see cref="User"/>, implemented against <c>IdentityDbContext</c> in Infrastructure.</summary>
public interface IUserRepository
{
    Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Looks up a user by email, case-insensitively.</summary>
    Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);

    Task<bool> AnyAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<User>> ListByRoleAsync(Role role, CancellationToken cancellationToken = default);

    void Add(User user);
}

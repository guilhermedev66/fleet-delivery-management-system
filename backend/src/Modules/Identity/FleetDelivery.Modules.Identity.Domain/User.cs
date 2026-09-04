using FleetDelivery.BuildingBlocks.Domain;

namespace FleetDelivery.Modules.Identity.Domain;

/// <summary>
/// Aggregate root for an authenticated principal. Password hashing and JWT
/// issuance live in Infrastructure/Application — this type only owns the
/// invariants of "what a user is" (a validated email, a role, an active
/// flag), never a plaintext password.
/// </summary>
public sealed class User : AggregateRoot<Guid>
{
    // Reserved for EF Core materialization.
    private User()
    {
    }

    private User(Guid id, Email email, string passwordHash, string fullName, Role role, DateTimeOffset createdAt)
        : base(id)
    {
        Email = email;
        NormalizedEmail = email.Normalize();
        PasswordHash = passwordHash;
        FullName = fullName;
        Role = role;
        IsActive = true;
        CreatedAt = createdAt;
    }

    public Email Email { get; private set; } = null!;

    /// <summary>
    /// Uppercase-invariant projection of <see cref="Email"/>, persisted as
    /// its own column so the case-insensitive uniqueness constraint can be
    /// a plain unique index rather than an expression index. See
    /// backend/README.md for the citext-vs-normalized-column tradeoff.
    /// </summary>
    public string NormalizedEmail { get; private set; } = string.Empty;

    /// <summary>
    /// Password hash produced by <see cref="Microsoft.AspNetCore.Identity.PasswordHasher{TUser}"/>.
    /// Never a plaintext password. No public setter — only replaced via
    /// domain behavior (none exposed yet in M1; credential rotation is a
    /// later use case).
    /// </summary>
    public string PasswordHash { get; private set; } = string.Empty;

    public string FullName { get; private set; } = string.Empty;

    public Role Role { get; private set; }

    public bool IsActive { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>
    /// Registers a new user. <paramref name="passwordHash"/> must already be
    /// hashed (e.g. via <see cref="Microsoft.AspNetCore.Identity.PasswordHasher{TUser}"/>)
    /// by the caller — this aggregate never sees a plaintext password.
    /// </summary>
    public static User Register(string email, string passwordHash, string fullName, Role role)
    {
        if (string.IsNullOrWhiteSpace(passwordHash))
        {
            throw new ArgumentException("Password hash cannot be empty.", nameof(passwordHash));
        }

        if (string.IsNullOrWhiteSpace(fullName))
        {
            throw new ArgumentException("Full name cannot be empty.", nameof(fullName));
        }

        var emailVo = Email.Create(email);

        return new User(Guid.NewGuid(), emailVo, passwordHash, fullName.Trim(), role, DateTimeOffset.UtcNow);
    }
}

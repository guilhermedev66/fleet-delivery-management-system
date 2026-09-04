using FleetDelivery.BuildingBlocks.Domain;

namespace FleetDelivery.Modules.Identity.Domain;

/// <summary>
/// Server-side record of an issued refresh token. The raw token is NEVER
/// stored — only <see cref="TokenHash"/> (SHA-256 of the raw token). Refresh
/// tokens rotate on use: a successful refresh revokes this row (setting
/// <see cref="RevokedAt"/> and <see cref="ReplacedByTokenHash"/>) and issues
/// a brand new row. Reusing a revoked token must fail closed — see
/// <see cref="IsActive"/>.
/// </summary>
public sealed class RefreshToken : Entity<Guid>
{
    // Reserved for EF Core materialization.
    private RefreshToken()
    {
    }

    private RefreshToken(Guid id, Guid userId, string tokenHash, DateTimeOffset expiresAt, DateTimeOffset createdAt)
        : base(id)
    {
        UserId = userId;
        TokenHash = tokenHash;
        ExpiresAt = expiresAt;
        CreatedAt = createdAt;
    }

    public Guid UserId { get; private set; }

    /// <summary>SHA-256 hash of the raw token. The raw value is returned to the client exactly once and never persisted.</summary>
    public string TokenHash { get; private set; } = string.Empty;

    public DateTimeOffset ExpiresAt { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? RevokedAt { get; private set; }

    /// <summary>Hash of the token this row was rotated into, if any. Lets a reused/stolen token be traced forward.</summary>
    public string? ReplacedByTokenHash { get; private set; }

    public bool IsRevoked => RevokedAt is not null;

    public bool IsExpired => DateTimeOffset.UtcNow >= ExpiresAt;

    /// <summary>Not revoked and not expired — the only state in which this token may be redeemed.</summary>
    public bool IsActive => !IsRevoked && !IsExpired;

    public static RefreshToken Issue(Guid userId, string tokenHash, TimeSpan lifetime)
    {
        if (string.IsNullOrWhiteSpace(tokenHash))
        {
            throw new ArgumentException("Token hash cannot be empty.", nameof(tokenHash));
        }

        var now = DateTimeOffset.UtcNow;

        return new RefreshToken(Guid.NewGuid(), userId, tokenHash, now.Add(lifetime), now);
    }

    /// <summary>Revokes this token, recording which token it was rotated into (if any).</summary>
    public void Revoke(string? replacedByTokenHash = null)
    {
        if (IsRevoked)
        {
            return;
        }

        RevokedAt = DateTimeOffset.UtcNow;
        ReplacedByTokenHash = replacedByTokenHash;
    }
}

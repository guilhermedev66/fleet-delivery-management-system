using FleetDelivery.Modules.Identity.Domain;
using FluentAssertions;

namespace FleetDelivery.UnitTests.Identity;

// Exercises RefreshToken's own state-transition methods directly — no DB involved.
public class RefreshTokenTests
{
    [Fact]
    public void Issue_creates_an_active_token()
    {
        var token = RefreshToken.Issue(Guid.NewGuid(), "some-hash", TimeSpan.FromDays(7));

        token.IsActive.Should().BeTrue();
        token.IsRevoked.Should().BeFalse();
        token.IsExpired.Should().BeFalse();
        token.RevokedAt.Should().BeNull();
    }

    [Fact]
    public void Issue_with_zero_or_negative_lifetime_is_immediately_expired_and_inactive()
    {
        var token = RefreshToken.Issue(Guid.NewGuid(), "some-hash", TimeSpan.Zero);

        token.IsExpired.Should().BeTrue();
        token.IsActive.Should().BeFalse();
    }

    [Fact]
    public void Rotation_revokes_the_old_token_and_links_it_to_the_new_ones_hash()
    {
        var userId = Guid.NewGuid();
        var oldToken = RefreshToken.Issue(userId, "old-hash", TimeSpan.FromDays(7));
        var newToken = RefreshToken.Issue(userId, "new-hash", TimeSpan.FromDays(7));

        oldToken.Revoke(newToken.TokenHash);

        // Old token is no longer usable...
        oldToken.IsActive.Should().BeFalse();
        oldToken.IsRevoked.Should().BeTrue();
        oldToken.ReplacedByTokenHash.Should().Be(newToken.TokenHash);

        // ...while the newly issued one is the only active one in the chain.
        newToken.IsActive.Should().BeTrue();
    }

    [Fact]
    public void Revoke_is_idempotent_and_does_not_overwrite_the_original_revocation()
    {
        var token = RefreshToken.Issue(Guid.NewGuid(), "hash", TimeSpan.FromDays(7));

        token.Revoke("first-replacement");
        var firstRevokedAt = token.RevokedAt;
        token.Revoke("second-replacement");

        token.RevokedAt.Should().Be(firstRevokedAt);
        token.ReplacedByTokenHash.Should().Be("first-replacement");
    }

    [Fact]
    public void Issue_with_empty_token_hash_throws()
    {
        var act = () => RefreshToken.Issue(Guid.NewGuid(), string.Empty, TimeSpan.FromDays(7));

        act.Should().Throw<ArgumentException>();
    }
}

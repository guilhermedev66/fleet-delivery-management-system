using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using FleetDelivery.Modules.Identity.Domain;
using FleetDelivery.Modules.Identity.Infrastructure.Security;
using FluentAssertions;
using Microsoft.Extensions.Options;

namespace FleetDelivery.UnitTests.Identity;

public class JwtTokenServiceTests
{
    private readonly JwtTokenService _sut = new(Options.Create(new JwtOptions
    {
        Issuer = "FleetDelivery.Tests",
        Audience = "FleetDelivery.Tests.Client",
        // 32+ bytes once UTF-8 encoded, as HMAC-SHA256 requires.
        SigningKey = "unit-test-signing-key-not-for-production-0123456789",
    }));

    private static User CreateUser() =>
        User.Register("driver@fleetdelivery.local", "irrelevant-hash", "Test Driver", Role.Driver);

    [Fact]
    public void CreateAccessToken_produces_a_token_with_sub_email_and_role_claims()
    {
        var user = CreateUser();

        var accessToken = _sut.CreateAccessToken(user);

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(accessToken.Value);

        jwt.Claims.Should().Contain(c => c.Type == JwtRegisteredClaimNames.Sub && c.Value == user.Id.ToString());
        jwt.Claims.Should().Contain(c => c.Type == JwtRegisteredClaimNames.Email && c.Value == user.Email.Value);
        jwt.Claims.Should().Contain(c => c.Type == ClaimTypes.Role && c.Value == Role.Driver.ToString());
    }

    [Fact]
    public void CreateAccessToken_sets_expiry_about_fifteen_minutes_out()
    {
        var user = CreateUser();
        var before = DateTimeOffset.UtcNow;

        var accessToken = _sut.CreateAccessToken(user);

        accessToken.ExpiresAt.Should().BeCloseTo(before.AddMinutes(15), TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void GenerateRefreshToken_produces_unique_values_each_call()
    {
        var first = _sut.GenerateRefreshToken();
        var second = _sut.GenerateRefreshToken();

        first.Should().NotBe(second);
    }

    [Fact]
    public void HashRefreshToken_is_deterministic_and_never_reproduces_the_raw_token()
    {
        var raw = _sut.GenerateRefreshToken();

        var hash1 = _sut.HashRefreshToken(raw);
        var hash2 = _sut.HashRefreshToken(raw);

        hash1.Should().Be(hash2);
        hash1.Should().NotBe(raw);
    }
}

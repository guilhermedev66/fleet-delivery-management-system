using FleetDelivery.Modules.Identity.Infrastructure.Security;
using FluentAssertions;

namespace FleetDelivery.UnitTests.Identity;

public class PasswordHasherTests
{
    private readonly PasswordHasher _sut = new();

    [Fact]
    public void Hash_then_verify_with_correct_password_succeeds()
    {
        const string password = "Correct-Horse-Battery-Staple-1";

        var hash = _sut.Hash(password);
        var verified = _sut.Verify(hash, password);

        verified.Should().BeTrue();
    }

    [Fact]
    public void Verify_with_wrong_password_fails()
    {
        const string password = "Correct-Horse-Battery-Staple-1";
        var hash = _sut.Hash(password);

        var verified = _sut.Verify(hash, "a-completely-different-password");

        verified.Should().BeFalse();
    }

    [Fact]
    public void Hash_never_stores_the_plaintext_password()
    {
        const string password = "Correct-Horse-Battery-Staple-1";

        var hash = _sut.Hash(password);

        hash.Should().NotContain(password);
    }

    [Fact]
    public void Hashing_the_same_password_twice_produces_different_hashes()
    {
        // PasswordHasher salts each hash independently, so two hashes of the
        // same password must never be equal even though both verify correctly.
        const string password = "Correct-Horse-Battery-Staple-1";

        var hash1 = _sut.Hash(password);
        var hash2 = _sut.Hash(password);

        hash1.Should().NotBe(hash2);
        _sut.Verify(hash1, password).Should().BeTrue();
        _sut.Verify(hash2, password).Should().BeTrue();
    }
}

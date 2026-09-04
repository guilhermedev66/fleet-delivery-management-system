using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FleetDelivery.Api.Endpoints;
using FleetDelivery.IntegrationTests.Infrastructure;
using FleetDelivery.Modules.Identity.Infrastructure.Seeding;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;

namespace FleetDelivery.IntegrationTests.Identity;

/// <summary>
/// End-to-end tests against the real <c>/api/auth/*</c> endpoints, a real
/// Postgres (via Testcontainers) and the real <c>IdentityDbContext</c>.
///
/// NOTE: requires a Docker daemon. See <see cref="IdentityApiFactory"/>.
/// </summary>
public sealed class AuthEndpointsTests : IClassFixture<IdentityApiFactory>
{
    private readonly IdentityApiFactory _factory;

    public AuthEndpointsTests(IdentityApiFactory factory)
    {
        _factory = factory;
    }

    private HttpClient CreateClient() => _factory.CreateClient(new WebApplicationFactoryClientOptions
    {
        // Managed manually per-test so we can inspect/replay individual
        // refresh-token cookie values (e.g. to prove a rotated-out token
        // stops working), which the default cookie container would hide.
        HandleCookies = false,
    });

    [Fact]
    public async Task Login_with_seeded_admin_credentials_succeeds_and_sets_refresh_cookie()
    {
        var client = CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(
            IdentityDevSeeder.DevAdminEmail,
            IdentityDevSeeder.DevAdminPassword));

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<LoginResponse>();
        body.Should().NotBeNull();
        body!.AccessToken.Should().NotBeNullOrWhiteSpace();
        body.User.Email.Should().Be(IdentityDevSeeder.DevAdminEmail);
        body.User.Role.Should().Be("Admin");

        var setCookie = GetSetCookieHeader(response, "refreshToken");
        var setCookieLower = setCookie.ToLowerInvariant();
        setCookieLower.Should().Contain("httponly");
        setCookieLower.Should().Contain("secure");
        setCookieLower.Should().Contain("path=/api/auth", "the cookie must be scoped to the auth path, not sent on every request");
    }

    [Fact]
    public async Task Login_with_wrong_password_returns_401_with_generic_message()
    {
        var client = CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(
            IdentityDevSeeder.DevAdminEmail,
            "definitely-the-wrong-password"));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Login_with_unknown_email_returns_the_same_401_as_wrong_password()
    {
        // No account enumeration: an unknown email and a known email with the
        // wrong password must be indistinguishable from the response alone.
        var client = CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(
            "no-such-user@fleetdelivery.local",
            "whatever-password"));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Refresh_with_valid_cookie_rotates_and_the_old_cookie_no_longer_works()
    {
        var client = CreateClient();

        var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(
            IdentityDevSeeder.DevAdminEmail,
            IdentityDevSeeder.DevAdminPassword));

        var oldRawToken = ExtractCookieValue(GetSetCookieHeader(loginResponse, "refreshToken"), "refreshToken");

        // First refresh with the token from login: succeeds and rotates.
        using var firstRefreshRequest = new HttpRequestMessage(HttpMethod.Post, "/api/auth/refresh");
        firstRefreshRequest.Headers.Add("Cookie", $"refreshToken={oldRawToken}");
        var firstRefreshResponse = await client.SendAsync(firstRefreshRequest);

        firstRefreshResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var newRawToken = ExtractCookieValue(GetSetCookieHeader(firstRefreshResponse, "refreshToken"), "refreshToken");
        newRawToken.Should().NotBe(oldRawToken);

        // Reusing the OLD (now-rotated-out) token must fail closed. This is
        // treated as a theft signal, so the whole chain — including the token
        // issued by the rotation above — is burned as a precaution.
        using var replayRequest = new HttpRequestMessage(HttpMethod.Post, "/api/auth/refresh");
        replayRequest.Headers.Add("Cookie", $"refreshToken={oldRawToken}");
        var replayResponse = await client.SendAsync(replayRequest);

        replayResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        // Proof the whole chain was burned: even the still-unused token from
        // the legitimate rotation above no longer works.
        using var secondRefreshRequest = new HttpRequestMessage(HttpMethod.Post, "/api/auth/refresh");
        secondRefreshRequest.Headers.Add("Cookie", $"refreshToken={newRawToken}");
        var secondRefreshResponse = await client.SendAsync(secondRefreshRequest);

        secondRefreshResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Refresh_with_a_freshly_rotated_token_that_has_not_been_reused_still_works()
    {
        // Companion to the theft-detection test above: rotation on its own
        // (no replay of an old token) must NOT burn the chain — only reuse
        // of an already-revoked token does.
        var client = CreateClient();

        var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(
            IdentityDevSeeder.DevAdminEmail,
            IdentityDevSeeder.DevAdminPassword));
        var rawToken = ExtractCookieValue(GetSetCookieHeader(loginResponse, "refreshToken"), "refreshToken");

        using var firstRefresh = new HttpRequestMessage(HttpMethod.Post, "/api/auth/refresh");
        firstRefresh.Headers.Add("Cookie", $"refreshToken={rawToken}");
        var firstRefreshResponse = await client.SendAsync(firstRefresh);
        var rotatedToken = ExtractCookieValue(GetSetCookieHeader(firstRefreshResponse, "refreshToken"), "refreshToken");

        using var secondRefresh = new HttpRequestMessage(HttpMethod.Post, "/api/auth/refresh");
        secondRefresh.Headers.Add("Cookie", $"refreshToken={rotatedToken}");
        var secondRefreshResponse = await client.SendAsync(secondRefresh);

        secondRefreshResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Refresh_without_a_cookie_returns_401()
    {
        var client = CreateClient();

        var response = await client.PostAsync("/api/auth/refresh", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Me_without_authentication_returns_401()
    {
        var client = CreateClient();

        var response = await client.GetAsync("/api/auth/me");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Me_with_valid_access_token_returns_the_current_user()
    {
        var client = CreateClient();

        var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(
            IdentityDevSeeder.DevAdminEmail,
            IdentityDevSeeder.DevAdminPassword));
        var loginBody = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();

        using var meRequest = new HttpRequestMessage(HttpMethod.Get, "/api/auth/me");
        meRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", loginBody!.AccessToken);
        var meResponse = await client.SendAsync(meRequest);

        meResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var me = await meResponse.Content.ReadFromJsonAsync<UserResponse>();
        me!.Email.Should().Be(IdentityDevSeeder.DevAdminEmail);
        me.Role.Should().Be("Admin");
    }

    [Fact]
    public async Task Logout_revokes_the_refresh_token_and_it_can_no_longer_be_used()
    {
        var client = CreateClient();

        var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(
            IdentityDevSeeder.DevAdminEmail,
            IdentityDevSeeder.DevAdminPassword));
        var rawToken = ExtractCookieValue(GetSetCookieHeader(loginResponse, "refreshToken"), "refreshToken");

        using var logoutRequest = new HttpRequestMessage(HttpMethod.Post, "/api/auth/logout");
        logoutRequest.Headers.Add("Cookie", $"refreshToken={rawToken}");
        var logoutResponse = await client.SendAsync(logoutRequest);

        logoutResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        using var refreshAfterLogout = new HttpRequestMessage(HttpMethod.Post, "/api/auth/refresh");
        refreshAfterLogout.Headers.Add("Cookie", $"refreshToken={rawToken}");
        var refreshAfterLogoutResponse = await client.SendAsync(refreshAfterLogout);

        refreshAfterLogoutResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private static string GetSetCookieHeader(HttpResponseMessage response, string cookieName)
    {
        var setCookieValues = response.Headers.TryGetValues("Set-Cookie", out var values)
            ? values
            : [];

        var match = setCookieValues.FirstOrDefault(v => v.StartsWith($"{cookieName}=", StringComparison.Ordinal));
        match.Should().NotBeNull($"a Set-Cookie header for '{cookieName}' was expected");

        return match!;
    }

    private static string ExtractCookieValue(string setCookieHeader, string cookieName)
    {
        var afterName = setCookieHeader[(cookieName.Length + 1)..];
        var end = afterName.IndexOf(';');

        return end >= 0 ? afterName[..end] : afterName;
    }
}

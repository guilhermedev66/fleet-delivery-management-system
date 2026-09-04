using System.Security.Claims;
using FleetDelivery.Modules.Identity.Application.Users;
using MediatR;

namespace FleetDelivery.Api.Endpoints;

public static class AuthEndpoints
{
    private const string RefreshCookieName = "refreshToken";
    private const string RefreshCookiePath = "/api/auth";

    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/auth").WithTags("Auth");

        group.MapPost("/login", LoginAsync).RequireRateLimiting("login");
        group.MapPost("/refresh", RefreshAsync);
        group.MapPost("/logout", LogoutAsync);
        group.MapGet("/me", GetCurrentUserAsync).RequireAuthorization();

        return app;
    }

    private static async Task<IResult> LoginAsync(LoginRequest request, ISender sender, HttpContext httpContext, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new LoginCommand(request.Email, request.Password), cancellationToken);

        if (result.IsFailure)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status401Unauthorized,
                title: "Invalid email or password.",
                type: "https://httpstatuses.io/401");
        }

        var value = result.Value;

        AppendRefreshTokenCookie(httpContext, value.RawRefreshToken, value.RefreshTokenExpiresAt);

        var response = new LoginResponse(
            value.AccessToken,
            value.AccessTokenExpiresAt,
            new UserResponse(value.User.Id, value.User.Email, value.User.FullName, value.User.Role));

        return Results.Ok(response);
    }

    private static async Task<IResult> RefreshAsync(HttpContext httpContext, ISender sender, CancellationToken cancellationToken)
    {
        if (!httpContext.Request.Cookies.TryGetValue(RefreshCookieName, out var rawRefreshToken) || string.IsNullOrEmpty(rawRefreshToken))
        {
            return Results.Unauthorized();
        }

        var result = await sender.Send(new RefreshTokenCommand(rawRefreshToken), cancellationToken);

        if (result.IsFailure)
        {
            httpContext.Response.Cookies.Delete(RefreshCookieName, new CookieOptions { Path = RefreshCookiePath });

            return Results.Unauthorized();
        }

        var value = result.Value;

        AppendRefreshTokenCookie(httpContext, value.RawRefreshToken, value.RefreshTokenExpiresAt);

        return Results.Ok(new RefreshResponse(value.AccessToken, value.AccessTokenExpiresAt));
    }

    private static async Task<IResult> LogoutAsync(HttpContext httpContext, ISender sender, CancellationToken cancellationToken)
    {
        httpContext.Request.Cookies.TryGetValue(RefreshCookieName, out var rawRefreshToken);

        await sender.Send(new LogoutCommand(rawRefreshToken), cancellationToken);

        httpContext.Response.Cookies.Delete(RefreshCookieName, new CookieOptions { Path = RefreshCookiePath });

        return Results.NoContent();
    }

    private static async Task<IResult> GetCurrentUserAsync(ClaimsPrincipal user, ISender sender, CancellationToken cancellationToken)
    {
        var subject = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.FindFirstValue("sub");

        if (subject is null || !Guid.TryParse(subject, out var userId))
        {
            return Results.Unauthorized();
        }

        var result = await sender.Send(new GetCurrentUserQuery(userId), cancellationToken);

        if (result.IsFailure)
        {
            return Results.NotFound();
        }

        var value = result.Value;

        return Results.Ok(new UserResponse(value.Id, value.Email, value.FullName, value.Role));
    }

    private static void AppendRefreshTokenCookie(HttpContext httpContext, string rawToken, DateTimeOffset expiresAt)
    {
        httpContext.Response.Cookies.Append(RefreshCookieName, rawToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            // Lax rather than Strict: same-site-but-cross-port SPA <-> API calls
            // during local dev, and cross-subdomain deployments, still need the
            // cookie attached to same-site fetches. Strict can silently drop it
            // on those. Documented in backend/README.md.
            SameSite = SameSiteMode.Lax,
            Path = RefreshCookiePath,
            Expires = expiresAt,
        });
    }
}

public sealed record LoginRequest(string Email, string Password);

public sealed record LoginResponse(string AccessToken, DateTimeOffset AccessTokenExpiresAt, UserResponse User);

public sealed record RefreshResponse(string AccessToken, DateTimeOffset AccessTokenExpiresAt);

public sealed record UserResponse(Guid Id, string Email, string FullName, string Role);

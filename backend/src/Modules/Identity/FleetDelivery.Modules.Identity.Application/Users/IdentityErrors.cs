using FleetDelivery.BuildingBlocks.Results;

namespace FleetDelivery.Modules.Identity.Application.Users;

/// <summary>
/// Expected-failure errors for the Identity module. <see cref="InvalidCredentials"/>
/// is deliberately reused for every login failure mode (unknown email, wrong
/// password, inactive account) so the API never reveals which one occurred —
/// no account enumeration.
/// </summary>
public static class IdentityErrors
{
    public static readonly Error InvalidCredentials = new("Identity.InvalidCredentials", "Invalid email or password.");

    public static readonly Error InvalidRefreshToken = new("Identity.InvalidRefreshToken", "Invalid or expired refresh token.");

    public static readonly Error UserNotFound = new("Identity.UserNotFound", "User not found.");
}

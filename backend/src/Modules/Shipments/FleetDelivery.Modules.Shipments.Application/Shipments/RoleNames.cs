namespace FleetDelivery.Modules.Shipments.Application.Shipments;

/// <summary>
/// Plain-string role names, matching <c>FleetDelivery.Modules.Identity.Domain.Role.ToString()</c>
/// exactly. Shipments.Application intentionally does not reference
/// Identity.Domain for a single enum — the caller's role reaches these
/// handlers as the string already carried on the JWT role claim (extracted
/// at the API layer), and <c>UserDto.Role</c> (Identity's own public
/// contract, used to validate an assignable driver) is a string for the
/// same cross-module-decoupling reason.
/// </summary>
public static class RoleNames
{
    public const string Driver = "Driver";
}

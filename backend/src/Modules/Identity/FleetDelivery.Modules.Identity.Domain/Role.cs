namespace FleetDelivery.Modules.Identity.Domain;

/// <summary>
/// RBAC role assigned to a <see cref="User"/>. Mapped to the JWT
/// <c>role</c> claim (<see cref="System.Security.Claims.ClaimTypes.Role"/>)
/// so <c>[Authorize(Roles = "...")]</c> works against it directly.
/// </summary>
public enum Role
{
    Admin,
    Dispatcher,
    Driver,
    Operations,
}

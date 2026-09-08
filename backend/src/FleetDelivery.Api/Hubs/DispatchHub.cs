using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace FleetDelivery.Api.Hubs;

/// <summary>
/// Server-push only — no client-callable methods. Group membership is
/// derived from the connection's authenticated claims in
/// <see cref="OnConnectedAsync"/>, never from anything the client sends
/// (docs/ARCHITECTURE.md's real-time security rule: "group membership is
/// derived from the user's server-side claims... never from a
/// client-supplied group name"). Dispatcher/Admin join
/// <see cref="DispatchersGroup"/> and receive every shipment event
/// (<see cref="RealTime.DispatchBoardConsumerHostedService"/> broadcasts to
/// it); every authenticated user also joins a per-user group for future
/// driver-specific push, unused by this milestone's frontend but harmless
/// and cheap to set up now rather than re-deriving the connection/group
/// wiring later.
/// </summary>
[Authorize]
public sealed class DispatchHub : Hub
{
    public const string DispatchersGroup = "dispatchers";

    public static string UserGroup(Guid userId) => $"user:{userId}";

    public override async Task OnConnectedAsync()
    {
        var role = Context.User?.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;
        var subject = Context.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
            ?? Context.User?.FindFirst("sub")?.Value;

        if (role is "Dispatcher" or "Admin")
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, DispatchersGroup);
        }

        if (Guid.TryParse(subject, out var userId))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, UserGroup(userId));
        }

        await base.OnConnectedAsync();
    }
}

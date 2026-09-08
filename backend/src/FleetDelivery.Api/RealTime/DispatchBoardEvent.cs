using System.Text.Json;

namespace FleetDelivery.Api.RealTime;

/// <summary>
/// What actually goes out over the wire to <c>DispatchHub</c> clients — a
/// thin projection of the RabbitMQ envelope, not the raw envelope itself
/// (its <c>correlationId</c>/internal plumbing has no reason to reach a
/// browser). <see cref="Data"/> stays whatever shape the underlying domain
/// event already serialized to; the frontend already has to branch on
/// <see cref="Type"/> to interpret it, so a bespoke per-event-type C#
/// contract here would just be duplicated shape-knowledge with nothing to
/// validate against.
/// </summary>
public sealed record DispatchBoardEvent(string Type, DateTimeOffset OccurredAt, JsonElement Data);

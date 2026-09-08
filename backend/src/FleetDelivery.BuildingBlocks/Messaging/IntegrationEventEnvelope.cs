using System.Text.Json.Serialization;

namespace FleetDelivery.BuildingBlocks.Messaging;

/// <summary>
/// The wire format every integration event is published in, per
/// docs/ARCHITECTURE.md's RabbitMQ topology: <c>{ messageId, type,
/// occurredAt, correlationId, data }</c>. <see cref="MessageId"/> is the
/// outbox row's own id (already globally unique, already the natural
/// idempotency key for a consumer's inbox pattern) — no separate id is
/// generated at publish time.
/// </summary>
public sealed record IntegrationEventEnvelope(
    Guid MessageId,
    string Type,
    DateTimeOffset OccurredAt,
    Guid CorrelationId,
    [property: JsonConverter(typeof(RawJsonConverter))] string Data);

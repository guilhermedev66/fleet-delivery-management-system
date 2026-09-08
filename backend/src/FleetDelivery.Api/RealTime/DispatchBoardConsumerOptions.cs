namespace FleetDelivery.Api.RealTime;

/// <summary>Bound from the <c>RealTime</c> configuration section.</summary>
public sealed class DispatchBoardConsumerOptions
{
    public const string SectionName = "RealTime";

    /// <summary>Off by default in test hosts that don't spin up a RabbitMQ container (same reasoning as <c>Outbox:PublisherEnabled</c>).</summary>
    public bool ConsumerEnabled { get; set; } = true;

    public string QueueName { get; set; } = "dispatch-board.fleet.events";

    /// <summary>Topic binding pattern — <c>#</c> (everything) because a dispatch board's whole point is seeing every shipment event, not a curated subset.</summary>
    public string RoutingPattern { get; set; } = "#";

    /// <summary>Dead-letter exchange a message is routed to when this consumer can't process it (malformed payload, or any other unrecoverable per-message error) — nack'd without requeue, never retried in a loop. See the doc comment on <see cref="DispatchBoardConsumerHostedService"/> for why this consumer doesn't do the outbox publisher's bounded-backoff retry instead.</summary>
    public string DeadLetterExchangeName { get; set; } = "fleet.events.dlx";

    public string DeadLetterQueueName { get; set; } = "dispatch-board.fleet.events.dlq";
}

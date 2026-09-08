namespace FleetDelivery.Modules.Shipments.Infrastructure.Messaging;

/// <summary>Bound from the <c>Outbox</c> configuration section. Controls the background publisher's cadence, not the broker connection itself (see <see cref="RabbitMqOptions"/>).</summary>
public sealed class OutboxPublisherOptions
{
    public const string SectionName = "Outbox";

    /// <summary>Whether <c>OutboxPublisherHostedService</c> is registered at all. Off by default in integration tests that don't exercise messaging (see <c>ShipmentsApiFactory</c>) — no RabbitMQ container to talk to there.</summary>
    public bool PublisherEnabled { get; set; } = true;

    /// <summary>How often the publisher polls for unprocessed rows.</summary>
    public int PollIntervalSeconds { get; set; } = 2;

    /// <summary>Max rows claimed per poll — bounds how long one batch (and the DB row locks it holds) can run for.</summary>
    public int BatchSize { get; set; } = 20;

    /// <summary>Ceiling on the exponential per-row retry backoff — a row that keeps failing is retried at most this often, never abandoned.</summary>
    public int MaxBackoffSeconds { get; set; } = 300;
}

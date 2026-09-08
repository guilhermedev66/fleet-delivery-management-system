namespace FleetDelivery.Modules.Shipments.Infrastructure.Messaging;

/// <summary>
/// Bound from the shared <c>RabbitMq</c> configuration section (same keys
/// docker-compose and appsettings already carry for the broker container).
/// Lives in Shipments.Infrastructure rather than BuildingBlocks because
/// Shipments is the only module publishing today — see the doc comment on
/// <see cref="RabbitMqConnectionProvider"/> for when to promote this.
/// </summary>
public sealed class RabbitMqOptions
{
    public const string SectionName = "RabbitMq";

    public string Host { get; set; } = string.Empty;

    public int Port { get; set; } = 5672;

    public string Username { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;

    public string VirtualHost { get; set; } = "/";

    /// <summary>Topic exchange every integration event is published to. See docs/ARCHITECTURE.md's RabbitMQ topology.</summary>
    public string ExchangeName { get; set; } = "fleet.events";
}

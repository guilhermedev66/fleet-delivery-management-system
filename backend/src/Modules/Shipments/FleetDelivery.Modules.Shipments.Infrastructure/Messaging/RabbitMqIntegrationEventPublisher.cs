using System.Text.Json;
using FleetDelivery.BuildingBlocks.Messaging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace FleetDelivery.Modules.Shipments.Infrastructure.Messaging;

/// <summary>
/// Publishes to the <c>fleet.events</c> topic exchange (declared durable,
/// idempotently, on first use — no consumer queues declared here: binding a
/// queue is a consumer's responsibility, and none exist yet). Messages are
/// published persistent (survive a broker restart) since nothing downstream
/// has run yet to have already durably recorded the event elsewhere.
/// </summary>
public sealed class RabbitMqIntegrationEventPublisher(RabbitMqConnectionProvider connectionProvider, IOptions<RabbitMqOptions> options)
    : IIntegrationEventPublisher, IAsyncDisposable
{
    // camelCase to match every other JSON contract this API already emits
    // (ASP.NET Core's Web defaults) — consumers shouldn't have to special-case
    // the messaging envelope's casing versus the HTTP API's.
    private static readonly JsonSerializerOptions EnvelopeSerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly RabbitMqOptions _options = options.Value;
    private readonly SemaphoreSlim _channelLock = new(1, 1);
    private IChannel? _channel;

    public async Task PublishAsync(string routingKey, OutboxMessage message, CancellationToken cancellationToken = default)
    {
        var channel = await GetOrCreateChannelAsync(cancellationToken);

        var envelope = new IntegrationEventEnvelope(
            message.Id,
            message.Type,
            message.OccurredOn,
            // No request-scoped correlation id is threaded through commands
            // yet (that's future work, likely alongside real OpenTelemetry
            // trace-context propagation) — self-correlated with the
            // message's own id is an honest placeholder, not a fabricated
            // value pretending to trace back to an originating request.
            message.Id,
            message.Content);

        var body = JsonSerializer.SerializeToUtf8Bytes(envelope, EnvelopeSerializerOptions);

        var properties = new BasicProperties
        {
            Persistent = true,
            ContentType = "application/json",
            MessageId = message.Id.ToString(),
            Type = message.Type,
            Timestamp = new AmqpTimestamp(message.OccurredOn.ToUnixTimeSeconds()),
        };

        await channel.BasicPublishAsync(
            exchange: _options.ExchangeName,
            routingKey: routingKey,
            mandatory: false,
            basicProperties: properties,
            body: body,
            cancellationToken: cancellationToken);
    }

    private async Task<IChannel> GetOrCreateChannelAsync(CancellationToken cancellationToken)
    {
        if (_channel is { IsOpen: true })
        {
            return _channel;
        }

        await _channelLock.WaitAsync(cancellationToken);

        try
        {
            if (_channel is { IsOpen: true })
            {
                return _channel;
            }

            _channel = await connectionProvider.CreateChannelAsync(cancellationToken);

            await _channel.ExchangeDeclareAsync(
                exchange: _options.ExchangeName,
                type: ExchangeType.Topic,
                durable: true,
                autoDelete: false,
                cancellationToken: cancellationToken);

            return _channel;
        }
        finally
        {
            _channelLock.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_channel is not null)
        {
            await _channel.DisposeAsync();
        }

        _channelLock.Dispose();
    }
}

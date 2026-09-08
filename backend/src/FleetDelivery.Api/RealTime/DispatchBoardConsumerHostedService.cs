using System.Text.Json;
using FleetDelivery.Api.Hubs;
using FleetDelivery.Modules.Shipments.Infrastructure.Messaging;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace FleetDelivery.Api.RealTime;

/// <summary>
/// The first real consumer of the M4 outbox publisher's events: binds a
/// durable queue to the <c>fleet.events</c> topic exchange (routing pattern
/// <c>#</c> — everything) and re-broadcasts each message to
/// <see cref="DispatchHub"/>'s <c>dispatchers</c> group. Reuses
/// <see cref="RabbitMqConnectionProvider"/> (already a singleton, already
/// wired by <c>AddShipmentsModule</c>) for its own channel rather than
/// opening a second connection — one broker connection per process, same as
/// the publisher.
///
/// Failure handling is deliberately simpler than the publisher's
/// bounded-backoff retry: this consumer's only work per message is parsing
/// JSON and calling <c>IHubContext.Clients.Group(...).SendAsync</c> — there
/// is no external dependency to be transiently down (SignalR is in-process),
/// so a failure here is overwhelmingly likely to be a permanently malformed
/// message, not a transient one. A failed message is nack'd without
/// requeue, which RabbitMQ routes to <see cref="DispatchBoardConsumerOptions.DeadLetterExchangeName"/>
/// (configured as the queue's <c>x-dead-letter-exchange</c> argument) —
/// visible for operator inspection, never retried in a loop, never silently
/// dropped.
///
/// Always registered (see <c>Program.cs</c>) — <see cref="DispatchBoardConsumerOptions.ConsumerEnabled"/>
/// is checked here, inside <see cref="ExecuteAsync"/>, via the properly
/// DI-resolved <see cref="IOptions{TOptions}"/>. A registration-time check
/// against raw <c>IConfiguration</c> (as this originally did) silently
/// misses <c>WebApplicationFactory</c>'s test config overrides — that
/// config is merged into the builder only when the real host is built,
/// which happens after Program.cs's own top-level statements have already
/// run — so <c>ShipmentsApiFactory</c>'s <c>RealTime:ConsumerEnabled = false</c>
/// override went unseen, this service started anyway, and its first call
/// (<see cref="RabbitMqConnectionProvider.CreateChannelAsync"/>, against a
/// broker that test host doesn't have) threw uncaught out of
/// <see cref="ExecuteAsync"/> — which, under .NET's default
/// <c>BackgroundServiceExceptionBehavior.StopHost</c>, crashed the entire
/// test host for every test using that factory, not just this one. Checking
/// the properly-bound option first avoids the whole class of bug — it's
/// unaffected by when the connection is actually attempted.
/// </summary>
public sealed class DispatchBoardConsumerHostedService(
    RabbitMqConnectionProvider connectionProvider,
    IHubContext<DispatchHub> hubContext,
    IOptions<DispatchBoardConsumerOptions> options,
    ILogger<DispatchBoardConsumerHostedService> logger) : BackgroundService
{
    private const string ExchangeName = "fleet.events";

    private readonly DispatchBoardConsumerOptions _options = options.Value;
    private IChannel? _channel;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.ConsumerEnabled)
        {
            logger.LogInformation("Dispatch board consumer disabled (RealTime:ConsumerEnabled=false) — not starting.");

            return;
        }

        // Bounded-backoff retry around the one-time setup (connect, declare
        // topology, register the consumer) — a RabbitMQ outage at process
        // startup (e.g. the broker container isn't up yet) must not crash
        // the whole API host via BackgroundServiceExceptionBehavior.StopHost.
        // Once BasicConsumeAsync succeeds, RabbitMqConnectionProvider's
        // AutomaticRecoveryEnabled connection handles resilience for drops
        // after that — this loop is purely for "never got connected yet".
        var attempt = 0;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                _channel = await connectionProvider.CreateChannelAsync(stoppingToken);

                await _channel.ExchangeDeclareAsync(ExchangeName, ExchangeType.Topic, durable: true, autoDelete: false, cancellationToken: stoppingToken);

                // Fanout: every failed message goes to the same DLQ regardless
                // of its original routing key — there's exactly one consumer's
                // worth of dead letters to collect right now, no need for
                // topic-shaped dead-letter routing until a second consumer
                // justifies it.
                await _channel.ExchangeDeclareAsync(_options.DeadLetterExchangeName, ExchangeType.Fanout, durable: true, autoDelete: false, cancellationToken: stoppingToken);
                await _channel.QueueDeclareAsync(_options.DeadLetterQueueName, durable: true, exclusive: false, autoDelete: false, cancellationToken: stoppingToken);
                await _channel.QueueBindAsync(_options.DeadLetterQueueName, _options.DeadLetterExchangeName, routingKey: string.Empty, cancellationToken: stoppingToken);

                await _channel.QueueDeclareAsync(
                    _options.QueueName,
                    durable: true,
                    exclusive: false,
                    autoDelete: false,
                    arguments: new Dictionary<string, object?> { ["x-dead-letter-exchange"] = _options.DeadLetterExchangeName },
                    cancellationToken: stoppingToken);

                await _channel.QueueBindAsync(_options.QueueName, ExchangeName, _options.RoutingPattern, cancellationToken: stoppingToken);

                // One in-flight message at a time is plenty for a stateless
                // broadcast — no reason to let the broker push a flood of
                // unacked deliveries at this consumer.
                await _channel.BasicQosAsync(prefetchSize: 0, prefetchCount: 1, global: false, cancellationToken: stoppingToken);

                var consumer = new AsyncEventingBasicConsumer(_channel);
                consumer.ReceivedAsync += OnMessageReceivedAsync;

                await _channel.BasicConsumeAsync(_options.QueueName, autoAck: false, consumer, stoppingToken);

                logger.LogInformation("Dispatch board consumer started on queue {QueueName}.", _options.QueueName);

                break;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                attempt++;
                var delay = TimeSpan.FromSeconds(Math.Min(2 * Math.Pow(2, Math.Min(attempt, 8)), 60));

                logger.LogError(ex, "Dispatch board consumer failed to start (attempt {Attempt}) — retrying in {Delay}.", attempt, delay);

                try
                {
                    await Task.Delay(delay, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    return;
                }
            }
        }

        try
        {
            // BasicConsumeAsync registers a callback-driven consumer and
            // returns immediately — this just keeps the hosted service alive
            // until shutdown, per BackgroundService's contract.
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Expected on graceful shutdown.
        }

        logger.LogInformation("Dispatch board consumer stopped.");
    }

    private async Task OnMessageReceivedAsync(object sender, BasicDeliverEventArgs eventArgs)
    {
        try
        {
            var envelope = JsonSerializer.Deserialize<JsonElement>(eventArgs.Body.Span);
            var type = envelope.GetProperty("type").GetString() ?? "Unknown";
            var occurredAt = envelope.GetProperty("occurredAt").GetDateTimeOffset();
            var data = envelope.GetProperty("data");

            await hubContext.Clients.Group(DispatchHub.DispatchersGroup)
                .SendAsync("shipmentEvent", new DispatchBoardEvent(type, occurredAt, data));

            await _channel!.BasicAckAsync(eventArgs.DeliveryTag, multiple: false);

            logger.LogInformation("Broadcast {Type} to the dispatchers group.", type);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to process a dispatch board message — dead-lettering it.");

            await _channel!.BasicNackAsync(eventArgs.DeliveryTag, multiple: false, requeue: false);
        }
    }

    // No explicit channel disposal here — RabbitMqConnectionProvider (a
    // singleton) disposes the whole connection, and every channel opened on
    // it, when the DI container shuts down.
}

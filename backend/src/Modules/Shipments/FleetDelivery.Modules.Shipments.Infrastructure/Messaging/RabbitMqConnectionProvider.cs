using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace FleetDelivery.Modules.Shipments.Infrastructure.Messaging;

/// <summary>
/// Owns one long-lived <see cref="IConnection"/> for the process, created
/// lazily on first use and reused for every subsequent channel. Registered
/// as a singleton — RabbitMQ connections are meant to be long-lived and
/// reused across many channels/publishes, not opened per request or per
/// outbox batch. <see cref="ConnectionFactory.AutomaticRecoveryEnabled"/>
/// handles reconnecting after a dropped connection without a hand-rolled
/// reconnect loop.
///
/// Deliberately Shipments-module-local (see the doc comment on
/// <see cref="RabbitMqOptions"/>) rather than a shared BuildingBlocks
/// service — promote it once a second module needs to publish, not before
/// (same YAGNI reasoning as the per-module Outbox tables).
/// </summary>
public sealed class RabbitMqConnectionProvider(IOptions<RabbitMqOptions> options) : IAsyncDisposable
{
    private readonly RabbitMqOptions _options = options.Value;
    private readonly SemaphoreSlim _connectionLock = new(1, 1);
    private IConnection? _connection;

    public async Task<IChannel> CreateChannelAsync(CancellationToken cancellationToken = default)
    {
        var connection = await GetOrCreateConnectionAsync(cancellationToken);

        return await connection.CreateChannelAsync(cancellationToken: cancellationToken);
    }

    private async Task<IConnection> GetOrCreateConnectionAsync(CancellationToken cancellationToken)
    {
        if (_connection is { IsOpen: true })
        {
            return _connection;
        }

        await _connectionLock.WaitAsync(cancellationToken);

        try
        {
            if (_connection is { IsOpen: true })
            {
                return _connection;
            }

            var factory = new ConnectionFactory
            {
                HostName = _options.Host,
                Port = _options.Port,
                UserName = _options.Username,
                Password = _options.Password,
                VirtualHost = _options.VirtualHost,
                AutomaticRecoveryEnabled = true,
                TopologyRecoveryEnabled = true,
            };

            _connection = await factory.CreateConnectionAsync(cancellationToken);

            return _connection;
        }
        finally
        {
            _connectionLock.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_connection is not null)
        {
            await _connection.DisposeAsync();
        }

        _connectionLock.Dispose();
    }
}

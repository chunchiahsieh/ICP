using System.Text;
using ICP.Models.Integration;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace ICP.Services.Integration;

public class RabbitMqPublisher : IRabbitMqPublisher, IAsyncDisposable
{
    private readonly IOptionsMonitor<IntegrationOptions> _options;
    private readonly ILogger<RabbitMqPublisher> _logger;
    private readonly SemaphoreSlim _connectionLock = new(1, 1);
    private IConnection? _connection;

    public RabbitMqPublisher(IOptionsMonitor<IntegrationOptions> options, ILogger<RabbitMqPublisher> logger)
    {
        _options = options;
        _logger = logger;
    }

    public async Task PublishAsync(string routingKey, string messageId, string payloadJson, CancellationToken cancellationToken = default)
    {
        var rabbitMq = _options.CurrentValue.RabbitMq;
        if (!rabbitMq.Enabled)
        {
            return;
        }

        var connection = await GetOrCreateConnectionAsync(rabbitMq, cancellationToken);
        await using var channel = await connection.CreateChannelAsync(cancellationToken: cancellationToken);
        var body = Encoding.UTF8.GetBytes(payloadJson);
        var properties = new BasicProperties
        {
            ContentType = "application/json",
            DeliveryMode = DeliveryModes.Persistent,
            MessageId = messageId,
            Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds())
        };

        await channel.BasicPublishAsync(
            exchange: rabbitMq.Exchange,
            routingKey: string.IsNullOrWhiteSpace(routingKey) ? rabbitMq.RoutingKey : routingKey,
            mandatory: false,
            basicProperties: properties,
            body: body,
            cancellationToken: cancellationToken);

        _logger.LogInformation(
            "Published integration event {MessageId} to exchange {Exchange} routingKey {RoutingKey}",
            messageId,
            rabbitMq.Exchange,
            rabbitMq.RoutingKey);
    }

    public async ValueTask DisposeAsync()
    {
        await _connectionLock.WaitAsync();
        try
        {
            if (_connection is not null)
            {
                await _connection.DisposeAsync();
                _connection = null;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error disposing RabbitMQ connection.");
        }
        finally
        {
            _connectionLock.Release();
            _connectionLock.Dispose();
        }
    }

    private async Task<IConnection> GetOrCreateConnectionAsync(RabbitMqOptions rabbitMq, CancellationToken cancellationToken)
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

            if (_connection is not null)
            {
                await _connection.DisposeAsync();
                _connection = null;
            }

            var factory = new ConnectionFactory
            {
                HostName = rabbitMq.HostName,
                Port = rabbitMq.Port,
                VirtualHost = rabbitMq.VirtualHost,
                UserName = rabbitMq.UserName,
                Password = rabbitMq.Password
            };

            _connection = await factory.CreateConnectionAsync(cancellationToken);
            return _connection;
        }
        finally
        {
            _connectionLock.Release();
        }
    }
}

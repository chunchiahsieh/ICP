namespace TEL.IntegrationHub.Models;

public class HubIntegrationOptions
{
    public const string SectionName = "Integration";

    public RabbitMqOptions RabbitMq { get; set; } = new();

    public DatabaseOptions Database { get; set; } = new();
}

public class RabbitMqOptions
{
    public bool Enabled { get; set; }

    public string HostName { get; set; } = "localhost";

    public int Port { get; set; } = 5672;

    public string VirtualHost { get; set; } = "/";

    public string UserName { get; set; } = "guest";

    public string Password { get; set; } = "guest";

    public string Exchange { get; set; } = "tel.integration";

    public string ExchangeType { get; set; } = "topic";

    public string RoutingKey { get; set; } = IcpIntegrationEventTypes.ShipInfoCaseInitiated;

    /// <summary>
    /// Topic bindings for the Hub queue. When empty, falls back to <see cref="RoutingKey"/>.
    /// </summary>
    public string[] RoutingKeys { get; set; } =
    [
        IcpIntegrationEventTypes.ShipInfoCaseInitiated,
        IcpIntegrationEventTypes.ExportCompleted
    ];

    public string QueueName { get; set; } = "tel.integration.hub.queue";

    public IReadOnlyList<string> ResolveRoutingKeys()
    {
        if (RoutingKeys is { Length: > 0 })
        {
            return RoutingKeys
                .Where(k => !string.IsNullOrWhiteSpace(k))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        return string.IsNullOrWhiteSpace(RoutingKey)
            ? Array.Empty<string>()
            : [RoutingKey];
    }
}

public class DatabaseOptions
{
    public bool EnsureCreatedOnStartup { get; set; } = true;
}

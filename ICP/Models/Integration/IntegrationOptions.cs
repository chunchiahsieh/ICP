namespace ICP.Models.Integration;

public class IntegrationOptions
{
    public const string SectionName = "Integration";

    public RabbitMqOptions RabbitMq { get; set; } = new();

    public OutboxOptions Outbox { get; set; } = new();

    public HubClientOptions Hub { get; set; } = new();

    public ExportClientOptions Export { get; set; } = new();
}

public class ExportClientOptions
{
    /// <summary>
    /// Shared output root with FileGenerator (same host).
    /// Default: folder <c>ICPFileGenerator</c> under the ICP project ContentRoot.
    /// </summary>
    public string OutputDirectory { get; set; } = "ICPFileGenerator";
}

public class HubClientOptions
{
    /// <summary>Base URL of TEL Integration Hub (e.g. http://localhost:5261).</summary>
    public string BaseUrl { get; set; } = "http://localhost:5261";
}

public class RabbitMqOptions
{
    public bool Enabled { get; set; }

    public string HostName { get; set; } = "localhost";

    public int Port { get; set; } = 5672;

    public string VirtualHost { get; set; } = "/";

    public string UserName { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;

    public string Exchange { get; set; } = "tel.integration";

    public string RoutingKey { get; set; } = "icp.shipinfo.case.initiated";
}

public class OutboxOptions
{
    public int PollIntervalSeconds { get; set; } = 10;

    public int MaxRetryCount { get; set; } = 5;

    public int BatchSize { get; set; } = 20;
}

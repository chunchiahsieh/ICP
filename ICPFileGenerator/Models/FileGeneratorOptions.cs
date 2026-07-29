namespace ICPFileGenerator.Models;

public class FileGeneratorOptions
{
    public const string SectionName = "FileGenerator";

    public int PollingIntervalSeconds { get; set; } = 10;

    public string WorkerId { get; set; } = "ICPFileGenerator-01";

    public string OutputDirectory { get; set; } = "Output";

    public int ProcessingTimeoutMinutes { get; set; } = 30;

    public int MaxRetryCount { get; set; } = 3;

    public HubClientOptions Hub { get; set; } = new();
}

public class HubClientOptions
{
    public string BaseUrl { get; set; } = "http://localhost:5261";
}

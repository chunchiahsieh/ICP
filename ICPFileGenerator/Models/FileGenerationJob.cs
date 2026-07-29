namespace ICPFileGenerator.Models;

public static class FileGenerationJobStatuses
{
    public const string Pending = "Pending";
    public const string Processing = "Processing";
    public const string Completed = "Completed";
    public const string Failed = "Failed";
}

public class FileGenerationJob
{
    public Guid Id { get; set; }

    public Guid RequestId { get; set; }

    public string SourceSystem { get; set; } = string.Empty;

    public string SourceRecordId { get; set; } = string.Empty;

    public string FileType { get; set; } = string.Empty;

    public string? InputFilePath { get; set; }

    public string? OutputFilePath { get; set; }

    public string Status { get; set; } = FileGenerationJobStatuses.Pending;

    public string? WorkerId { get; set; }

    public int RetryCount { get; set; }

    public string? ErrorMessage { get; set; }

    public DateTime CreateTime { get; set; }

    public DateTime? StartTime { get; set; }

    public DateTime? CompleteTime { get; set; }

    public DateTime? UpdateTime { get; set; }
}

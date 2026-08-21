namespace TEL.IntegrationHub.Models;

public class ExportRequestDto
{
    public Guid RequestId { get; set; }

    public string FileName { get; set; } = string.Empty;

    public string SourceRecordId { get; set; } = string.Empty;

    public string? StoredPath { get; set; }
}

public class FileJobCompletedDto
{
    public Guid RequestId { get; set; }

    /// <summary>Folder path containing generated Excel/PDF (same host as ICP).</summary>
    public string? OutputFilePath { get; set; }
}

public class FileJobFailedDto
{
    public Guid RequestId { get; set; }

    public string Error { get; set; } = string.Empty;
}

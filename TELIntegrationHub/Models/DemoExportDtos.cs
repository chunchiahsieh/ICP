namespace TEL.IntegrationHub.Models;

public class DemoExportRequestDto
{
    public Guid RequestId { get; set; }

    public string FileName { get; set; } = string.Empty;

    public string SourceRecordId { get; set; } = string.Empty;

    public string? StoredPath { get; set; }
}

public class DemoFileJobCompletedDto
{
    public Guid RequestId { get; set; }
}

public class DemoFileJobFailedDto
{
    public Guid RequestId { get; set; }

    public string Error { get; set; } = string.Empty;
}

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ICP.Models.Icp;

[Table("EXPORT_REQUEST")]
public class ExportRequest
{
    [Key]
    public Guid Id { get; set; }

    [MaxLength(260)]
    public string FileName { get; set; } = string.Empty;

    [MaxLength(1024)]
    public string StoredPath { get; set; } = string.Empty;

    /// <summary>Generated output folder path (Excel + PDFs), shared host with FileGenerator.</summary>
    [MaxLength(1024)]
    public string? OutputFilePath { get; set; }

    [MaxLength(32)]
    public string Status { get; set; } = ExportRequestStatuses.Pending;

    [MaxLength(2000)]
    public string? ErrorMessage { get; set; }

    public DateTime CreateTime { get; set; }

    public DateTime? UpdateTime { get; set; }
}

public static class ExportRequestStatuses
{
    public const string Pending = "Pending";
    public const string Processing = "Processing";
    public const string Completed = "Completed";
    public const string Failed = "Failed";
}

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ICP.Models.Icp;

[Table("Attachments")]
public class Attachment : IcpAuditableEntity
{
    [MaxLength(50)] public string AttachmentType { get; set; } = string.Empty;
    [MaxLength(100)] public string AttachmentOwnerId { get; set; } = string.Empty;
    [MaxLength(255)] public string OriginalFileName { get; set; } = string.Empty;
    [MaxLength(255)] public string StoredFileName { get; set; } = string.Empty;
    [MaxLength(500)] public string RelativePath { get; set; } = string.Empty;
    public long FileSize { get; set; }
    [MaxLength(100)] public string? ContentType { get; set; }
    public bool IsDeleted { get; set; }
}

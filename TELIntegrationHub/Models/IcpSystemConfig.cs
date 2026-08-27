using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TEL.IntegrationHub.Models;

[Table("SystemConfigs")]
public sealed class IcpSystemConfig
{
    [Key]
    public int Id { get; set; }
    public string Category { get; set; } = string.Empty;
    public string Key1 { get; set; } = string.Empty;
    public string? Value4 { get; set; }
    public bool IsDeleted { get; set; }
}

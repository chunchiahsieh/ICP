using System.ComponentModel.DataAnnotations;

namespace ICP.Models.Icp;

public sealed class NotificationMailDispatch
{
    [Key]
    [MaxLength(200)]
    public string EventKey { get; set; } = string.Empty;
    [MaxLength(20)]
    public string State { get; set; } = "Sending";
    public DateTime CreatedUtc { get; set; }
    public DateTime UpdatedUtc { get; set; }
}

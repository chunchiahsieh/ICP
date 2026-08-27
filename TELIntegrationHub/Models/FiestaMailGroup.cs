using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TEL.IntegrationHub.Models;

[Table("MailGroup")]
public sealed class FiestaMailGroup
{
    [Key]
    [Column("UID")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Uid { get; set; }

    [Column("Address")]
    public string? Address { get; set; }

    [Column("EmpID")]
    public string? EmpId { get; set; }
}

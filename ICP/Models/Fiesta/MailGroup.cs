using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ICP.Models.Fiesta;

[Table("MailGroup")]
public class MailGroup
{
    [Key]
    [Column("UID")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Uid { get; set; }

    [Column("Name")]
    [MaxLength(255)]
    public string? Name { get; set; }

    [Column("Address")]
    [MaxLength(255)]
    public string? Address { get; set; }

    [Column("EmpID")]
    [MaxLength(50)]
    public string? EmpId { get; set; }
}

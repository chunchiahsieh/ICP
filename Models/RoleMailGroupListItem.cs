namespace ICP.Models;

public class RoleMailGroupListItem
{
    public Guid Id { get; set; }

    public string MailGroupAddress { get; set; } = string.Empty;

    public string? MailGroupName { get; set; }

    public string RoleCode { get; set; } = string.Empty;

    public string RoleName { get; set; } = string.Empty;

    public DateTime CreateTime { get; set; }

    public string? CreateUser { get; set; }
}

namespace ICP.Models;

public class RoleMailGroupsBatchCreateModel
{
    public List<Guid> RoleIds { get; set; } = [];

    public List<string> MailGroupAddresses { get; set; } = [];
}

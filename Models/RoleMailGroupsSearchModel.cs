namespace ICP.Models;

public class RoleMailGroupsSearchModel
{
    public List<string> MailGroupAddresses { get; set; } = [];

    public List<string> MailGroupNames { get; set; } = [];

    public List<string> RoleCodes { get; set; } = [];

    public List<string> RoleNames { get; set; } = [];

    public List<string> CreateTimes { get; set; } = [];

    public List<string> CreateUsers { get; set; } = [];
}

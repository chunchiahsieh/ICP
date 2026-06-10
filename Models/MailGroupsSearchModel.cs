namespace ICP.Models;

public class MailGroupsSearchModel
{
    public List<string> Uids { get; set; } = [];

    public List<string> Names { get; set; } = [];

    public List<string> Addresses { get; set; } = [];

    public List<string> EmpIds { get; set; } = [];

    public List<string> TelIds { get; set; } = [];

    public List<string> DisplayNames { get; set; } = [];

    public List<string> EmailAddresses { get; set; } = [];
}

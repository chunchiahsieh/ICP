namespace ICP.Models;

public class RolesSearchModel
{
    public List<string> RoleCodes { get; set; } = [];

    public List<string> RoleNames { get; set; } = [];

    public List<string> IsEnableds { get; set; } = [];

    public List<string> Descriptions { get; set; } = [];

    public List<string> CreateTimes { get; set; } = [];

    public List<string> CreateUsers { get; set; } = [];

    public List<string> UpdateTimes { get; set; } = [];

    public List<string> UpdateUsers { get; set; } = [];
}

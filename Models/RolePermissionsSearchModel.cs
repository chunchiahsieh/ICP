namespace ICP.Models;

public class RolePermissionsSearchModel
{
    public List<string> RoleCodes { get; set; } = [];

    public List<string> RoleNames { get; set; } = [];

    public List<string> ResourceCodes { get; set; } = [];

    public List<string> ResourceNames { get; set; } = [];

    public List<string> ResourceTypes { get; set; } = [];

    public List<string> ActionCodes { get; set; } = [];
}

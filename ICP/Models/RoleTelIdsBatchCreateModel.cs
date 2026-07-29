namespace ICP.Models;

public class RoleTelIdsBatchCreateModel
{
    public List<Guid> RoleIds { get; set; } = [];

    public List<string> TelIds { get; set; } = [];
}

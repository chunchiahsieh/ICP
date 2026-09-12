namespace ICP.Models;

public class RolePermissionBatchCreateModel
{
    public List<Guid> RoleIds { get; set; } = [];

    public List<Guid> ResourceIds { get; set; } = [];

    public DataScopeDefinition? DataScope { get; set; }
}

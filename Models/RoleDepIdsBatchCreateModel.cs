namespace ICP.Models;

public class RoleDepIdsBatchCreateModel
{
    public List<Guid> RoleIds { get; set; } = [];

    public List<string> DepIds { get; set; } = [];
}

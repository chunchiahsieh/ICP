namespace ICP.Models;

public class UserPermissionsResponse
{
    public int KeyId { get; set; }

    public string? DepName { get; set; }

    public string? UserName { get; set; }

    public string? TelId { get; set; }

    public string? EmailAddress { get; set; }

    public string? DisplayName { get; set; }

    public string? DepId { get; set; }

    public string? DepName2 { get; set; }

    public string CreateDate { get; set; } = string.Empty;

    public UserRoleAssignmentsDetail RoleAssignments { get; set; } = new();

    public List<UserResourceItem> Resources { get; set; } = [];
}

public class UserRoleAssignmentsDetail
{
    public List<UserRoleTelIdPermissionItem> RoleTelIds { get; set; } = [];

    public List<UserRoleDepIdPermissionItem> RoleDepIds { get; set; } = [];

    public List<UserRoleMailGroupPermissionItem> RoleMailGroups { get; set; } = [];
}

public class UserResourceItem
{
    public Guid ResourceId { get; set; }

    public string ResourceCode { get; set; } = string.Empty;

    public string ResourceName { get; set; } = string.Empty;

    public string ResourceType { get; set; } = string.Empty;

    public string SystemCode { get; set; } = string.Empty;

    public string ModuleCode { get; set; } = string.Empty;

    public string? Route { get; set; }

    public string ActionCode { get; set; } = string.Empty;

    public bool IsAllowed { get; set; }
}

public class UserRoleTelIdPermissionItem
{
    public Guid Id { get; set; }

    public string TelId { get; set; } = string.Empty;

    public string RoleCode { get; set; } = string.Empty;

    public string RoleName { get; set; } = string.Empty;

    public bool IsEnabled { get; set; }

    public string? Description { get; set; }

    public DateTime CreateTime { get; set; }

    public string? CreateUser { get; set; }
}

public class UserRoleDepIdPermissionItem
{
    public Guid Id { get; set; }

    public string DepId { get; set; } = string.Empty;

    public string RoleCode { get; set; } = string.Empty;

    public string RoleName { get; set; } = string.Empty;

    public bool IsEnabled { get; set; }

    public string? Description { get; set; }

    public DateTime CreateTime { get; set; }

    public string? CreateUser { get; set; }
}

public class UserRoleMailGroupPermissionItem
{
    public Guid Id { get; set; }

    public string Address { get; set; } = string.Empty;

    public string? MailGroupName { get; set; }

    public string RoleCode { get; set; } = string.Empty;

    public string RoleName { get; set; } = string.Empty;

    public bool IsEnabled { get; set; }

    public string? Description { get; set; }

    public DateTime CreateTime { get; set; }

    public string? CreateUser { get; set; }
}

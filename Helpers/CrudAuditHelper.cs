namespace ICP.Helpers;

public static class CrudAuditHelper
{
    public static string ResolveUserName(string? identityName)
    {
        return string.IsNullOrWhiteSpace(identityName) ? "System" : identityName;
    }

    public static void ApplyCreateAudit(dynamic entity, string? identityName)
    {
        var user = ResolveUserName(identityName);
        entity.CreateTime = DateTime.Now;
        entity.CreateUser = user;
    }

    public static void ApplyUpdateAudit(dynamic entity, string? identityName)
    {
        var user = ResolveUserName(identityName);
        entity.UpdateTime = DateTime.Now;
        entity.UpdateUser = user;
    }

    public static string? MapDbUpdateException(Exception ex)
    {
        var message = ex.InnerException?.Message ?? ex.Message;

        if (message.Contains("IX_Roles_RoleCode", StringComparison.OrdinalIgnoreCase))
        {
            return "RoleCode 已存在";
        }

        if (message.Contains("IX_RolesTELID_TELID_RoleId", StringComparison.OrdinalIgnoreCase))
        {
            return "此 TELID 與角色組合已存在";
        }

        if (message.Contains("IX_RolesDepID_DepID_RoleId", StringComparison.OrdinalIgnoreCase))
        {
            return "此 DepID 與角色組合已存在";
        }

        if (message.Contains("IX_RolePermissions", StringComparison.OrdinalIgnoreCase))
        {
            return "此角色、資源與動作組合已存在";
        }

        return null;
    }
}

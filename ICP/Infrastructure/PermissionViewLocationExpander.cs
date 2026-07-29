using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Razor;

namespace ICP.Infrastructure;

/// <summary>將 Permission 模組 Controller 的 View 解析至 Views/Permission/{Controller}/。</summary>
public class PermissionViewLocationExpander : IViewLocationExpander
{
    private const string PermissionModuleKey = "permission-module";

    public void PopulateValues(ViewLocationExpanderContext context)
    {
        if (context.ActionContext.ActionDescriptor is ControllerActionDescriptor descriptor &&
            descriptor.ControllerTypeInfo.IsDefined(typeof(PermissionModuleAttribute), inherit: true))
        {
            context.Values[PermissionModuleKey] = "true";
        }
    }

    public IEnumerable<string> ExpandViewLocations(
        ViewLocationExpanderContext context,
        IEnumerable<string> viewLocations)
    {
        if (context.Values.ContainsKey(PermissionModuleKey))
        {
            var viewFolder = ResolvePermissionViewFolder(context.ControllerName);
            yield return $"/Views/Permission/{viewFolder}/{{0}}.cshtml";
        }

        foreach (var location in viewLocations)
        {
            yield return location;
        }
    }

    private static string ResolvePermissionViewFolder(string? controllerName)
    {
        if (controllerName?.Equals("Resources", StringComparison.OrdinalIgnoreCase) == true)
        {
            return "RoleResources";
        }

        return controllerName ?? string.Empty;
    }
}

using ICP.Models;
using ICP.Services;
using Microsoft.AspNetCore.Routing;

namespace ICP.Helpers;

public static class PermissionRequestResolver
{
    public static string? Resolve(
        ResourceRouteRegistryService registry,
        string? controller,
        string? action,
        string httpMethod,
        RouteValueDictionary routeValues,
        IDictionary<string, object?> actionArguments)
    {
        if (string.IsNullOrWhiteSpace(controller) || string.IsNullOrWhiteSpace(action))
        {
            return null;
        }

        if (controller.Equals("Admin", StringComparison.OrdinalIgnoreCase)
            && action.Equals("PermissionScan", StringComparison.OrdinalIgnoreCase))
        {
            return ResolveIfRegistered(registry, "Views.Permission.RoleResources.Scan");
        }

        if (controller.Equals("Users", StringComparison.OrdinalIgnoreCase)
            && action.Equals("GetPermissions", StringComparison.OrdinalIgnoreCase))
        {
            return ResolveIfRegistered(registry, "Views.Permission.Users.View");
        }

        if (action.Equals("Index", StringComparison.OrdinalIgnoreCase)
            && httpMethod.Equals("GET", StringComparison.OrdinalIgnoreCase))
        {
            if (SettingCategories.IsKnown(controller))
            {
                return ResolveIfRegistered(registry, $"Views.Shared._SidebarNav.Setting.{controller}");
            }

            var route = PermissionRouteNormalizer.ControllerToRoute(controller);
            return registry.FindPageResourceCodeByRoute(route);
        }

        var suffix = ResolveActionSuffix(action, actionArguments);
        if (suffix is null)
        {
            return null;
        }

        if (SettingCategories.IsKnown(controller))
        {
            return ResolveIfRegistered(registry, $"Views.Setting.{controller}.{suffix}");
        }

        var module = ResolveModuleName(controller);
        return ResolveIfRegistered(registry, $"Views.Permission.{module}.{suffix}");
    }

    private static string? ResolveActionSuffix(string action, IDictionary<string, object?> actionArguments)
    {
        if (action.Equals("Save", StringComparison.OrdinalIgnoreCase))
        {
            if (actionArguments.TryGetValue("model", out var modelObj)
                && modelObj is RoleEditModel model
                && model.Id.HasValue
                && model.Id.Value != Guid.Empty)
            {
                return "Edit";
            }

            return "Create";
        }

        if (action.Equals("BatchCreate", StringComparison.OrdinalIgnoreCase))
        {
            return "Create";
        }

        if (action.Equals("BatchDelete", StringComparison.OrdinalIgnoreCase))
        {
            return "Delete";
        }

        if (action.Equals("Disable", StringComparison.OrdinalIgnoreCase)
            || action.Equals("BatchDisable", StringComparison.OrdinalIgnoreCase))
        {
            return "Disable";
        }

        if (IsReadAction(action))
        {
            return "View";
        }

        return null;
    }

    private static bool IsReadAction(string action)
    {
        if (action.Equals("Lookup", StringComparison.OrdinalIgnoreCase)
            || action.Equals("Get", StringComparison.OrdinalIgnoreCase)
            || action.Equals("Query", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (action.StartsWith("Query", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (action.StartsWith("GetFilterOptions", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }

    private static string ResolveModuleName(string controller)
    {
        return controller.Equals("Resources", StringComparison.OrdinalIgnoreCase)
            ? "RoleResources"
            : controller;
    }

    private static string? ResolveIfRegistered(ResourceRouteRegistryService registry, string resourceCode)
    {
        return registry.IsRegisteredResourceCode(resourceCode) ? resourceCode : null;
    }
}

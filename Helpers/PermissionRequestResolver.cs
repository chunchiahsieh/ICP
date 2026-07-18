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

        if (controller.Equals("TariffData", StringComparison.OrdinalIgnoreCase))
        {
            if (action.Equals("UploadCustomsData", StringComparison.OrdinalIgnoreCase)
                && httpMethod.Equals("POST", StringComparison.OrdinalIgnoreCase))
            {
                return ResolveIfRegistered(registry, "Views.Broker.TariffData.UploadCustomsData");
            }

            if (action.Equals("UploadDeclarationPdf", StringComparison.OrdinalIgnoreCase)
                && httpMethod.Equals("POST", StringComparison.OrdinalIgnoreCase))
            {
                return ResolveIfRegistered(registry, "Views.Broker.TariffData.UploadDeclarationPdf");
            }

            if (action.Equals("UploadCost", StringComparison.OrdinalIgnoreCase)
                && httpMethod.Equals("POST", StringComparison.OrdinalIgnoreCase))
            {
                return ResolveIfRegistered(registry, "Views.Broker.TariffData.UploadCost");
            }

            if (action.Equals("DownloadTemplate", StringComparison.OrdinalIgnoreCase)
                && httpMethod.Equals("GET", StringComparison.OrdinalIgnoreCase))
            {
                return ResolveIfRegistered(registry, "Views.Broker.TariffData.UploadCustomsData");
            }

            if (action.Equals("DownloadAttachment", StringComparison.OrdinalIgnoreCase)
                && httpMethod.Equals("GET", StringComparison.OrdinalIgnoreCase))
            {
                return ResolveIfRegistered(registry, "Views.Broker.TariffData.View");
            }

            if (IsReadAction(action))
            {
                return ResolveIfRegistered(registry, "Views.Broker.TariffData.View");
            }

            if (action.Equals("Index", StringComparison.OrdinalIgnoreCase)
                && httpMethod.Equals("GET", StringComparison.OrdinalIgnoreCase))
            {
                return ResolveIfRegistered(registry, "Views.Broker.TariffData.View");
            }
        }

        if (controller.Equals("ForwarderDataUpload", StringComparison.OrdinalIgnoreCase))
        {
            if ((action.Equals("Upload", StringComparison.OrdinalIgnoreCase)
                    || action.Equals("Save", StringComparison.OrdinalIgnoreCase)
                    || action.Equals("CancelPending", StringComparison.OrdinalIgnoreCase)
                    || action.Equals("Query", StringComparison.OrdinalIgnoreCase))
                && httpMethod.Equals("POST", StringComparison.OrdinalIgnoreCase))
            {
                return ResolveIfRegistered(registry, "Views.Forwarder.ForwarderDataUpload.Upload");
            }

            if ((action.Equals("DownloadTemplate", StringComparison.OrdinalIgnoreCase)
                    || action.Equals("GetFilterOptions", StringComparison.OrdinalIgnoreCase))
                && httpMethod.Equals("GET", StringComparison.OrdinalIgnoreCase))
            {
                return ResolveIfRegistered(registry, "Views.Forwarder.ForwarderDataUpload.Upload");
            }

            if (IsReadAction(action))
            {
                return ResolveIfRegistered(registry, "Views.Forwarder.ForwarderDataUpload.View");
            }
        }

        if (controller.Equals("AddDiSa", StringComparison.OrdinalIgnoreCase))
        {
            if ((action.Equals("Upload", StringComparison.OrdinalIgnoreCase)
                    || action.Equals("Save", StringComparison.OrdinalIgnoreCase)
                    || action.Equals("CancelPending", StringComparison.OrdinalIgnoreCase)
                    || action.Equals("Query", StringComparison.OrdinalIgnoreCase))
                && httpMethod.Equals("POST", StringComparison.OrdinalIgnoreCase))
            {
                return ResolveIfRegistered(registry, "Views.Function.AddDiSa.Upload");
            }

            if (action.Equals("DownloadTemplate", StringComparison.OrdinalIgnoreCase)
                && httpMethod.Equals("GET", StringComparison.OrdinalIgnoreCase))
            {
                return ResolveIfRegistered(registry, "Views.Function.AddDiSa.Upload");
            }

            if (IsReadAction(action))
            {
                return ResolveIfRegistered(registry, "Views.Function.AddDiSa.View")
                    ?? ResolveIfRegistered(registry, "Views.Shared._SidebarNav.Function.AddDiSa");
            }

            if (action.Equals("Index", StringComparison.OrdinalIgnoreCase)
                && httpMethod.Equals("GET", StringComparison.OrdinalIgnoreCase))
            {
                return ResolveIfRegistered(registry, "Views.Shared._SidebarNav.Function.AddDiSa");
            }
        }

        if (controller.Equals("MassUpdateNcpi", StringComparison.OrdinalIgnoreCase))
        {
            if ((action.Equals("Upload", StringComparison.OrdinalIgnoreCase)
                    || action.Equals("Query", StringComparison.OrdinalIgnoreCase)
                    || action.Equals("Save", StringComparison.OrdinalIgnoreCase)
                    || action.Equals("CancelPending", StringComparison.OrdinalIgnoreCase))
                && httpMethod.Equals("POST", StringComparison.OrdinalIgnoreCase))
            {
                return ResolveIfRegistered(registry, "Views.Function.MassUpdateNcpi.Upload");
            }

            if (action.Equals("DownloadSample", StringComparison.OrdinalIgnoreCase)
                && httpMethod.Equals("GET", StringComparison.OrdinalIgnoreCase))
            {
                return ResolveIfRegistered(registry, "Views.Function.MassUpdateNcpi.Upload");
            }

            if (action.Equals("Index", StringComparison.OrdinalIgnoreCase)
                && httpMethod.Equals("GET", StringComparison.OrdinalIgnoreCase))
            {
                return ResolveIfRegistered(registry, "Views.Shared._SidebarNav.Function.MassUpdateNcpi");
            }
        }

        if (controller.Equals("MassUpdateNonNcpi", StringComparison.OrdinalIgnoreCase))
        {
            if ((action.Equals("Upload", StringComparison.OrdinalIgnoreCase)
                    || action.Equals("Query", StringComparison.OrdinalIgnoreCase)
                    || action.Equals("Save", StringComparison.OrdinalIgnoreCase)
                    || action.Equals("CancelPending", StringComparison.OrdinalIgnoreCase))
                && httpMethod.Equals("POST", StringComparison.OrdinalIgnoreCase))
            {
                return ResolveIfRegistered(registry, "Views.Function.MassUpdateNonNcpi.Upload");
            }

            if (action.Equals("DownloadSample", StringComparison.OrdinalIgnoreCase)
                && httpMethod.Equals("GET", StringComparison.OrdinalIgnoreCase))
            {
                return ResolveIfRegistered(registry, "Views.Function.MassUpdateNonNcpi.Upload");
            }

            if (action.Equals("Index", StringComparison.OrdinalIgnoreCase)
                && httpMethod.Equals("GET", StringComparison.OrdinalIgnoreCase))
            {
                return ResolveIfRegistered(registry, "Views.Shared._SidebarNav.Function.MassUpdateNonNcpi");
            }
        }

        if (controller.Equals("ShippingReport", StringComparison.OrdinalIgnoreCase)
            || controller.Equals("CompareIcpVsArUr", StringComparison.OrdinalIgnoreCase)
            || controller.Equals("MassDataReport", StringComparison.OrdinalIgnoreCase))
        {
            if (action.Equals("Index", StringComparison.OrdinalIgnoreCase)
                && httpMethod.Equals("GET", StringComparison.OrdinalIgnoreCase))
            {
                return ResolveIfRegistered(registry, $"Views.Shared._SidebarNav.Report.{controller}");
            }

            if (IsReadAction(action)
                || action.Equals("GetPageConfig", StringComparison.OrdinalIgnoreCase)
                || action.Equals("DownloadExcel", StringComparison.OrdinalIgnoreCase))
            {
                return ResolveIfRegistered(registry, $"Views.Shared._SidebarNav.Report.{controller}");
            }
        }

        if (action.Equals("Index", StringComparison.OrdinalIgnoreCase)
            && httpMethod.Equals("GET", StringComparison.OrdinalIgnoreCase))
        {
            if (SettingCategories.IsKnown(controller))
            {
                return ResolveIfRegistered(registry, $"Views.Shared._SidebarNav.Setting.{controller}");
            }

            if (FunctionCategories.IsKnown(controller))
            {
                return ResolveIfRegistered(registry, $"Views.Shared._SidebarNav.Function.{controller}");
            }

            if (ReportCategories.IsKnown(controller))
            {
                return ResolveIfRegistered(registry, $"Views.Shared._SidebarNav.Report.{controller}");
            }

            if (BrokerCategories.IsKnown(controller))
            {
                return ResolveIfRegistered(registry, $"Views.Shared._SidebarNav.Broker.{controller}");
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

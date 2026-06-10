using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Razor;

namespace ICP.Infrastructure;

public class SettingViewLocationExpander : IViewLocationExpander
{
    private const string SettingModuleKey = "setting-module";

    public void PopulateValues(ViewLocationExpanderContext context)
    {
        if (context.ActionContext.ActionDescriptor is ControllerActionDescriptor descriptor &&
            descriptor.ControllerTypeInfo.IsDefined(typeof(SettingModuleAttribute), inherit: true))
        {
            context.Values[SettingModuleKey] = "true";
        }
    }

    public IEnumerable<string> ExpandViewLocations(
        ViewLocationExpanderContext context,
        IEnumerable<string> viewLocations)
    {
        if (context.Values.ContainsKey(SettingModuleKey))
        {
            yield return $"/Views/Setting/{context.ControllerName}/{{0}}.cshtml";
            yield return "/Views/Setting/Shared/{0}.cshtml";
        }

        foreach (var location in viewLocations)
        {
            yield return location;
        }
    }
}

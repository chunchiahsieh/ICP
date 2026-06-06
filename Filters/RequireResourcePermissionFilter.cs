using ICP.Helpers;
using ICP.Infrastructure;
using ICP.Models.Auth;
using ICP.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;

namespace ICP.Filters;

public class RequireResourcePermissionFilter : IAsyncActionFilter
{
    private readonly UserResourcePermissionService _userResourcePermissionService;
    private readonly ResourceRouteRegistryService _routeRegistry;
    private readonly AppAuthOptions _authOptions;
    private readonly IStringLocalizer<SharedResource> _localizer;

    public RequireResourcePermissionFilter(
        UserResourcePermissionService userResourcePermissionService,
        ResourceRouteRegistryService routeRegistry,
        IOptions<AppAuthOptions> authOptions,
        IStringLocalizer<SharedResource> localizer)
    {
        _userResourcePermissionService = userResourcePermissionService;
        _routeRegistry = routeRegistry;
        _authOptions = authOptions.Value;
        _localizer = localizer;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (_authOptions.IsSuperUserEnabled)
        {
            await next();
            return;
        }

        if (ShouldSkip(context))
        {
            await next();
            return;
        }

        var routeValues = context.RouteData.Values;
        var controller = routeValues["controller"]?.ToString();
        var action = routeValues["action"]?.ToString();
        var httpMethod = context.HttpContext.Request.Method;

        var requiredResourceCode = PermissionRequestResolver.Resolve(
            _routeRegistry,
            controller,
            action,
            httpMethod,
            routeValues,
            context.ActionArguments);

        if (requiredResourceCode is null)
        {
            await next();
            return;
        }

        if (!_userResourcePermissionService.HasPermission(requiredResourceCode))
        {
            context.Result = CreateDeniedResult(context, requiredResourceCode);
            return;
        }

        await next();
    }

    private static bool ShouldSkip(ActionExecutingContext context)
    {
        return HasAllowAnonymous(context) || HasSkipResourcePermission(context);
    }

    private IActionResult CreateDeniedResult(ActionExecutingContext context, string resourceCode)
    {
        var message = _localizer["Permission.AccessDenied"].Value;

        if (WantsJsonResponse(context.HttpContext.Request))
        {
            return new JsonResult(new { success = false, message, resourceCode })
            {
                StatusCode = StatusCodes.Status403Forbidden
            };
        }

        return new RedirectToActionResult("Index", "ErrorPage", null);
    }

    private static bool WantsJsonResponse(HttpRequest request)
    {
        if (request.Headers.TryGetValue("X-Requested-With", out var requestedWith)
            && requestedWith.ToString().Contains("XMLHttpRequest", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (request.Headers.Accept.Any(value =>
                value?.Contains("application/json", StringComparison.OrdinalIgnoreCase) == true))
        {
            return true;
        }

        return request.Path.StartsWithSegments("/Admin", StringComparison.OrdinalIgnoreCase)
            || request.ContentType?.Contains("application/json", StringComparison.OrdinalIgnoreCase) == true;
    }

    private static bool HasAllowAnonymous(ActionExecutingContext context)
    {
        return context.ActionDescriptor.EndpointMetadata.Any(m => m is IAllowAnonymous);
    }

    private static bool HasSkipResourcePermission(ActionExecutingContext context)
    {
        return context.ActionDescriptor.EndpointMetadata.Any(m => m is SkipResourcePermissionAttribute);
    }
}

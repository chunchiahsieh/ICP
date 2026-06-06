using ICP.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Localization;

namespace ICP.Filters;

public class RequireLoginFilter : IAsyncActionFilter
{
    private readonly UserAuthService _userAuthService;
    private readonly UserResourcePermissionService _userResourcePermissionService;
    private readonly IStringLocalizer<SharedResource> _localizer;

    public RequireLoginFilter(
        UserAuthService userAuthService,
        UserResourcePermissionService userResourcePermissionService,
        IStringLocalizer<SharedResource> localizer)
    {
        _userAuthService = userAuthService;
        _userResourcePermissionService = userResourcePermissionService;
        _localizer = localizer;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (HasAllowAnonymous(context))
        {
            await next();
            return;
        }

        if (_userAuthService.IsAuthenticated)
        {
            var user = _userAuthService.GetSessionUserInfo();
            if (user is not null)
            {
                await _userResourcePermissionService.EnsureSessionResourcesAsync(
                    user,
                    context.HttpContext.RequestAborted);
            }

            await next();
            return;
        }

        if (context.Controller is Controller controller)
        {
            controller.TempData["ReturnMsg"] = _localizer["Auth.PleaseLoginFirst"].Value;
        }

        context.Result = new RedirectToActionResult("Index", "Login", null);
    }

    private static bool HasAllowAnonymous(ActionExecutingContext context)
    {
        var endpoint = context.ActionDescriptor.EndpointMetadata;
        return endpoint.Any(m => m is IAllowAnonymous);
    }
}

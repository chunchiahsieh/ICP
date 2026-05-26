using ICP.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Localization;

namespace ICP.Filters;

public class RequireLoginFilter : IAsyncActionFilter
{
    private readonly LoginSessionService _loginSessionService;
    private readonly IStringLocalizer<SharedResource> _localizer;

    public RequireLoginFilter(
        LoginSessionService loginSessionService,
        IStringLocalizer<SharedResource> localizer)
    {
        _loginSessionService = loginSessionService;
        _localizer = localizer;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (HasAllowAnonymous(context))
        {
            await next();
            return;
        }

        if (_loginSessionService.IsAuthenticated)
        {
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

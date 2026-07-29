using ICP.Models;
using ICP.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace ICP.Filters;

public sealed class ShipInfoApiExceptionFilter : IExceptionFilter
{
    private readonly ILogger<ShipInfoApiExceptionFilter> _logger;

    public ShipInfoApiExceptionFilter(ILogger<ShipInfoApiExceptionFilter> logger)
    {
        _logger = logger;
    }

    public void OnException(ExceptionContext context)
    {
        var controllerName = context.RouteData.Values["controller"]?.ToString();
        if (!string.Equals(controllerName, "ShipInfo", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var actionName = context.RouteData.Values["action"]?.ToString();
        if (string.Equals(context.HttpContext.Request.Method, "GET", StringComparison.OrdinalIgnoreCase)
            && string.Equals(actionName, "Index", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var (statusCode, message) = MapException(context.Exception);
        if (statusCode == StatusCodes.Status500InternalServerError)
        {
            _logger.LogError(context.Exception, "ShipInfo API error.");
        }
        else
        {
            _logger.LogWarning(context.Exception, "ShipInfo API rejected: {Message}", message);
        }

        context.Result = new ObjectResult(ApiResponse<object>.Fail(message))
        {
            StatusCode = statusCode
        };
        context.ExceptionHandled = true;
    }

    private static (int StatusCode, string Message) MapException(Exception exception) =>
        exception switch
        {
            ShipInfoForbiddenException ex => (StatusCodes.Status403Forbidden, ex.Message),
            ShipInfoNotFoundException ex => (StatusCodes.Status404NotFound, ex.Message),
            ShipInfoConcurrencyException ex => (StatusCodes.Status409Conflict, ex.Message),
            ShipInfoBusinessException ex => (StatusCodes.Status400BadRequest, ex.Message),
            _ => (StatusCodes.Status500InternalServerError, "System error.")
        };
}

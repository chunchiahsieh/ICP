using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace ICP.Helpers;

public static class CrudJsonHelper
{
    public static IActionResult ValidationErrors(ModelStateDictionary modelState)
    {
        var errors = modelState
            .Where(x => x.Value?.Errors.Count > 0)
            .ToDictionary(
                x => x.Key,
                x => x.Value!.Errors.Select(e => e.ErrorMessage).ToArray());

        return new JsonResult(new { success = false, errors });
    }

    public static IActionResult Success()
    {
        return new JsonResult(new { success = true });
    }

    public static IActionResult Failure(string message)
    {
        return new JsonResult(new { success = false, message });
    }
}

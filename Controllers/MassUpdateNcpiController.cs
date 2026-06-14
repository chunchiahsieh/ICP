using ICP;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;

namespace ICP.Controllers;

public class MassUpdateNcpiController : Controller
{
    private const int MaxSizeMb = 50;

    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".xlsx",
        ".xls",
        ".csv"
    };

    private readonly IStringLocalizer<SharedResource> _localizer;

    public MassUpdateNcpiController(IStringLocalizer<SharedResource> localizer)
    {
        _localizer = localizer;
    }

    [HttpGet]
    public IActionResult Index()
    {
        ViewData["MaxSizeMb"] = MaxSizeMb;
        return View("~/Views/FUNCTION/MassUpdateNcpi/View.cshtml");
    }

    [HttpPost]
    [RequestSizeLimit(52_428_800)]
    public IActionResult Upload(IFormFile? file)
    {
        if (file is null || file.Length == 0)
        {
            return Json(new { success = false, message = _localizer["Function.AddDiSa.NoFileSelected"].Value });
        }

        if (file.Length > MaxSizeMb * 1024L * 1024L)
        {
            return Json(new
            {
                success = false,
                message = string.Format(_localizer["Function.AddDiSa.MaxSizeExceeded"].Value, MaxSizeMb)
            });
        }

        var extension = Path.GetExtension(file.FileName);
        if (string.IsNullOrWhiteSpace(extension) || !AllowedExtensions.Contains(extension))
        {
            return Json(new { success = false, message = _localizer["Function.AddDiSa.InvalidFileType"].Value });
        }

        return Json(new
        {
            success = false,
            message = _localizer["Function.Placeholder"].Value
        });
    }
}

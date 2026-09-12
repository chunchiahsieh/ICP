using ICP.Infrastructure;
using ICP.Models.LocalizationManagement;
using ICP.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace ICP.Controllers;

/// <summary>Direct editor for ICP SharedResource resource files. This page intentionally has no role permission.</summary>
[SkipResourcePermission]
public class LocalizationManagementController : Controller
{
    private readonly LocalizationResourceManagementService _resourceService;
    private readonly IOptionsMonitor<LocalizationManagementOptions> _options;

    public LocalizationManagementController(
        LocalizationResourceManagementService resourceService,
        IOptionsMonitor<LocalizationManagementOptions> options)
    {
        _resourceService = resourceService;
        _options = options;
    }

    [HttpGet]
    public IActionResult Index()
    {
        return IsEnabled() ? View() : NotFound();
    }

    [HttpGet]
    public async Task<IActionResult> Rows(CancellationToken cancellationToken = default)
    {
        if (!IsEnabled())
        {
            return NotFound();
        }

        return Json(await _resourceService.GetRowsAsync(cancellationToken));
    }

    [HttpGet]
    public async Task<IActionResult> Export(CancellationToken cancellationToken = default)
    {
        if (!IsEnabled())
        {
            return NotFound();
        }

        var bytes = await _resourceService.ExportExcelAsync(cancellationToken);
        return File(bytes,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"ICP-Localization-{DateTime.Now:yyyyMMdd}.xlsx");
    }

    [HttpPost]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> Import(IFormFile? file, CancellationToken cancellationToken = default)
    {
        if (!IsEnabled())
        {
            return NotFound();
        }

        if (file is null || file.Length == 0)
        {
            return BadRequest(new { success = false, message = "Select an Excel file to import." });
        }

        if (file.Length > 10 * 1024 * 1024)
        {
            return BadRequest(new { success = false, message = "The localization Excel file must be 10 MB or smaller." });
        }

        try
        {
            await using var stream = file.OpenReadStream();
            var result = await _resourceService.ImportExcelAsync(stream, cancellationToken);
            return Json(new { success = true, importedCount = result.ImportedCount, changedCount = result.ChangedCount });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
        catch (InvalidDataException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    [HttpPost]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> Save(
        [FromBody] LocalizationResourceSaveRequest? request,
        CancellationToken cancellationToken = default)
    {
        if (!IsEnabled())
        {
            return NotFound();
        }

        if (request is null)
        {
            return BadRequest(new { success = false, message = "Invalid localization update request." });
        }

        try
        {
            var changedCount = await _resourceService.SaveAsync(request.Rows, cancellationToken);
            return Json(new { success = true, changedCount });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
        catch (IOException)
        {
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { success = false, message = "Unable to write localization resource files." });
        }
    }

    private bool IsEnabled() => _options.CurrentValue.Enabled;
}

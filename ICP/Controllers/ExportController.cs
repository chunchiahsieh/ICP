using ICP.Models.Icp;
using ICP.Services;
using Microsoft.AspNetCore.Mvc;

namespace ICP.Controllers;

public class ExportController : Controller
{
    private readonly IExportDemoService _exportDemoService;

    public ExportController(IExportDemoService exportDemoService)
    {
        _exportDemoService = exportDemoService;
    }

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var items = await _exportDemoService.ListAsync(cancellationToken);
        return View("~/Views/FUNCTION/Export/View.cshtml", items);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Upload(IFormFile? file, CancellationToken cancellationToken)
    {
        try
        {
            if (file is null || file.Length == 0)
            {
                TempData["ExportError"] = "Please select a file.";
                return RedirectToAction(nameof(Index));
            }

            var created = await _exportDemoService.UploadAndNotifyHubAsync(file, cancellationToken);
            TempData["ExportOk"] =
                $"Uploaded {created.FileName}. RequestId={created.Id:D}. Hub notified (Pending → Processing by Hub).";
        }
        catch (Exception ex)
        {
            TempData["ExportError"] = ex.Message;
        }

        return RedirectToAction(nameof(Index));
    }
}

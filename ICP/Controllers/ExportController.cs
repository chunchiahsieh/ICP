using ICP.Services;
using Microsoft.AspNetCore.Mvc;

namespace ICP.Controllers;

public class ExportController : Controller
{
    private readonly IExportService _exportService;

    public ExportController(IExportService exportService)
    {
        _exportService = exportService;
    }

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var items = await _exportService.ListAsync(cancellationToken);
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

            var created = await _exportService.UploadAndNotifyHubAsync(file, cancellationToken);
            TempData["ExportOk"] =
                $"Uploaded {created.FileName}. RequestId={created.Id:D}. Hub notified (Pending → Processing by Hub).";
        }
        catch (Exception ex)
        {
            TempData["ExportError"] = ex.Message;
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Files(Guid id, CancellationToken cancellationToken)
    {
        var request = await _exportService.GetAsync(id, cancellationToken);
        if (request is null)
        {
            return NotFound(new { error = "Export request not found." });
        }

        var files = await _exportService.ListOutputFilesAsync(id, cancellationToken);
        var displayPath = _exportService.GetDisplayFolderPath(id);
        return Json(new
        {
            requestId = id,
            status = request.Status,
            displayPath,
            files = files.Select(f => new
            {
                f.FileName,
                f.Extension,
                f.SizeBytes,
                downloadUrl = Url.Action(nameof(Download), new { id, file = f.FileName })
            })
        });
    }

    [HttpGet]
    public async Task<IActionResult> Download(Guid id, string file, CancellationToken cancellationToken)
    {
        var opened = await _exportService.OpenOutputFileAsync(id, file, cancellationToken);
        if (opened is null)
        {
            return NotFound();
        }

        return File(opened.Value.Stream, opened.Value.ContentType, opened.Value.DownloadName);
    }

    [HttpGet]
    public async Task<IActionResult> DownloadAll(Guid id, CancellationToken cancellationToken)
    {
        var opened = await _exportService.OpenOutputZipAsync(id, cancellationToken);
        if (opened is null)
        {
            return NotFound();
        }

        return File(opened.Value.Stream, "application/zip", opened.Value.DownloadName);
    }
}

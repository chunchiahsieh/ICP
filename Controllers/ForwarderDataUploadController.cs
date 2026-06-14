using Microsoft.AspNetCore.Mvc;

namespace ICP.Controllers;

public class ForwarderDataUploadController : Controller
{
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<ForwarderDataUploadController> _logger;

    public ForwarderDataUploadController(
        IWebHostEnvironment environment,
        ILogger<ForwarderDataUploadController> logger)
    {
        _environment = environment;
        _logger = logger;
    }

    [HttpGet]
    public IActionResult Index()
    {
        return View("~/Views/FORWARDER/ForwarderDataUpload/View.cshtml");
    }

    [HttpPost]
    [RequestSizeLimit(52_428_800)]
    public async Task<IActionResult> Upload(IFormFile? file, CancellationToken cancellationToken = default)
    {
        if (file is null || file.Length == 0)
        {
            return Json(new { success = false, message = "未選擇檔案" });
        }

        var uploadDirectory = Path.Combine(_environment.WebRootPath, "uploads", "forwarder");
        Directory.CreateDirectory(uploadDirectory);

        var safeFileName = Path.GetFileName(file.FileName);
        if (string.IsNullOrWhiteSpace(safeFileName))
        {
            return Json(new { success = false, message = "檔案名稱無效" });
        }

        var storedFileName = $"{DateTime.Now:yyyyMMddHHmmssfff}_{safeFileName}";
        var storedPath = Path.Combine(uploadDirectory, storedFileName);

        await using (var stream = System.IO.File.Create(storedPath))
        {
            await file.CopyToAsync(stream, cancellationToken);
        }

        _logger.LogInformation("Forwarder data uploaded: {FileName} -> {StoredPath}", safeFileName, storedPath);

        return Json(new { success = true, message = "上傳成功" });
    }
}

using ICP.Services;
using Microsoft.AspNetCore.Mvc;

namespace ICP.Controllers;

public class AdminController : Controller
{
    private readonly PermissionScannerService _scannerService;
    private readonly PermissionResourceSyncService _syncService;

    public AdminController(
        PermissionScannerService scannerService,
        PermissionResourceSyncService syncService)
    {
        _scannerService = scannerService;
        _syncService = syncService;
    }

    [HttpPost]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> PermissionScan(CancellationToken cancellationToken)
    {
        var scanned = _scannerService.Scan();
        var result = await _syncService.UpsertAsync(
            scanned,
            User.Identity?.Name,
            cancellationToken);

        return Json(result);
    }
}

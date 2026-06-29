using ICP.Services;
using Microsoft.AspNetCore.Mvc;

namespace ICP.Controllers;

public class AdminController : Controller
{
    private readonly PermissionScannerService _scannerService;
    private readonly PermissionResourceSyncService _syncService;
    private readonly ResourceRouteRegistryService _routeRegistry;

    public AdminController(
        PermissionScannerService scannerService,
        PermissionResourceSyncService syncService,
        ResourceRouteRegistryService routeRegistry)
    {
        _scannerService = scannerService;
        _syncService = syncService;
        _routeRegistry = routeRegistry;
    }

    [HttpPost]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> PermissionScan(CancellationToken cancellationToken)
    {
        var scanned = _scannerService.Scan();
        // UpsertAsync 會先移除掃描結果以外的 Resources，再寫入/更新掃描結果；RolePermissions 於 ResourceCode 仍有效時保留
        var result = await _syncService.UpsertAsync(
            scanned,
            User.Identity?.Name,
            cancellationToken);

        await _routeRegistry.RefreshAsync(cancellationToken);

        return Json(result);
    }
}

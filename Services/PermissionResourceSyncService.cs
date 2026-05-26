using ICP.Data;
using ICP.Models;
using ICP.Models.Icp;
using Microsoft.EntityFrameworkCore;

namespace ICP.Services;

public class PermissionResourceSyncService
{
    private readonly ApplicationDbContext _dbContext;

    public PermissionResourceSyncService(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<PermissionScanResult> UpsertAsync(
        IReadOnlyList<ScannedPermission> scannedItems,
        string? updateUser,
        CancellationToken cancellationToken = default)
    {
        var actor = string.IsNullOrWhiteSpace(updateUser) ? "PermissionScanner" : updateUser;
        var distinctItems = scannedItems
            .GroupBy(x => x.ResourceCode, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .ToList();

        var inserted = 0;
        var updated = 0;
        var resourceCodes = new List<string>();

        foreach (var item in distinctItems)
        {
            resourceCodes.Add(item.ResourceCode);

            var existing = await _dbContext.Resources
                .FirstOrDefaultAsync(r => r.ResourceCode == item.ResourceCode, cancellationToken);

            if (existing is null)
            {
                var segments = item.ResourceCode.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

                _dbContext.Resources.Add(new Resource
                {
                    Id = Guid.NewGuid(),
                    ResourceCode = item.ResourceCode,
                    SystemCode = segments.Length > 0 ? segments[0] : string.Empty,
                    ModuleCode = segments.Length > 1 ? segments[1] : string.Empty,
                    ResourceName = item.ResourceName,
                    ResourceType = item.ResourceType,
                    Route = item.Route,
                    Description = item.Description,
                    IsVisible = true,
                    IsEnabled = true,
                    CreateTime = DateTime.Now,
                    CreateUser = actor
                });
                inserted++;
                continue;
            }

            existing.ResourceName = item.ResourceName;
            existing.ResourceType = item.ResourceType;
            existing.Route = item.Route;
            existing.Description = item.Description;
            existing.UpdateTime = DateTime.Now;
            existing.UpdateUser = actor;
            updated++;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new PermissionScanResult
        {
            ScannedCount = scannedItems.Count,
            InsertedCount = inserted,
            UpdatedCount = updated,
            ResourceCodes = resourceCodes
        };
    }
}

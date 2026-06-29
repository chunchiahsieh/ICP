using ICP.Data;
using ICP.Models.ShipInfo;
using Microsoft.EntityFrameworkCore;

namespace ICP.Services;

public class ShipInfoLookupService
{
    private readonly ApplicationDbContext _db;

    public ShipInfoLookupService(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<ShipInfoLookupOption>> GetOptionsAsync(
        string category,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(category))
        {
            return [];
        }

        var normalizedCategory = category.Trim();
        if (normalizedCategory.Equals(ShipInfoStatuses.LookupCategory, StringComparison.OrdinalIgnoreCase))
        {
            return ShipInfoStatuses.LookupOptions;
        }

        var rows = await _db.SystemConfigs
            .AsNoTracking()
            .Where(x => !x.IsDeleted && x.Category == normalizedCategory)
            .OrderBy(x => x.Key1)
            .Select(x => new ShipInfoLookupOption
            {
                Value = x.Key1,
                Text = string.IsNullOrWhiteSpace(x.Value1) ? x.Key1 : x.Value1!
            })
            .ToListAsync(cancellationToken);

        return rows;
    }
}

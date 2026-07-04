using ICP.Data;
using ICP.Models.ShipInfo;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

namespace ICP.Services;

public class ShipInfoLookupService
{
    private readonly ApplicationDbContext _db;
    private readonly IStringLocalizerFactory _localizerFactory;

    public ShipInfoLookupService(ApplicationDbContext db, IStringLocalizerFactory localizerFactory)
    {
        _db = db;
        _localizerFactory = localizerFactory;
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

        if (ShipInfoCaseStatuses.IsLookupCategory(normalizedCategory))
        {
            return BuildCaseStatusOptions();
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

    private IReadOnlyList<ShipInfoLookupOption> BuildCaseStatusOptions()
    {
        var localizer = _localizerFactory.Create(typeof(SharedResource));
        return ShipInfoCaseStatuses.LookupOrder
            .Select(status => new ShipInfoLookupOption
            {
                Value = ShipInfoCaseStatuses.ToCode(status),
                Text = localizer[$"ShipInfo.CaseStatus.{status}"].Value
            })
            .ToList();
    }
}

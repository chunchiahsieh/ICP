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
            .Select(x => new
            {
                x.Key1,
                x.Value1,
                x.Value3
            })
            .ToListAsync(cancellationToken);

        var isDeliveryTo = normalizedCategory.Equals("DeliveryToList", StringComparison.OrdinalIgnoreCase);
        return rows.Select(x => new ShipInfoLookupOption
        {
            Value = x.Key1,
            // DeliveryTo stores the setting key for ARUR ShipToCode, while users need
            // to see the street address maintained in DeliveryToList.Value3.
            Text = isDeliveryTo && !string.IsNullOrWhiteSpace(x.Value3)
                ? x.Value3!
                : string.IsNullOrWhiteSpace(x.Value1) ? x.Key1 : x.Value1!
        }).ToList();
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

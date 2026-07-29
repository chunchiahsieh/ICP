using ICP.Models.ShipInfo;
using Microsoft.Extensions.Options;

namespace ICP.Models.Report;

/// <summary>
/// Resolves per-report table field options bound from separate JSON files
/// via named <see cref="ShipInfoTableFieldsOptions"/>.
/// </summary>
public class ReportTableFieldsOptions
{
    private readonly IOptionsMonitor<ShipInfoTableFieldsOptions> _namedOptions;

    public ReportTableFieldsOptions(IOptionsMonitor<ShipInfoTableFieldsOptions> namedOptions)
    {
        _namedOptions = namedOptions;
    }

    public ShipInfoTableFieldsOptions Resolve(string reportKey)
    {
        if (!ReportKeys.IsKnown(reportKey))
        {
            throw new ArgumentOutOfRangeException(nameof(reportKey), reportKey, "Unknown report key.");
        }

        return _namedOptions.Get(reportKey);
    }
}

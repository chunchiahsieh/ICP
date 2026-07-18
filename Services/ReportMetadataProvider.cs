using System.Globalization;
using ICP.Helpers;
using ICP.Models.Report;
using ICP.Models.ShipInfo;
using Microsoft.Extensions.Localization;

namespace ICP.Services;

public class ReportMetadataProvider
{
    private readonly ReportTableFieldsOptions _reportTableFields;
    private readonly IStringLocalizerFactory _localizerFactory;
    private readonly ILogger<ReportMetadataProvider> _logger;

    public ReportMetadataProvider(
        ReportTableFieldsOptions reportTableFields,
        IStringLocalizerFactory localizerFactory,
        ILogger<ReportMetadataProvider> logger)
    {
        _reportTableFields = reportTableFields;
        _localizerFactory = localizerFactory;
        _logger = logger;
    }

    public ShipInfoPageConfig GetPageConfig(string reportKey, string? culture = null)
    {
        if (!ReportKeys.IsKnown(reportKey))
        {
            throw new ArgumentOutOfRangeException(nameof(reportKey), reportKey, "Unknown report key.");
        }

        var normalizedCulture = string.IsNullOrWhiteSpace(culture)
            ? CultureInfo.CurrentUICulture.Name
            : culture;
        var report = _reportTableFields.Resolve(reportKey);
        var headerFields = BuildConfiguredFields(
            ShipInfoFieldCatalog.BuildHeaderCatalog(),
            report.Header,
            normalizedCulture);
        var detailFields = BuildConfiguredFields(
            ShipInfoFieldCatalog.BuildDetailCatalog(),
            report.Detail,
            normalizedCulture);

        return new ShipInfoPageConfig
        {
            Culture = normalizedCulture,
            HeaderFields = headerFields,
            DetailFields = detailFields,
            HeaderInitialSort = report.Header.InitialSort,
            DetailInitialSort = report.Detail.InitialSort,
            HeaderTableUi = ShipInfoTableUiOptions.MergeDefaults(report.Header.TableUi),
            DetailTableUi = ShipInfoTableUiOptions.MergeDefaults(report.Detail.TableUi)
        };
    }

    private IReadOnlyList<ShipInfoFieldMetadata> BuildConfiguredFields(
        IReadOnlyList<ShipInfoFieldMetadata> catalog,
        ShipInfoTableSectionOptions section,
        string culture)
    {
        var entries = section.ResolveListFieldEntries();
        var merged = ShipInfoFieldConfigMerger.MergeList(catalog, section, _logger)
            .ToDictionary(field => field.FieldName, StringComparer.OrdinalIgnoreCase);
        var fields = new List<ShipInfoFieldMetadata>();

        foreach (var entry in entries)
        {
            if (string.IsNullOrWhiteSpace(entry.FieldName))
            {
                continue;
            }

            if (merged.TryGetValue(entry.FieldName, out var field))
            {
                fields.Add(field);
            }
            else
            {
                _logger.LogWarning("Report config references unknown field: {FieldName}", entry.FieldName);
            }
        }

        ShipInfoFieldLabelResolver.ApplyLabels(fields, _localizerFactory, culture);
        return fields;
    }
}

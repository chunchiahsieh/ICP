using ICP.Helpers;
using ICP.Models.ShipInfo;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;

namespace ICP.Services;

public class ShipInfoMetadataProvider
{
    private readonly IOptionsMonitor<ShipInfoTableFieldsOptions> _tableFieldsOptions;
    private readonly IStringLocalizerFactory _localizerFactory;
    private readonly ILogger<ShipInfoMetadataProvider> _logger;

    public ShipInfoMetadataProvider(
        IOptionsMonitor<ShipInfoTableFieldsOptions> tableFieldsOptions,
        IStringLocalizerFactory localizerFactory,
        ILogger<ShipInfoMetadataProvider> logger)
    {
        _tableFieldsOptions = tableFieldsOptions;
        _localizerFactory = localizerFactory;
        _logger = logger;
    }

    public ShipInfoPageConfig GetPageConfig(string? culture = null)
    {
        var normalizedCulture = culture ?? "zh-TW";
        var tableFields = _tableFieldsOptions.CurrentValue;
        var headerFields = MergeAndLabelFields(ShipInfoTableFieldCatalog.BuildHeaderCatalog(), tableFields.Header, normalizedCulture);
        var detailFields = MergeAndLabelFields(ShipInfoTableFieldCatalog.BuildDetailCatalog(), tableFields.Detail, normalizedCulture);

        return new ShipInfoPageConfig
        {
            Culture = normalizedCulture,
            HeaderFields = headerFields,
            DetailFields = detailFields,
            SearchFields = ShipInfoMetadataHelper.GetSearchFields(headerFields),
            StatusRules = ShipInfoStatusRules.BuildMatrix(),
            HeaderInitialSort = tableFields.Header.InitialSort,
            DetailInitialSort = tableFields.Detail.InitialSort,
            HeaderTableUi = ShipInfoTableUiOptions.MergeDefaults(tableFields.Header.TableUi),
            DetailTableUi = ShipInfoTableUiOptions.MergeDefaults(tableFields.Detail.TableUi)
        };
    }

    public IReadOnlyList<ShipInfoFieldMetadata> GetHeaderEditFields() =>
        ShipInfoTableFieldCatalog.BuildHeaderCatalog()
            .Where(x => x.FieldName is "Status" or "SaDate" or "Eta")
            .ToList();

    public IReadOnlyList<ShipInfoFieldMetadata> GetDetailEditFields() =>
        ShipInfoTableFieldCatalog.BuildDetailCatalog().Where(x => x.Editable).ToList();

    private IReadOnlyList<ShipInfoFieldMetadata> MergeAndLabelFields(
        IReadOnlyList<ShipInfoFieldMetadata> catalog,
        ShipInfoTableSectionOptions? section,
        string culture)
    {
        var merged = ShipInfoFieldConfigMerger.Merge(catalog, section, _logger);
        ShipInfoFieldLabelResolver.ApplyLabels(merged, _localizerFactory, culture);
        return merged;
    }
}

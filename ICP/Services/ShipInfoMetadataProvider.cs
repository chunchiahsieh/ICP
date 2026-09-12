using ICP.Helpers;
using ICP.Models.ShipInfo;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;

namespace ICP.Services;

public class ShipInfoMetadataProvider
{
    private readonly IOptionsMonitor<ShipInfoProDataTableFieldsOptions> _tableFieldsOptions;
    private readonly IStringLocalizerFactory _localizerFactory;
    private readonly ShipInfoFormMetadataProvider _formMetadataProvider;
    private readonly ILogger<ShipInfoMetadataProvider> _logger;

    public ShipInfoMetadataProvider(
        IOptionsMonitor<ShipInfoProDataTableFieldsOptions> tableFieldsOptions,
        IStringLocalizerFactory localizerFactory,
        ShipInfoFormMetadataProvider formMetadataProvider,
        ILogger<ShipInfoMetadataProvider> logger)
    {
        _tableFieldsOptions = tableFieldsOptions;
        _localizerFactory = localizerFactory;
        _formMetadataProvider = formMetadataProvider;
        _logger = logger;
    }

    public ShipInfoPageConfig GetPageConfig(string? culture = null)
    {
        var normalizedCulture = culture ?? "zh-TW";
        var tableFields = _tableFieldsOptions.CurrentValue;
        var headerCatalog = ShipInfoFieldCatalog.BuildHeaderCatalog();
        var detailCatalog = ShipInfoFieldCatalog.BuildDetailCatalog();
        var headerFormMetadata = _formMetadataProvider.GetHeaderFormMetadata(normalizedCulture);
        var detailFormMetadata = _formMetadataProvider.GetDetailFormMetadata(normalizedCulture);
        var headerListFields = MergeAndLabelFields(headerCatalog, tableFields.Header, ShipInfoFieldConfigMerger.MergeList, normalizedCulture);
        var configuredHeaderNames = tableFields.Header.ResolveListFieldEntries()
            .Select(x => x.FieldName).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (configuredHeaderNames.Count > 0)
        {
            headerListFields = headerListFields.Where(x => configuredHeaderNames.Contains(x.FieldName)).ToList();
        }
        var detailListFields = MergeAndLabelFields(detailCatalog, tableFields.Detail, ShipInfoFieldConfigMerger.MergeList, normalizedCulture);
        var configuredDetailNames = tableFields.Detail.ResolveListFieldEntries()
            .Select(x => x.FieldName).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (configuredDetailNames.Count > 0)
        {
            detailListFields = detailListFields.Where(x => configuredDetailNames.Contains(x.FieldName)).ToList();
        }
        var headerEditFields = _formMetadataProvider.GetHeaderEditFields(normalizedCulture);
        var detailEditFields = _formMetadataProvider.GetDetailEditFields(normalizedCulture);

        return new ShipInfoPageConfig
        {
            Culture = normalizedCulture,
            HeaderFields = headerListFields,
            DetailFields = detailListFields,
            HeaderEditFields = headerEditFields,
            HeaderFormMetadata = headerFormMetadata,
            DetailFormMetadata = detailFormMetadata,
            DetailEditFields = detailEditFields,
            SearchFields = ShipInfoMetadataHelper.GetSearchFields(headerListFields),
            StatusRules = ShipInfoStatusRules.BuildMatrix(),
            HeaderInitialSort = tableFields.Header.InitialSort,
            DetailInitialSort = tableFields.Detail.InitialSort,
            HeaderTableUi = ShipInfoTableUiOptions.MergeDefaults(tableFields.Header.TableUi),
            DetailTableUi = ShipInfoTableUiOptions.MergeDefaults(tableFields.Detail.TableUi)
        };
    }

    public IReadOnlyList<ShipInfoFieldMetadata> GetHeaderEditFields() =>
        GetPageConfig().HeaderEditFields.Where(x => x.Editable).ToList();

    public IReadOnlyList<ShipInfoFieldMetadata> GetDetailEditFields() =>
        GetPageConfig().DetailEditFields.Where(x => x.Editable).ToList();

    private IReadOnlyList<ShipInfoFieldMetadata> MergeAndLabelFields(
        IReadOnlyList<ShipInfoFieldMetadata> catalog,
        ShipInfoTableSectionOptions? section,
        Func<IReadOnlyList<ShipInfoFieldMetadata>, ShipInfoTableSectionOptions?, ILogger?, IReadOnlyList<ShipInfoFieldMetadata>> merge,
        string culture)
    {
        var merged = merge(catalog, section, _logger);
        ShipInfoFieldLabelResolver.ApplyLabels(merged, _localizerFactory, culture);
        return merged;
    }
}

using ICP.Helpers;
using ICP.Models.ShipInfo;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;

namespace ICP.Services;

public class ShipInfoMetadataProvider
{
    private readonly IOptionsMonitor<ShipInfoTableFieldsOptions> _tableFieldsOptions;
    private readonly IStringLocalizerFactory _localizerFactory;
    private readonly ShipInfoFormMetadataProvider _formMetadataProvider;
    private readonly ILogger<ShipInfoMetadataProvider> _logger;

    public ShipInfoMetadataProvider(
        IOptionsMonitor<ShipInfoTableFieldsOptions> tableFieldsOptions,
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
        var headerListFields = MergeAndLabelFields(headerCatalog, tableFields.Header, ShipInfoFieldConfigMerger.MergeList, normalizedCulture);
        var detailListFields = MergeAndLabelFields(detailCatalog, tableFields.Detail, ShipInfoFieldConfigMerger.MergeList, normalizedCulture);
        var headerEditFields = MergeAndLabelFields(headerCatalog, tableFields.Header, ShipInfoFieldConfigMerger.MergeEdit, normalizedCulture);
        var detailEditFields = MergeAndLabelFields(detailCatalog, tableFields.Detail, ShipInfoFieldConfigMerger.MergeEdit, normalizedCulture);

        return new ShipInfoPageConfig
        {
            Culture = normalizedCulture,
            HeaderFields = headerListFields,
            DetailFields = detailListFields,
            HeaderEditFields = headerEditFields,
            HeaderFormMetadata = _formMetadataProvider.GetHeaderFormMetadata(normalizedCulture),
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

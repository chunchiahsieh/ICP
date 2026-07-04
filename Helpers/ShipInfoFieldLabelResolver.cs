using System.Globalization;
using ICP.Models.ShipInfo;
using Microsoft.Extensions.Localization;

namespace ICP.Helpers;

public static class ShipInfoFieldLabelResolver
{
    public static string ResolveLabelKey(ShipInfoFieldMetadata field) =>
        string.IsNullOrWhiteSpace(field.LabelKey)
            ? $"ShipInfo.Field.{field.FieldName}"
            : field.LabelKey;

    public static void ApplyLabels(
        IEnumerable<ShipInfoFieldMetadata> fields,
        IStringLocalizerFactory localizerFactory,
        string? culture)
    {
        var normalizedCulture = string.IsNullOrWhiteSpace(culture) ? "zh-TW" : culture;
        var cultureInfo = CultureInfo.GetCultureInfo(normalizedCulture);
        var localizer = localizerFactory.Create(typeof(SharedResource));
        var previousUiCulture = CultureInfo.CurrentUICulture;
        var previousCulture = CultureInfo.CurrentCulture;

        try
        {
            CultureInfo.CurrentUICulture = cultureInfo;
            CultureInfo.CurrentCulture = cultureInfo;

            foreach (var field in fields)
            {
                var localized = localizer[ResolveLabelKey(field)];
                field.Label = localized.ResourceNotFound
                    ? ShipInfoTableViewHelper.ResolveLabel(field, normalizedCulture)
                    : localized.Value;
            }
        }
        finally
        {
            CultureInfo.CurrentUICulture = previousUiCulture;
            CultureInfo.CurrentCulture = previousCulture;
        }
    }
}

using ICP.Models.ShipInfo;

namespace ICP.Services;

public static class ShipInfoFieldConfigMerger
{
    public static IReadOnlyList<ShipInfoFieldMetadata> Merge(
        IReadOnlyList<ShipInfoFieldMetadata> catalog,
        ShipInfoTableSectionOptions? section,
        ILogger? logger = null)
    {
        if (section?.Fields is not { Count: > 0 })
        {
            return catalog.Select(Clone).ToList();
        }

        var catalogByName = catalog.ToDictionary(x => x.FieldName, StringComparer.OrdinalIgnoreCase);
        var merged = new List<ShipInfoFieldMetadata>(catalog.Count);
        var configuredNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (var index = 0; index < section.Fields.Count; index++)
        {
            var entry = section.Fields[index];
            if (string.IsNullOrWhiteSpace(entry.FieldName))
            {
                continue;
            }

            if (!catalogByName.TryGetValue(entry.FieldName, out var catalogField))
            {
                logger?.LogWarning("ShipInfo table config references unknown field: {FieldName}", entry.FieldName);
                continue;
            }

            configuredNames.Add(entry.FieldName);
            var copy = ApplyEntry(Clone(catalogField), entry);
            copy.DisplayOrder = (index + 1) * 10;
            merged.Add(copy);
        }

        var trailingOrder = merged.Count;
        foreach (var catalogField in catalog)
        {
            if (configuredNames.Contains(catalogField.FieldName))
            {
                continue;
            }

            var copy = Clone(catalogField);
            copy.DisplayOrder = (++trailingOrder) * 10;
            merged.Add(copy);
        }

        return merged;
    }

    private static ShipInfoFieldMetadata ApplyEntry(ShipInfoFieldMetadata field, ShipInfoTableFieldEntry entry)
    {
        if (entry.Visible.HasValue)
        {
            field.Visible = entry.Visible.Value;
        }

        if (entry.Searchable.HasValue)
        {
            field.Searchable = entry.Searchable.Value;
        }

        if (!string.IsNullOrWhiteSpace(entry.FilterType))
        {
            field.FilterType = ShipInfoFilterTypes.Normalize(entry.FilterType);
        }

        if (!string.IsNullOrWhiteSpace(entry.LabelKey))
        {
            field.LabelKey = entry.LabelKey;
        }

        return field;
    }

    private static ShipInfoFieldMetadata Clone(ShipInfoFieldMetadata source) =>
        new()
        {
            Id = source.Id,
            FieldName = source.FieldName,
            DisplayName = source.DisplayName,
            DisplayNameZh = source.DisplayNameZh,
            LabelKey = source.LabelKey,
            Label = source.Label,
            DisplayOrder = source.DisplayOrder,
            Visible = source.Visible,
            Searchable = source.Searchable,
            FilterType = source.FilterType,
            Editable = source.Editable,
            Required = source.Required,
            ControlType = source.ControlType,
            SearchControlType = source.SearchControlType,
            LookupCategory = source.LookupCategory,
            Placeholder = source.Placeholder,
            MaxLength = source.MaxLength,
            MinLength = source.MinLength,
            MaxValue = source.MaxValue,
            MinValue = source.MinValue,
            Regex = source.Regex,
            ReadOnly = source.ReadOnly,
            PermissionCode = source.PermissionCode,
            Tooltip = source.Tooltip,
            DefaultValue = source.DefaultValue,
            Group = source.Group,
            Tab = source.Tab,
            Section = source.Section
        };
}

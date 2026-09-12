using ICP.Models.ShipInfo;

namespace ICP.Services;

public static class ShipInfoFieldConfigMerger
{
    public static IReadOnlyList<ShipInfoFieldMetadata> MergeList(
        IReadOnlyList<ShipInfoFieldMetadata> catalog,
        ShipInfoTableSectionOptions? section,
        ILogger? logger = null) =>
        Merge(
            catalog,
            section?.ResolveListFieldEntries() ?? [],
            ApplyListEntry,
            includeUnconfiguredCatalogFields: true,
            logger);

    private static IReadOnlyList<ShipInfoFieldMetadata> Merge(
        IReadOnlyList<ShipInfoFieldMetadata> catalog,
        IReadOnlyList<ShipInfoTableFieldEntry> entries,
        Action<ShipInfoFieldMetadata, ShipInfoTableFieldEntry> applyEntry,
        bool includeUnconfiguredCatalogFields,
        ILogger? logger)
    {
        if (entries.Count == 0)
        {
            return catalog.Select(Clone).ToList();
        }

        var catalogByName = catalog.ToDictionary(x => x.FieldName, StringComparer.OrdinalIgnoreCase);
        var merged = new List<ShipInfoFieldMetadata>(entries.Count);
        var configuredNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (var index = 0; index < entries.Count; index++)
        {
            var entry = entries[index];
            if (string.IsNullOrWhiteSpace(entry.FieldName))
            {
                continue;
            }

            if (!catalogByName.TryGetValue(entry.FieldName, out var catalogField))
            {
                logger?.LogWarning("ShipInfo field config references unknown DTO field: {FieldName}", entry.FieldName);
                continue;
            }

            configuredNames.Add(entry.FieldName);
            var copy = Clone(catalogField);
            applyEntry(copy, entry);
            copy.DisplayOrder = (index + 1) * 10;
            merged.Add(copy);
        }

        if (!includeUnconfiguredCatalogFields)
        {
            return merged;
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

    private static void ApplyListEntry(ShipInfoFieldMetadata field, ShipInfoTableFieldEntry entry)
    {
        if (entry.Visible.HasValue)
        {
            field.Visible = entry.Visible.Value;
        }

        field.Searchable = entry.Searchable ?? true;

        if (!string.IsNullOrWhiteSpace(entry.FilterType))
        {
            field.FilterType = ShipInfoFilterTypes.Normalize(entry.FilterType);
        }

        if (!string.IsNullOrWhiteSpace(entry.LabelKey))
        {
            field.LabelKey = entry.LabelKey;
        }
    }

    private static ShipInfoFieldMetadata Clone(ShipInfoFieldMetadata source) =>
        new()
        {
            Id = source.Id,
            FieldName = source.FieldName,
            EntityPropertyName = source.EntityPropertyName,
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

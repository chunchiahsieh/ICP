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

    public static IReadOnlyList<ShipInfoFieldMetadata> MergeEdit(
        IReadOnlyList<ShipInfoFieldMetadata> catalog,
        ShipInfoTableSectionOptions? section,
        ILogger? logger = null)
    {
        if (section?.Edit?.IncludeAllExceptSystem == true)
        {
            return MergeEditAllExceptSystem(catalog, section.Edit.Fields, logger);
        }

        var entries = section?.ResolveEditFieldEntries() ?? [];
        if (entries.Count == 0)
        {
            return catalog
                .Where(x => x.Editable)
                .Select(Clone)
                .ToList();
        }

        return Merge(
            catalog,
            entries,
            ApplyEditEntry,
            includeUnconfiguredCatalogFields: false,
            logger);
    }

    private static IReadOnlyList<ShipInfoFieldMetadata> MergeEditAllExceptSystem(
        IReadOnlyList<ShipInfoFieldMetadata> catalog,
        IReadOnlyList<ShipInfoTableFieldEntry> overrides,
        ILogger? logger)
    {
        var excluded = new HashSet<string>(ShipInfoSystemFieldNames.ExcludedFromEdit, StringComparer.OrdinalIgnoreCase);
        var overrideByName = overrides
            .Where(x => !string.IsNullOrWhiteSpace(x.FieldName))
            .GroupBy(x => x.FieldName, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.Last(), StringComparer.OrdinalIgnoreCase);

        var merged = new List<ShipInfoFieldMetadata>();
        var displayOrder = 0;
        foreach (var catalogField in catalog.OrderBy(x => x.DisplayOrder).ThenBy(x => x.FieldName, StringComparer.OrdinalIgnoreCase))
        {
            if (excluded.Contains(catalogField.FieldName))
            {
                continue;
            }

            var copy = Clone(catalogField);
            copy.Visible = true;
            // Opt-in editable: catalog defaults are ignored; only edit.fields with editable:true unlock editing.
            copy.Editable = false;
            if (overrideByName.TryGetValue(catalogField.FieldName, out var entry))
            {
                ApplyEditEntry(copy, entry);
            }

            copy.DisplayOrder = (++displayOrder) * 10;
            merged.Add(copy);
        }

        foreach (var entry in overrides)
        {
            if (string.IsNullOrWhiteSpace(entry.FieldName))
            {
                continue;
            }

            if (merged.Any(x => x.FieldName.Equals(entry.FieldName, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            logger?.LogWarning("ShipInfo edit override references unknown or excluded field: {FieldName}", entry.FieldName);
        }

        return merged;
    }

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

    private static void ApplyEditEntry(ShipInfoFieldMetadata field, ShipInfoTableFieldEntry entry)
    {
        if (entry.Visible.HasValue)
        {
            field.Visible = entry.Visible.Value;
        }

        if (!string.IsNullOrWhiteSpace(entry.LabelKey))
        {
            field.LabelKey = entry.LabelKey;
        }

        if (entry.Editable.HasValue)
        {
            field.Editable = entry.Editable.Value;
        }

        if (!string.IsNullOrWhiteSpace(entry.ControlType))
        {
            field.ControlType = ShipInfoControlTypes.Normalize(entry.ControlType);
        }

        if (!string.IsNullOrWhiteSpace(entry.LookupCategory))
        {
            field.LookupCategory = entry.LookupCategory;
        }

        if (entry.Required.HasValue)
        {
            field.Required = entry.Required.Value;
        }

        if (entry.ReadOnly.HasValue)
        {
            field.ReadOnly = entry.ReadOnly.Value;
        }

        if (entry.MaxLength.HasValue)
        {
            field.MaxLength = entry.MaxLength.Value;
        }

        if (!string.IsNullOrWhiteSpace(entry.Placeholder))
        {
            field.Placeholder = entry.Placeholder;
        }

        if (!string.IsNullOrWhiteSpace(entry.Group))
        {
            field.Group = entry.Group;
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

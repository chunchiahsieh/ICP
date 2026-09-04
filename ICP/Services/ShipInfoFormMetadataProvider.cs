using System.Globalization;
using System.Text.Json;
using ICP.Helpers;
using ICP.Models.ShipInfo;
using Microsoft.Extensions.Localization;

namespace ICP.Services;

public sealed class ShipInfoFormMetadataProvider
{
    public const string HeaderFormId = "shipinfo-header";
    public const string DetailFormId = "shipinfo-detail";
    private const string ViewMode = "view";
    private const string EditMode = "edit";
    private const string CreateMode = "create";
    private static readonly HashSet<string> SupportedTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "text", "number", "date", "select", "checkbox"
    };
    private static readonly IReadOnlyDictionary<string, string> OptionsSources =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["airSea"] = "AirSea",
            ["broker"] = "Broker",
            ["buCode"] = "BuCode",
            ["customized"] = "Customized",
            ["defaultDeliveryWh"] = "DefaultDeliveryWh",
            ["deliveryTo"] = "DeliveryToList",
            ["etaDelDateTable"] = "EtaDelDateTable",
            ["invoiceType"] = "InvoiceType",
            ["orderPriority"] = "OrderPriority",
            ["orderType"] = "OrderType",
            ["pickUpLocation"] = "PickUpLocation",
            ["warehouse"] = "WhCode"
        };
    private const string FileUploaderComponent = "fileUploader";
    private const string HeaderAttachmentsAdapter = "shipInfoHeaderAttachments";

    private readonly string _headerFilePath;
    private readonly string _detailFilePath;
    private readonly bool _isDevelopment;
    private readonly IStringLocalizerFactory _localizerFactory;
    private readonly ILogger<ShipInfoFormMetadataProvider> _logger;

    public ShipInfoFormMetadataProvider(
        IWebHostEnvironment environment,
        IStringLocalizerFactory localizerFactory,
        ILogger<ShipInfoFormMetadataProvider> logger)
    {
        _headerFilePath = Path.Combine(environment.ContentRootPath, "Config", "shipinfo-form-fields.json");
        _detailFilePath = Path.Combine(environment.ContentRootPath, "Config", "shipinfo-detail-form-fields.json");
        _isDevelopment = environment.IsDevelopment();
        _localizerFactory = localizerFactory;
        _logger = logger;
    }

    public ShipInfoFormMetadata GetHeaderFormMetadata(string? culture = null)
    {
        var metadata = LoadAndValidate(_headerFilePath, HeaderFormId, ShipInfoFieldCatalog.BuildHeaderCatalog());
        ApplyLocalizedText(metadata, ShipInfoFieldCatalog.BuildHeaderCatalog(), culture ?? "zh-TW");
        return metadata;
    }

    public ShipInfoFormMetadata GetDetailFormMetadata(string? culture = null)
    {
        var metadata = LoadAndValidate(_detailFilePath, DetailFormId, ShipInfoFieldCatalog.BuildDetailCatalog());
        ApplyLocalizedText(metadata, ShipInfoFieldCatalog.BuildDetailCatalog(), culture ?? "zh-TW");
        return metadata;
    }

    public static void ValidateAtStartup(string contentRootPath)
    {
        _ = LoadAndValidate(Path.Combine(contentRootPath, "Config", "shipinfo-form-fields.json"), HeaderFormId, ShipInfoFieldCatalog.BuildHeaderCatalog());
        _ = LoadAndValidate(Path.Combine(contentRootPath, "Config", "shipinfo-detail-form-fields.json"), DetailFormId, ShipInfoFieldCatalog.BuildDetailCatalog());
    }

    private static ShipInfoFormMetadata LoadAndValidate(string path, string formId, IReadOnlyList<ShipInfoFieldMetadata> catalog)
    {
        if (!File.Exists(path))
        {
            throw new InvalidOperationException($"Ship Info form metadata file is missing: {path}");
        }

        using var document = JsonDocument.Parse(File.ReadAllText(path));
        ValidateJsonShape(document.RootElement);
        var metadata = document.RootElement.Deserialize<ShipInfoFormMetadata>(new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }) ?? throw new InvalidOperationException("Ship Info form metadata is invalid.");

        ValidateMetadata(metadata, formId, catalog);
        return metadata;
    }

    private static void ValidateMetadata(ShipInfoFormMetadata metadata, string formId, IReadOnlyList<ShipInfoFieldMetadata> catalogFields)
    {
        if (!string.Equals(metadata.FormId, formId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"formId must be '{formId}'.");
        }

        if (string.IsNullOrWhiteSpace(metadata.MetadataVersion))
        {
            throw new InvalidOperationException("metadataVersion is required.");
        }

        if (metadata.Fields.Count == 0)
        {
            throw new InvalidOperationException("fields must contain at least one field.");
        }

        var catalog = catalogFields
            .ToDictionary(x => x.FieldName, StringComparer.OrdinalIgnoreCase);
        foreach (var (name, field) in metadata.Fields)
        {
            if (string.IsNullOrWhiteSpace(name) || !catalog.ContainsKey(name))
            {
                throw new InvalidOperationException($"fields contains an unknown Ship Info Header field: '{name}'.");
            }

            if (!SupportedTypes.Contains(field.Type))
            {
                throw new InvalidOperationException($"fields.{name}.type '{field.Type}' is not supported.");
            }

            if (field.MaxLength is <= 0)
            {
                throw new InvalidOperationException($"fields.{name}.maxLength must be greater than zero.");
            }

            if (string.Equals(field.Type, "select", StringComparison.OrdinalIgnoreCase))
            {
                var hasOptions = field.Options is { Count: > 0 };
                var hasSource = !string.IsNullOrWhiteSpace(field.OptionsSource);
                if (hasOptions == hasSource)
                {
                    throw new InvalidOperationException($"fields.{name} select requires exactly one of options or optionsSource.");
                }

                if (hasSource && !OptionsSources.ContainsKey(field.OptionsSource!))
                {
                    throw new InvalidOperationException($"fields.{name}.optionsSource '{field.OptionsSource}' is not registered.");
                }

                if (hasOptions && field.Options!.Any(x => string.IsNullOrWhiteSpace(x.Value)))
                {
                    throw new InvalidOperationException($"fields.{name}.options contains an empty value.");
                }
            }
            else if (field.Options is { Count: > 0 } || !string.IsNullOrWhiteSpace(field.OptionsSource))
            {
                throw new InvalidOperationException($"fields.{name} options are only supported by select.");
            }

            if (string.Equals(field.Type, "checkbox", StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrWhiteSpace(field.CheckedValue) || string.IsNullOrWhiteSpace(field.UncheckedValue)
                    || string.Equals(field.CheckedValue, field.UncheckedValue, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException($"fields.{name} checkbox requires different checkedValue and uncheckedValue.");
                }
            }
            else if (field.CheckedValue is not null || field.UncheckedValue is not null)
            {
                throw new InvalidOperationException($"fields.{name} checkbox mapping is only supported by checkbox.");
            }
        }

        foreach (var requiredMode in new[] { ViewMode, EditMode })
        {
            if (!metadata.Modes.ContainsKey(requiredMode))
            {
                throw new InvalidOperationException($"modes.{requiredMode} is required.");
            }
        }

        foreach (var (mode, definition) in metadata.Modes)
        {
            if (!new[] { ViewMode, EditMode, CreateMode }.Contains(mode, StringComparer.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"modes contains unsupported mode '{mode}'.");
            }

            var groupIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var fieldNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var group in definition.Groups)
            {
                if (string.IsNullOrWhiteSpace(group.Id) || !groupIds.Add(group.Id))
                {
                    throw new InvalidOperationException($"modes.{mode}.groups contains a missing or duplicate id.");
                }

                var columns = group.Columns ?? 1;
                if (columns is < 1 or > 4)
                {
                    throw new InvalidOperationException($"modes.{mode}.groups.{group.Id}.columns must be between 1 and 4.");
                }

                var isComponent = !string.IsNullOrWhiteSpace(group.Component);
                if (isComponent)
                {
                    if (!string.Equals(group.Component, FileUploaderComponent, StringComparison.Ordinal)
                        || !string.Equals(group.Adapter, HeaderAttachmentsAdapter, StringComparison.Ordinal)
                        || group.Fields.Count != 0)
                    {
                        throw new InvalidOperationException($"modes.{mode}.groups.{group.Id} has an invalid component definition.");
                    }

                    continue;
                }

                if (!string.IsNullOrWhiteSpace(group.Adapter))
                {
                    throw new InvalidOperationException($"modes.{mode}.groups.{group.Id}.adapter requires a component.");
                }

                foreach (var field in group.Fields)
                {
                    if (string.IsNullOrWhiteSpace(field.Name) || !metadata.Fields.ContainsKey(field.Name))
                    {
                        throw new InvalidOperationException($"modes.{mode}.groups.{group.Id} references an unknown field '{field.Name}'.");
                    }

                    if (!fieldNames.Add(field.Name))
                    {
                        throw new InvalidOperationException($"modes.{mode} references field '{field.Name}' more than once.");
                    }

                    if (field.ColumnSpan is <= 0 || (field.ColumnSpan ?? 1) > columns)
                    {
                        throw new InvalidOperationException($"modes.{mode}.groups.{group.Id}.{field.Name}.columnSpan is invalid.");
                    }
                }
            }
        }
    }

    private static void ValidateJsonShape(JsonElement root)
    {
        RequireObject(root, "$", "formId", "metadataVersion", "fields", "modes");
        ValidateProperties(root, "$", ["formId", "metadataVersion", "fields", "modes"]);

        var fieldNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var field in root.GetProperty("fields").EnumerateObject())
        {
            if (!fieldNames.Add(field.Name)) throw new InvalidOperationException($"$.fields contains duplicate field '{field.Name}'.");
            RequireObject(field.Value, "$.fields." + field.Name, "type");
            ValidateProperties(field.Value, "$.fields." + field.Name,
                ["labelKey", "type", "maxLength", "placeholderKey", "helpTextKey", "options", "optionsSource", "checkedValue", "uncheckedValue"]);
            if (field.Value.TryGetProperty("options", out var options))
            {
                if (options.ValueKind != JsonValueKind.Array) throw new InvalidOperationException($"$.fields.{field.Name}.options must be an array.");
                foreach (var option in options.EnumerateArray())
                {
                    RequireObject(option, "$.fields." + field.Name + ".options[]", "value");
                    ValidateProperties(option, "$.fields." + field.Name + ".options[]", ["value", "labelKey"]);
                }
            }
        }

        var modeNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var mode in root.GetProperty("modes").EnumerateObject())
        {
            if (!modeNames.Add(mode.Name)) throw new InvalidOperationException($"$.modes contains duplicate mode '{mode.Name}'.");
            RequireObject(mode.Value, "$.modes." + mode.Name, "groups");
            ValidateProperties(mode.Value, "$.modes." + mode.Name, ["groups"]);
            foreach (var group in mode.Value.GetProperty("groups").EnumerateArray())
            {
                RequireObject(group, "$.modes." + mode.Name + ".groups[]", "id", "fields");
                ValidateProperties(group, "$.modes." + mode.Name + ".groups[]", ["id", "labelKey", "order", "columns", "component", "adapter", "fields"]);
                foreach (var field in group.GetProperty("fields").EnumerateArray())
                {
                    RequireObject(field, "$.modes." + mode.Name + ".groups[].fields[]", "name");
                    ValidateProperties(field, "$.modes." + mode.Name + ".groups[].fields[]", ["name", "order", "readOnly", "required", "columnSpan"]);
                }
            }
        }
    }

    private static void RequireObject(JsonElement element, string path, params string[] requiredProperties)
    {
        if (element.ValueKind != JsonValueKind.Object) throw new InvalidOperationException($"{path} must be an object.");
        foreach (var property in requiredProperties)
        {
            if (!element.TryGetProperty(property, out _)) throw new InvalidOperationException($"{path}.{property} is required.");
        }
    }

    private static void ValidateProperties(JsonElement element, string path, IReadOnlyCollection<string> allowed)
    {
        foreach (var property in element.EnumerateObject())
        {
            if (!allowed.Contains(property.Name, StringComparer.Ordinal))
            {
                throw new InvalidOperationException($"{path}.{property.Name} is not a supported metadata property.");
            }
        }
    }

    private void ApplyLocalizedText(ShipInfoFormMetadata metadata, IReadOnlyList<ShipInfoFieldMetadata> catalogFields, string culture)
    {
        var catalog = catalogFields
            .ToDictionary(x => x.FieldName, StringComparer.OrdinalIgnoreCase);
        var localizer = _localizerFactory.Create(typeof(SharedResource));
        var previousUiCulture = CultureInfo.CurrentUICulture;
        var previousCulture = CultureInfo.CurrentCulture;
        try
        {
            var cultureInfo = CultureInfo.GetCultureInfo(culture);
            CultureInfo.CurrentUICulture = cultureInfo;
            CultureInfo.CurrentCulture = cultureInfo;
            foreach (var (name, field) in metadata.Fields)
            {
                field.Label = ResolveText(localizer, field.LabelKey, catalog[name].DisplayName, $"fields.{name}.labelKey");
                field.Placeholder = ResolveText(localizer, field.PlaceholderKey, null, $"fields.{name}.placeholderKey");
                field.HelpText = ResolveText(localizer, field.HelpTextKey, null, $"fields.{name}.helpTextKey");
                field.LookupCategory = !string.IsNullOrWhiteSpace(field.OptionsSource)
                    ? OptionsSources[field.OptionsSource]
                    : null;
                if (field.Options is not null)
                {
                    foreach (var option in field.Options)
                    {
                        option.Label = ResolveText(localizer, option.LabelKey, option.Value, $"fields.{name}.options.labelKey");
                    }
                }
            }

            foreach (var mode in metadata.Modes.Values)
            {
                foreach (var group in mode.Groups)
                {
                    group.Label = string.IsNullOrWhiteSpace(group.LabelKey)
                        ? null
                        : ResolveText(localizer, group.LabelKey, group.Id, $"groups.{group.Id}.labelKey");
                }
            }
        }
        finally
        {
            CultureInfo.CurrentUICulture = previousUiCulture;
            CultureInfo.CurrentCulture = previousCulture;
        }
    }

    private string? ResolveText(IStringLocalizer localizer, string? key, string? fallback, string path)
    {
        if (string.IsNullOrWhiteSpace(key)) return fallback;
        var localized = localizer[key];
        if (!localized.ResourceNotFound) return localized.Value;
        if (_isDevelopment)
        {
            _logger.LogWarning("Ship Info form metadata localization key was not found: {Path} ({Key})", path, key);
        }
        return fallback;
    }
}

using System.Globalization;
using System.Xml;

namespace ICP.Services;

/// <summary>Loads the latest Export translations from the editable RESX files for each request.</summary>
public sealed class ExportRuntimeTranslations(
    LocalizationResourceManagementService resources,
    ILogger<ExportRuntimeTranslations> logger)
{
    private readonly Dictionary<string, IReadOnlyDictionary<string, string>> _snapshots = new(StringComparer.OrdinalIgnoreCase);

    public string Resolve(string key, string fallback)
    {
        var culture = CultureInfo.CurrentUICulture.Name;
        if (!_snapshots.TryGetValue(culture, out var values))
        {
            try
            {
                values = resources.ReadExportTranslations(culture);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or XmlException or ArgumentException)
            {
                logger.LogWarning(ex, "Unable to load Export translations; using page defaults.");
                values = new Dictionary<string, string>();
            }
            _snapshots[culture] = values;
        }

        return values.TryGetValue(key, out var value) ? value : fallback;
    }
}

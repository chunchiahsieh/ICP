using System.Xml;

namespace ICP.Services;

/// <summary>One translation snapshot per language per request; the next request sees saved RESX edits.</summary>
public sealed class ShipInfoRuntimeTranslations(
    LocalizationResourceManagementService resources,
    ILogger<ShipInfoRuntimeTranslations> logger)
{
    private readonly Dictionary<string, IReadOnlyDictionary<string, string>> _snapshots = new(StringComparer.OrdinalIgnoreCase);

    public string? Resolve(string key, string culture)
    {
        if (!_snapshots.TryGetValue(culture, out var values))
        {
            try
            {
                values = resources.ReadShipInfoTranslations(culture);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or XmlException or ArgumentException)
            {
                logger.LogWarning(ex, "Unable to load ShipInfo translations; using embedded resources.");
                values = new Dictionary<string, string>();
            }
            _snapshots[culture] = values;
        }
        return values.GetValueOrDefault(key);
    }
}

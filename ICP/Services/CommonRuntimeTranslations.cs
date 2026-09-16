using System.Globalization;
using System.Xml;

namespace ICP.Services;

/// <summary>Loads editable translations for shared browser UI labels.</summary>
public sealed class CommonRuntimeTranslations(
    LocalizationResourceManagementService resources,
    ILogger<CommonRuntimeTranslations> logger)
{
    public string Resolve(string key, string fallback)
    {
        try
        {
            return resources.ReadTranslation(key, CultureInfo.CurrentUICulture.Name) ?? fallback;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or XmlException or ArgumentException)
        {
            logger.LogWarning(ex, "Unable to load runtime translation for {Key}; using embedded resource.", key);
            return fallback;
        }
    }
}

using System.Collections;
using System.Globalization;
using System.Xml;
using Microsoft.Extensions.Localization;

namespace ICP.Services;

/// <summary>
/// Uses the editable RESX files for SharedResource. One snapshot is kept for the
/// current request, so the next page load reflects a Localization Management save.
/// </summary>
public sealed class RuntimeSharedResourceLocalizer(
    IStringLocalizerFactory factory,
    LocalizationResourceManagementService resources,
    ILogger<RuntimeSharedResourceLocalizer> logger) : IStringLocalizer<SharedResource>
{
    private readonly IStringLocalizer _embedded = factory.Create(typeof(SharedResource));
    private readonly Dictionary<string, IReadOnlyDictionary<string, string>?> _snapshots = new(StringComparer.OrdinalIgnoreCase);

    public LocalizedString this[string name] => Resolve(name, []);

    public LocalizedString this[string name, params object[] arguments] => Resolve(name, arguments);

    public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => _embedded.GetAllStrings(includeParentCultures);

    private LocalizedString Resolve(string name, object[] arguments)
    {
        var culture = CultureInfo.CurrentUICulture.Name;
        if (!_snapshots.TryGetValue(culture, out var values))
        {
            try
            {
                values = resources.ReadTranslations(culture);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or XmlException or ArgumentException)
            {
                logger.LogWarning(ex, "Unable to load runtime localization resources; using embedded resources.");
                values = null;
            }
            _snapshots[culture] = values;
        }

        if (values is not null && values.TryGetValue(name, out var value))
        {
            return new LocalizedString(name, arguments.Length == 0 ? value : string.Format(CultureInfo.CurrentCulture, value, arguments));
        }

        return arguments.Length == 0 ? _embedded[name] : _embedded[name, arguments];
    }
}

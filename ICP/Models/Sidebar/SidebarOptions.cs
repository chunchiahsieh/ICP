using Microsoft.Extensions.Options;

namespace ICP.Models.Sidebar;

public sealed class SidebarOptions
{
    public const string SectionName = "Sidebar";
    public int RecentLimit { get; set; } = 3;
    public bool RecentEnabled { get; set; } = true;
    public int PinnedLimit { get; set; } = 5;
    public string DefaultPage { get; set; } = "ShipInfo";
    public Dictionary<string, int> MenuOrder { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public List<SidebarCustomLinkOptions> CustomLinks { get; set; } = [];
}

public sealed class SidebarCustomLinkOptions
{
    public string Name { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public bool OpenInNewTab { get; set; }
    public bool Enabled { get; set; } = true;
    public int? SortOrder { get; set; }
}

public sealed class SidebarOptionsValidator : IValidateOptions<SidebarOptions>
{
    private static readonly HashSet<string> ValidDefaultPages = new(StringComparer.OrdinalIgnoreCase)
    {
        "ShipInfo", "AddDiSa", "MassUpdateNonNcpi", "MassUpdateNcpi", "Export",
        "BuCode", "WhCode", "DeliveryToList", "PickUpLocation", "EtaDelDateTable",
        "DefaultDeliveryWh", "OrderType", "AirSea", "Broker", "InvoiceType", "OrderPriority", "Customized",
        "ShippingReport", "CompareIcpVsArUr", "MassDataReport", "ForwarderDataUpload", "CustomsDataDownload", "TariffData",
        "LocalizationManagement", "Users", "Resources", "Roles", "RolePermissions", "RoleTelIds", "RoleDepIds", "RoleMailGroups"
    };

    public ValidateOptionsResult Validate(string? name, SidebarOptions options)
    {
        var failures = new List<string>();
        if (options.RecentLimit is < 0 or > 50)
            failures.Add("Sidebar:RecentLimit must be between 0 and 50.");
        if (options.PinnedLimit is < 0 or > 20)
            failures.Add("Sidebar:PinnedLimit must be between 0 and 20.");
        if (!ValidDefaultPages.Contains(options.DefaultPage))
            failures.Add("Sidebar:DefaultPage must be the controller name of an ICP menu page.");
        foreach (var (page, order) in options.MenuOrder)
        {
            if (!ValidDefaultPages.Contains(page))
                failures.Add($"Sidebar:MenuOrder contains unknown menu page '{page}'.");
            if (order is < -10000 or > 10000)
                failures.Add($"Sidebar:MenuOrder '{page}' must be between -10000 and 10000.");
        }

        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var urls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var link in options.CustomLinks.Where(link => link.Enabled))
        {
            if (string.IsNullOrWhiteSpace(link.Name))
                failures.Add("Each enabled Sidebar:CustomLinks entry requires a Name.");
            else if (!names.Add(link.Name.Trim()))
                failures.Add($"Sidebar:CustomLinks contains duplicate name '{link.Name}'.");

            var normalizedUrl = link.Url.Trim();
            var isLocalPath = normalizedUrl.StartsWith('/') && !normalizedUrl.StartsWith("//", StringComparison.Ordinal);
            var isHttpUrl = Uri.TryCreate(normalizedUrl, UriKind.Absolute, out var uri) &&
                (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
            if (!isLocalPath && !isHttpUrl)
                failures.Add($"Sidebar:CustomLinks '{link.Name}' must use a root-relative path or an absolute http or https Url.");
            else if (!urls.Add(isLocalPath ? normalizedUrl : uri!.AbsoluteUri))
                failures.Add($"Sidebar:CustomLinks contains duplicate Url '{normalizedUrl}'.");
            if (link.SortOrder is < -10000 or > 10000)
                failures.Add($"Sidebar:CustomLinks '{link.Name}' SortOrder must be between -10000 and 10000.");
        }

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }
}

using System.Xml.Linq;

namespace ICP.Services;

/// <summary>從 SharedResource.resx 解析顯示名稱（供 PermissionScan 使用）。</summary>
public static class SharedResourceNameResolver
{
    public static string? TryResolve(IWebHostEnvironment environment, string? resourceKey)
    {
        if (string.IsNullOrWhiteSpace(resourceKey))
        {
            return null;
        }

        var resxPath = Path.Combine(environment.ContentRootPath, "Resources", "SharedResource.resx");
        if (!File.Exists(resxPath))
        {
            return null;
        }

        try
        {
            var doc = XDocument.Load(resxPath);
            var data = doc.Root?
                .Elements("data")
                .FirstOrDefault(e => (string?)e.Attribute("name") == resourceKey.Trim());

            return data?.Element("value")?.Value?.Trim();
        }
        catch
        {
            return null;
        }
    }
}

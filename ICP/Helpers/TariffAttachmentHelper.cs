using ICP.Models;
using ICP.Models.Icp;

namespace ICP.Helpers;

public static class TariffAttachmentHelper
{
    public const string DeclarationPdfFolder = "declaration-pdf";
    public const string CostFolder = "cost";
    public const string PresenceHas = "Has";
    public const string PresenceNone = "None";

    public static string ResolveStorageRoot(IWebHostEnvironment environment, TariffDataOptions options)
    {
        var root = Path.IsPathRooted(options.StoragePath)
            ? options.StoragePath
            : Path.Combine(environment.ContentRootPath, options.StoragePath);

        return Path.GetFullPath(root);
    }

    public static string SanitizeHawbFileStem(string hawb)
    {
        var trimmed = (hawb ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return string.Empty;
        }

        foreach (var invalid in Path.GetInvalidFileNameChars())
        {
            trimmed = trimmed.Replace(invalid, '_');
        }

        return trimmed;
    }

    public static string? FindDeclarationPdfPath(string storageRoot, TariffData item)
    {
        if (!string.IsNullOrWhiteSpace(item.DeclarationFile))
        {
            var fromDb = ResolveUnderStorage(storageRoot, item.DeclarationFile);
            if (fromDb is not null)
            {
                return fromDb;
            }
        }

        var stem = SanitizeHawbFileStem(item.HAWB);
        if (string.IsNullOrWhiteSpace(stem))
        {
            return null;
        }

        var candidate = Path.Combine(storageRoot, DeclarationPdfFolder, stem + ".pdf");
        return System.IO.File.Exists(candidate) ? Path.GetFullPath(candidate) : null;
    }

    public static string? FindCostFilePath(string storageRoot, TariffData item)
    {
        if (!string.IsNullOrWhiteSpace(item.Cost))
        {
            var relative = item.Cost.Contains('/') || item.Cost.Contains('\\')
                ? item.Cost
                : Path.Combine(CostFolder, item.Cost);
            var fromDb = ResolveUnderStorage(storageRoot, relative);
            if (fromDb is not null)
            {
                return fromDb;
            }

            var direct = ResolveUnderStorage(storageRoot, Path.Combine(CostFolder, Path.GetFileName(item.Cost)));
            if (direct is not null)
            {
                return direct;
            }
        }

        var stem = SanitizeHawbFileStem(item.HAWB);
        if (string.IsNullOrWhiteSpace(stem))
        {
            return null;
        }

        foreach (var ext in new[] { ".xlsx", ".xls" })
        {
            var candidate = Path.Combine(storageRoot, CostFolder, stem + ext);
            if (System.IO.File.Exists(candidate))
            {
                return Path.GetFullPath(candidate);
            }
        }

        return null;
    }

    public static HashSet<string> CollectHawbsWithDeclarationPdf(
        string storageRoot,
        IEnumerable<(string Hawb, string? DeclarationFile)> dbRows)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        CollectDiskStems(Path.Combine(storageRoot, DeclarationPdfFolder), [".pdf"], set);

        foreach (var (hawb, declarationFile) in dbRows)
        {
            if (string.IsNullOrWhiteSpace(hawb) || string.IsNullOrWhiteSpace(declarationFile))
            {
                continue;
            }

            var item = new TariffData { HAWB = hawb, DeclarationFile = declarationFile };
            if (FindDeclarationPdfPath(storageRoot, item) is not null)
            {
                set.Add(hawb.Trim());
            }
        }

        return set;
    }

    public static HashSet<string> CollectHawbsWithCost(
        string storageRoot,
        IEnumerable<(string Hawb, string? Cost)> dbRows)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var costDir = Path.Combine(storageRoot, CostFolder);
        CollectDiskStems(costDir, [".xlsx", ".xls"], set);

        foreach (var (hawb, cost) in dbRows)
        {
            if (string.IsNullOrWhiteSpace(hawb) || string.IsNullOrWhiteSpace(cost))
            {
                continue;
            }

            var item = new TariffData { HAWB = hawb, Cost = cost };
            if (FindCostFilePath(storageRoot, item) is not null)
            {
                set.Add(hawb.Trim());
            }
        }

        return set;
    }

    public static IQueryable<TariffData> ApplyPresenceFilter(
        IQueryable<TariffData> query,
        IReadOnlyList<string> selectedValues,
        IReadOnlyCollection<string> hawbsWithFile)
    {
        var wantHas = selectedValues.Any(v =>
            string.Equals(v, PresenceHas, StringComparison.OrdinalIgnoreCase));
        var wantNone = selectedValues.Any(v =>
            string.Equals(v, PresenceNone, StringComparison.OrdinalIgnoreCase));

        if (wantHas == wantNone)
        {
            return query;
        }

        var list = hawbsWithFile.ToList();
        return wantHas
            ? query.Where(e => list.Contains(e.HAWB))
            : query.Where(e => !list.Contains(e.HAWB));
    }

    private static void CollectDiskStems(string directory, string[] extensions, HashSet<string> set)
    {
        if (!Directory.Exists(directory))
        {
            return;
        }

        foreach (var file in Directory.EnumerateFiles(directory))
        {
            var ext = Path.GetExtension(file);
            if (!extensions.Contains(ext, StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }

            var stem = Path.GetFileNameWithoutExtension(file);
            if (!string.IsNullOrWhiteSpace(stem))
            {
                set.Add(stem.Trim());
            }
        }
    }

    private static string? ResolveUnderStorage(string storageRoot, string relativeOrAbsolute)
    {
        if (string.IsNullOrWhiteSpace(relativeOrAbsolute))
        {
            return null;
        }

        var full = Path.IsPathRooted(relativeOrAbsolute)
            ? Path.GetFullPath(relativeOrAbsolute)
            : Path.GetFullPath(Path.Combine(storageRoot, relativeOrAbsolute));

        var rootFull = Path.GetFullPath(storageRoot);
        if (!full.StartsWith(rootFull, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return System.IO.File.Exists(full) ? full : null;
    }
}

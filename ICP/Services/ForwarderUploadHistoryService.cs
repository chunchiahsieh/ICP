using ICP.Data;
using ICP.Models;
using Microsoft.EntityFrameworkCore;

namespace ICP.Services;

public sealed record ForwarderUploadHistoryRow(string StoredFileName, string FileName, DateTime ImportedTime,
    string ImportedBy, int RowCount, bool FileExists);

public sealed class ForwarderUploadHistoryService(ApplicationDbContext db, IWebHostEnvironment environment,
    ForwarderDataUploadOptions options)
{
    public async Task<List<ForwarderUploadHistoryRow>> GetAsync(CancellationToken ct = default)
    {
        var current = db.ForwarderDataUploads.AsNoTracking().Select(row => new { row.FilePath, row.CreateTime, row.CreateUser });
        var archived = db.ForwarderDataUploadArchives.AsNoTracking().Select(row => new { row.FilePath, row.CreateTime, row.CreateUser });
        var batches = await current.Concat(archived).Where(row => row.FilePath != "")
            .GroupBy(row => row.FilePath)
            .Select(group => new { Path = group.Key, Time = group.Min(row => row.CreateTime), User = group.Max(row => row.CreateUser), Count = group.Count() })
            .OrderByDescending(row => row.Time).ToListAsync(ct);
        return batches.Select(batch => new ForwarderUploadHistoryRow(Path.GetFileName(batch.Path),
            OriginalName(Path.GetFileName(batch.Path)), batch.Time, batch.User ?? "", batch.Count,
            ResolvePath(Path.GetFileName(batch.Path)) is { } path && string.Equals(path, Path.GetFullPath(batch.Path), StringComparison.OrdinalIgnoreCase)
                && File.Exists(path))).ToList();
    }

    public async Task<string?> GetDownloadPathAsync(string? storedName, CancellationToken ct = default)
    {
        var path = ResolvePath(storedName);
        if (path is null || !File.Exists(path)) return null;
        return await db.ForwarderDataUploads.AnyAsync(row => row.FilePath == path, ct)
            || await db.ForwarderDataUploadArchives.AnyAsync(row => row.FilePath == path, ct) ? path : null;
    }

    private string? ResolvePath(string? name)
    {
        if (string.IsNullOrWhiteSpace(name) || name != Path.GetFileName(name)
            || name.Contains('/') || name.Contains('\\')
            || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0
            || !new[] { ".xlsx", ".xls", ".csv" }.Contains(Path.GetExtension(name), StringComparer.OrdinalIgnoreCase)) return null;
        var root = Path.GetFullPath(ForwarderDataImportService.ResolveStorageDirectory(environment, options));
        return Path.Combine(root, name);
    }

    public static string OriginalName(string name) => name.Length > 18 && name[17] == '_' && name[..17].All(char.IsDigit)
        ? name[18..] : name;
}

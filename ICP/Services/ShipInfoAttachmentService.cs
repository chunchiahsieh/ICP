using ICP.Data;
using ICP.Helpers;
using ICP.Models.Icp;
using ICP.Models.ShipInfo;
using Microsoft.EntityFrameworkCore;

namespace ICP.Services;

public sealed class ShipInfoAttachmentService
{
    public const string AttachmentType = "ICP_HEADER";
    private readonly ApplicationDbContext _db;
    private readonly IConfiguration _configuration;
    private readonly UserResourcePermissionService _permissionService;

    public ShipInfoAttachmentService(
        ApplicationDbContext db,
        IConfiguration configuration,
        UserResourcePermissionService permissionService)
    {
        _db = db;
        _configuration = configuration;
        _permissionService = permissionService;
    }

    public static string OwnerId(Guid headerId) => headerId.ToString("D");

    public async Task<IReadOnlyList<ShipInfoAttachmentDto>> ListAsync(Guid headerId, CancellationToken ct)
    {
        await RequireHeaderAccessAsync(headerId, canModify: false, ct);
        return await _db.Attachments.AsNoTracking().Where(x => x.AttachmentType == AttachmentType && x.AttachmentOwnerId == OwnerId(headerId) && !x.IsDeleted)
            .OrderBy(x => x.CreateTime).Select(x => new ShipInfoAttachmentDto { Id = x.Id, OriginalFileName = x.OriginalFileName, FileSize = x.FileSize, ContentType = x.ContentType, CreateTime = x.CreateTime, CreateUser = x.CreateUser }).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<string>> ValidateArurAsync(Guid headerId, CancellationToken ct)
    {
        var files = await _db.Attachments.AsNoTracking().Where(x => x.AttachmentType == AttachmentType && x.AttachmentOwnerId == OwnerId(headerId) && !x.IsDeleted).OrderBy(x => x.Id).ToListAsync(ct);
        if (files.Count == 0) return [];
        var root = RequireRoot(); var paths = new List<string>(files.Count); var errors = new List<string>();
        foreach (var file in files)
        {
            var path = Path.GetFullPath(Path.Combine(root, file.RelativePath));
            try { EnsureUnderRoot(root, path); } catch { errors.Add($"附件路徑無效：{file.OriginalFileName}"); continue; }
            if (!File.Exists(path)) errors.Add($"附件檔案不存在：{file.OriginalFileName}"); else paths.Add(path);
        }
        if (paths.Count > 0 && string.Join(',', paths).Length > 1000) errors.Add($"附件 UNC 路徑總長度超過 1000 字元，無法 ARUR 起案（實際 {string.Join(',', paths).Length}）。");
        return errors;
    }

    public async Task<ShipInfoAttachmentDto> UploadAsync(Guid headerId, IFormFile file, string? user, CancellationToken ct)
    {
        var header = await RequireHeaderAccessAsync(headerId, canModify: true, ct);
        if (file is null) throw new InvalidOperationException("Uploaded file is required.");
        if (file.Length <= 0) throw new InvalidOperationException("Uploaded file is empty.");
        var max = _configuration.GetValue<int?>("ArurAttachment:MaxSizeMb") ?? 50;
        if (file.Length > max * 1024L * 1024L) throw new InvalidOperationException($"File exceeds {max}MB.");
        var safeInvoice = SafeSegment(header.InvoiceNo);
        var original = Path.GetFileName(file.FileName);
        if (string.IsNullOrWhiteSpace(original) || original.Length > 255 || !string.Equals(original, file.FileName, StringComparison.Ordinal)
            || original.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || original.Contains('/') || original.Contains('\\'))
        {
            throw new InvalidOperationException("Invalid file name.");
        }

        var extension = Path.GetExtension(original).ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(extension) || !GetAllowedExtensions().Contains(extension))
        {
            throw new InvalidOperationException($"File extension '{extension}' is not allowed.");
        }

        var stored = $"{Guid.NewGuid():N}_{original}";
        if (stored.Length > 255) stored = $"{Guid.NewGuid():N}{Path.GetExtension(original)}";
        var relative = Path.Combine(_configuration["ArurAttachment:RelativeFolder"] ?? "ATTACHED_FILE", safeInvoice, stored);
        var root = RequireRoot(); var full = Path.GetFullPath(Path.Combine(root, relative)); EnsureUnderRoot(root, full);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        try
        {
            await using (var stream = File.Create(full)) await file.CopyToAsync(stream, ct);
            var entity = new Attachment { Id = Guid.NewGuid(), AttachmentType = AttachmentType, AttachmentOwnerId = OwnerId(headerId), OriginalFileName = original, StoredFileName = stored, RelativePath = relative, FileSize = file.Length, ContentType = GetSafeContentType(extension), CreateTime = DateTime.Now, CreateUser = user, IsDeleted = false };
            _db.Attachments.Add(entity); await _db.SaveChangesAsync(ct);
            return new ShipInfoAttachmentDto { Id = entity.Id, OriginalFileName = entity.OriginalFileName, FileSize = entity.FileSize, ContentType = entity.ContentType, CreateTime = entity.CreateTime, CreateUser = entity.CreateUser };
        }
        catch
        {
            if (File.Exists(full)) File.Delete(full);
            throw;
        }
    }

    public async Task<(Attachment item, string path)> RequireActiveAsync(Guid headerId, Guid id, CancellationToken ct)
    {
        await RequireHeaderAccessAsync(headerId, canModify: false, ct);
        var item = await _db.Attachments.FirstOrDefaultAsync(x => x.Id == id && x.AttachmentType == AttachmentType && x.AttachmentOwnerId == OwnerId(headerId) && !x.IsDeleted, ct) ?? throw new KeyNotFoundException("Attachment not found.");
        var root = RequireRoot(); var path = Path.GetFullPath(Path.Combine(root, item.RelativePath)); EnsureUnderRoot(root, path); return (item, path);
    }
    public async Task DeleteAsync(Guid headerId, Guid id, string? user, CancellationToken ct)
    {
        await RequireHeaderAccessAsync(headerId, canModify: true, ct);
        var item = await _db.Attachments.FirstOrDefaultAsync(x => x.Id == id && x.AttachmentType == AttachmentType && x.AttachmentOwnerId == OwnerId(headerId) && !x.IsDeleted, ct) ?? throw new KeyNotFoundException("Attachment not found.");
        item.IsDeleted = true; item.UpdateTime = DateTime.Now; item.UpdateUser = user; await _db.SaveChangesAsync(ct);
    }

    private async Task<IcpHeader> RequireHeaderAccessAsync(Guid headerId, bool canModify, CancellationToken ct)
    {
        var permission = canModify ? ShipInfoPermissionCodes.Edit : ShipInfoPermissionCodes.View;
        if (!_permissionService.HasPermission(permission)) throw new ShipInfoForbiddenException("Permission denied.");
        var header = await _db.IcpHeaders.AsNoTracking().FirstOrDefaultAsync(x => x.Id == headerId, ct)
            ?? throw new ShipInfoNotFoundException("Header not found.");
        if (canModify && !ShipInfoStatusRules.Resolve(ShipInfoStatusResolver.Resolve(header)).Edit)
        {
            throw new ShipInfoBusinessException("Header attachments cannot be modified in current status.");
        }
        return header;
    }

    private HashSet<string> GetAllowedExtensions()
    {
        var configured = _configuration.GetSection("ArurAttachment:AllowedExtensions").Get<string[]>();
        return (configured is { Length: > 0 } ? configured : [".pdf", ".doc", ".docx", ".xls", ".xlsx", ".csv", ".txt", ".zip", ".png", ".jpg", ".jpeg"])
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.StartsWith('.') ? x.ToLowerInvariant() : "." + x.ToLowerInvariant())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private static string GetSafeContentType(string extension) => extension switch
    {
        ".pdf" => "application/pdf", ".doc" => "application/msword", ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        ".xls" => "application/vnd.ms-excel", ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        ".csv" => "text/csv", ".txt" => "text/plain", ".zip" => "application/zip", ".png" => "image/png",
        ".jpg" or ".jpeg" => "image/jpeg", _ => "application/octet-stream"
    };
    private string RequireRoot() => _configuration["ArurAttachment:IpcStorageRoot"]?.TrimEnd('\\', '/') ?? throw new InvalidOperationException("ArurAttachment:IpcStorageRoot is required.");
    private static string SafeSegment(string value) { var x = string.Concat(value.Where(c => !Path.GetInvalidFileNameChars().Contains(c) && c != '.' && c != ' ')); if (string.IsNullOrWhiteSpace(x)) throw new InvalidOperationException("Invalid invoice number for attachment path."); return x; }
    private static void EnsureUnderRoot(string root, string path) { var fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar; if (!path.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Invalid attachment path."); }
}

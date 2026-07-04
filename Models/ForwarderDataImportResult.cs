namespace ICP.Models;

public class ForwarderDataImportResult
{
    public bool Success { get; init; }

    public string Message { get; init; } = string.Empty;

    public int ImportedCount { get; init; }

    public int SkippedCount { get; init; }

    public string? FilePath { get; init; }

    public IReadOnlyList<string> Errors { get; init; } = [];

    public bool RequiresOverwriteConfirmation { get; init; }

    public IReadOnlyList<string> DuplicateInvoiceNos { get; init; } = [];

    public int OverwrittenCount { get; init; }

    public static ForwarderDataImportResult Ok(int importedCount, string filePath, int skippedCount = 0) =>
        new()
        {
            Success = true,
            ImportedCount = importedCount,
            SkippedCount = skippedCount,
            FilePath = filePath,
            Message = $"成功匯入 {importedCount} 筆"
        };

    public static ForwarderDataImportResult PreviewOk(int rowCount, string filePath) =>
        new()
        {
            Success = true,
            ImportedCount = rowCount,
            FilePath = filePath,
            Message = $"已解析 {rowCount} 筆，請確認後按儲存"
        };

    public static ForwarderDataImportResult SaveOk(int importedCount, string filePath) =>
        new()
        {
            Success = true,
            ImportedCount = importedCount,
            FilePath = filePath,
            Message = $"成功儲存 {importedCount} 筆"
        };

    public static ForwarderDataImportResult Fail(string message, IReadOnlyList<string>? errors = null) =>
        new()
        {
            Success = false,
            Message = message,
            Errors = errors ?? []
        };

    public static ForwarderDataImportResult NeedOverwriteConfirmation(IReadOnlyList<string> duplicateInvoiceNos) =>
        new()
        {
            Success = false,
            RequiresOverwriteConfirmation = true,
            DuplicateInvoiceNos = duplicateInvoiceNos,
            Message = "duplicate invoice"
        };
}

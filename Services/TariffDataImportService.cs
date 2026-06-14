using System.Data;
using System.Text;
using ExcelDataReader;
using ICP.Data;
using ICP.Helpers;
using ICP.Models;
using ICP.Models.Icp;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ICP.Services;

public class TariffDataImportService
{
    static TariffDataImportService()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    private readonly ApplicationDbContext _db;
    private readonly TariffDataOptions _options;

    public TariffDataImportService(ApplicationDbContext db, IOptions<TariffDataOptions> options)
    {
        _db = db;
        _options = options.Value;
    }

    public static string ResolveStorageDirectory(IWebHostEnvironment environment, TariffDataOptions options, string subFolder)
    {
        var root = Path.IsPathRooted(options.StoragePath)
            ? options.StoragePath
            : Path.Combine(environment.ContentRootPath, options.StoragePath);

        return Path.GetFullPath(Path.Combine(root, subFolder));
    }

    public static string ValidateAndNormalizeStoredFilePath(
        string storedFilePath,
        IWebHostEnvironment environment,
        TariffDataOptions options,
        string subFolder)
    {
        var uploadDirectory = ResolveStorageDirectory(environment, options, subFolder);
        var normalizedPath = Path.GetFullPath(storedFilePath.Trim());

        if (!normalizedPath.StartsWith(uploadDirectory, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("檔案路徑無效");
        }

        if (!System.IO.File.Exists(normalizedPath))
        {
            throw new InvalidOperationException("檔案不存在");
        }

        return normalizedPath;
    }

    public async Task<TariffDataImportResult> ImportCustomsDataAsync(
        string storedFilePath,
        string importFileName,
        string createUser,
        CancellationToken cancellationToken = default)
    {
        var broker = TariffCustomsImportRules.ResolveBroker(importFileName, _options);
        var rows = ParseCustomsExcel(storedFilePath, importFileName, broker);
        if (rows.Count == 0)
        {
            throw new InvalidOperationException("檔案中沒有可匯入的資料列");
        }

        var duplicateInvoices = rows
            .GroupBy(r => r.InvoiceNumber, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .Take(5)
            .ToList();

        if (duplicateInvoices.Count > 0)
        {
            throw new InvalidOperationException($"檔案內 Invoice Number 重複：{string.Join("、", duplicateInvoices)}");
        }

        var invoiceNumbers = rows.Select(r => r.InvoiceNumber).ToList();
        var existingInvoices = await _db.TariffDataRecords
            .AsNoTracking()
            .Where(x => invoiceNumbers.Contains(x.InvoiceNumber))
            .Select(x => x.InvoiceNumber)
            .ToListAsync(cancellationToken);

        var existingInvoiceSet = new HashSet<string>(existingInvoices, StringComparer.OrdinalIgnoreCase);
        var tariffDataExists = await _db.TariffDataRecords.AnyAsync(cancellationToken);
        var knownHawbs = tariffDataExists
            ? await _db.TariffDataRecords
                .AsNoTracking()
                .Select(x => x.HAWB)
                .Distinct()
                .ToListAsync(cancellationToken)
            : [];

        var hawbErrors = new List<string>();
        TariffCustomsImportRules.ValidateNewRowHawbs(
            rows,
            existingInvoiceSet,
            new HashSet<string>(knownHawbs, StringComparer.OrdinalIgnoreCase),
            tariffDataExists,
            hawbErrors);
        TariffCustomsImportRules.ThrowIfErrors(hawbErrors);

        var existing = await _db.TariffDataRecords
            .Where(x => invoiceNumbers.Contains(x.InvoiceNumber))
            .ToDictionaryAsync(x => x.InvoiceNumber, StringComparer.OrdinalIgnoreCase, cancellationToken);

        var importedCount = 0;
        var updatedCount = 0;
        var now = DateTime.Now;
        var resolvedUser = TruncateUser(createUser);

        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            foreach (var row in rows)
            {
                if (existing.TryGetValue(row.InvoiceNumber, out var entity))
                {
                    TariffCustomsImportRules.ApplyImportRow(entity, row);
                    entity.UpdateTime = now;
                    entity.UpdateUser = resolvedUser;
                    updatedCount++;
                }
                else
                {
                    row.CreateTime = now;
                    row.CreateUser = resolvedUser;
                    _db.TariffDataRecords.Add(row);
                    importedCount++;
                }
            }

            await _db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return new TariffDataImportResult
            {
                ImportedCount = importedCount,
                UpdatedCount = updatedCount
            };
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private static List<TariffData> ParseCustomsExcel(string storedFilePath, string importFileName, string broker)
    {
        var extension = Path.GetExtension(storedFilePath);
        if (!extension.Equals(".xlsx", StringComparison.OrdinalIgnoreCase)
            && !extension.Equals(".xls", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("不支援的檔案格式");
        }

        using var stream = System.IO.File.OpenRead(storedFilePath);
        using var reader = ExcelReaderFactory.CreateReader(stream);
        var dataSet = reader.AsDataSet(new ExcelDataSetConfiguration
        {
            ConfigureDataTable = _ => new ExcelDataTableConfiguration
            {
                UseHeaderRow = true
            }
        });

        if (dataSet.Tables.Count == 0)
        {
            return [];
        }

        var table = dataSet.Tables[0];
        var errors = new List<string>();
        var columnMap = BuildColumnMapFromDataTable(table);
        TariffCustomsImportRules.ValidateRequiredHeaders(columnMap, errors);
        TariffCustomsImportRules.ThrowIfErrors(errors);

        var importBatchId = Guid.NewGuid();
        var createDate = DateOnly.FromDateTime(DateTime.Today);
        var rows = new List<TariffData>();
        var rowNumber = 1;

        foreach (DataRow dataRow in table.Rows)
        {
            rowNumber++;
            var values = ReadDataTableRow(dataRow, table.Columns.Count);
            if (IsEmptyRow(values))
            {
                continue;
            }

            rows.Add(TariffCustomsImportRules.MapRow(
                values,
                columnMap,
                importFileName,
                importBatchId,
                createDate,
                broker,
                rowNumber,
                errors));
        }

        TariffCustomsImportRules.ThrowIfErrors(errors);
        return rows;
    }

    private static Dictionary<string, int> BuildColumnMap(IReadOnlyList<string> headers)
    {
        var columnMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < headers.Count; i++)
        {
            if (!TariffExcelColumnMap.TryResolveProperty(headers[i], out var propertyName))
            {
                continue;
            }

            columnMap[propertyName] = i;
        }

        return columnMap;
    }

    private static Dictionary<string, int> BuildColumnMapFromDataTable(DataTable table)
    {
        var headers = new List<string>(table.Columns.Count);
        for (var i = 0; i < table.Columns.Count; i++)
        {
            headers.Add(table.Columns[i].ColumnName);
        }

        return BuildColumnMap(headers);
    }

    private static List<string> ReadDataTableRow(DataRow dataRow, int columnCount)
    {
        var values = new List<string>(columnCount);
        for (var i = 0; i < columnCount; i++)
        {
            var raw = dataRow[i];
            values.Add(raw == DBNull.Value ? string.Empty : TariffCustomsImportRules.FormatCellValue(raw));
        }

        return values;
    }

    private static bool IsEmptyRow(IReadOnlyList<string> values) =>
        values.All(string.IsNullOrEmpty);

    private static string TruncateUser(string user)
    {
        var resolved = CrudAuditHelper.ResolveUserName(user);
        return resolved.Length <= 50 ? resolved : resolved[..50];
    }
}

using System.Data;
using System.Globalization;
using System.Text;
using ExcelDataReader;
using ICP.Data;
using ICP.Helpers;
using ICP.Models;
using ICP.Models.Icp;
using Microsoft.EntityFrameworkCore;

namespace ICP.Services;

public class ForwarderDataImportService
{
    static ForwarderDataImportService()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    private readonly ApplicationDbContext _db;

    public ForwarderDataImportService(ApplicationDbContext db)
    {
        _db = db;
    }

    public static string ResolveStorageDirectory(IWebHostEnvironment environment, ForwarderDataUploadOptions options)
    {
        if (Path.IsPathRooted(options.StoragePath))
        {
            return options.StoragePath;
        }

        return Path.Combine(environment.ContentRootPath, options.StoragePath);
    }

    public static string ValidateAndNormalizeStoredFilePath(
        string storedFilePath,
        IWebHostEnvironment environment,
        ForwarderDataUploadOptions options)
    {
        var uploadDirectory = Path.GetFullPath(ResolveStorageDirectory(environment, options));
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

    public async Task<List<ForwarderDataUpload>> ParseAsync(
        string storedFilePath,
        string createUser,
        CancellationToken cancellationToken = default)
    {
        var extension = Path.GetExtension(storedFilePath);
        List<ForwarderDataUpload> rows;

        await using (var stream = System.IO.File.OpenRead(storedFilePath))
        {
            rows = extension.ToLowerInvariant() switch
            {
                ".xlsx" or ".xls" => ParseExcelRows(stream, storedFilePath, createUser),
                ".csv" => await ParseCsvRowsAsync(stream, storedFilePath, createUser, cancellationToken),
                _ => throw new InvalidOperationException("不支援的檔案格式")
            };
        }

        if (rows.Count == 0)
        {
            throw new InvalidOperationException("檔案中沒有可匯入的資料列");
        }

        var consistencyErrors = new List<string>();
        ForwarderImportRules.ValidatePerInvoiceFieldConsistency(rows, consistencyErrors);
        ThrowIfErrors(consistencyErrors);

        return rows;
    }

    public async Task<IReadOnlyList<string>> GetDuplicateInvoiceNumbersAsync(
        IEnumerable<string> invoiceNumbers,
        CancellationToken cancellationToken = default)
    {
        var incoming = invoiceNumbers
            .Where(invoice => !string.IsNullOrWhiteSpace(invoice))
            .Select(invoice => invoice.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (incoming.Count == 0)
        {
            return [];
        }

        var existingInvoices = await _db.ForwarderDataUploads
            .AsNoTracking()
            .Select(x => x.InvoiceNo)
            .Distinct()
            .ToListAsync(cancellationToken);

        return ForwarderImportRules.FindDuplicateInvoiceNumbers(incoming, existingInvoices);
    }

    public async Task<ForwarderDataImportResult> SaveAsync(
        string storedFilePath,
        string createUser,
        bool confirmOverwrite = false,
        CancellationToken cancellationToken = default)
    {
        var rows = await ParseAsync(storedFilePath, createUser, cancellationToken);
        var duplicateInvoices = await GetDuplicateInvoiceNumbersAsync(
            rows.Select(row => row.InvoiceNo),
            cancellationToken);

        if (duplicateInvoices.Count > 0 && !confirmOverwrite)
        {
            return ForwarderDataImportResult.NeedOverwriteConfirmation(duplicateInvoices);
        }

        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var overwrittenCount = 0;
            if (duplicateInvoices.Count > 0)
            {
                overwrittenCount = await ArchiveAndRemoveByInvoicesAsync(
                    duplicateInvoices,
                    storedFilePath,
                    createUser,
                    cancellationToken);
            }

            _db.ForwarderDataUploads.AddRange(rows);
            await _db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new ForwarderDataImportResult
            {
                Success = true,
                ImportedCount = rows.Count,
                OverwrittenCount = overwrittenCount,
                FilePath = storedFilePath,
                Message = $"成功儲存 {rows.Count} 筆"
            };
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private async Task<int> ArchiveAndRemoveByInvoicesAsync(
        IReadOnlyList<string> invoiceNumbers,
        string replacedByFilePath,
        string removedUser,
        CancellationToken cancellationToken)
    {
        var invoiceSet = new HashSet<string>(invoiceNumbers, StringComparer.OrdinalIgnoreCase);
        var existingRows = await _db.ForwarderDataUploads
            .Where(row => invoiceNumbers.Contains(row.InvoiceNo))
            .ToListAsync(cancellationToken);

        existingRows = existingRows
            .Where(row => invoiceSet.Contains(row.InvoiceNo))
            .ToList();

        if (existingRows.Count == 0)
        {
            return 0;
        }

        var removedTime = DateTime.Now;
        var resolvedUser = TruncateUser(removedUser);
        var archives = existingRows
            .Select(row => ForwarderDataUploadArchive.FromEntity(row, removedTime, resolvedUser, replacedByFilePath))
            .ToList();

        _db.ForwarderDataUploadArchives.AddRange(archives);
        _db.ForwarderDataUploads.RemoveRange(existingRows);
        await _db.SaveChangesAsync(cancellationToken);
        return existingRows.Count;
    }

    public Task<ForwarderDataImportResult> ImportAsync(
        string storedFilePath,
        string createUser,
        bool confirmOverwrite = false,
        CancellationToken cancellationToken = default) =>
        SaveAsync(storedFilePath, createUser, confirmOverwrite, cancellationToken);

    public async Task<IReadOnlyList<ForwarderDataUploadRowViewModel>> BuildRowViewModelsAsync(
        IReadOnlyList<ForwarderDataUpload> rows,
        bool preview,
        CancellationToken cancellationToken = default)
    {
        var inFileMultiLineCounts = rows
            .GroupBy(BuildInFileMultiLineKey, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.OrdinalIgnoreCase);

        HashSet<string> dbDuplicateSet;
        if (preview)
        {
            var duplicateInvoices = await GetDuplicateInvoiceNumbersAsync(
                rows.Select(row => row.InvoiceNo),
                cancellationToken);
            dbDuplicateSet = new HashSet<string>(duplicateInvoices, StringComparer.OrdinalIgnoreCase);
        }
        else
        {
            dbDuplicateSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        return rows
            .Select(row =>
            {
                var invoiceNo = row.InvoiceNo.Trim();
                var inFileKey = BuildInFileMultiLineKey(row);
                return new ForwarderDataUploadRowViewModel
                {
                    Row = row,
                    IsDbDuplicate = dbDuplicateSet.Contains(invoiceNo),
                    IsInFileMultiLine = inFileMultiLineCounts.TryGetValue(inFileKey, out var count) && count > 1
                };
            })
            .ToList();
    }

    private static string BuildInFileMultiLineKey(ForwarderDataUpload row) =>
        $"{row.InvoiceNo.Trim()}|{row.MaterialCode?.Trim() ?? string.Empty}";

    public static IReadOnlyList<ForwarderDataUploadRowViewModel> ApplyDuplicateStatusFilter(
        IReadOnlyList<ForwarderDataUploadRowViewModel> rows,
        IReadOnlyList<string> duplicateStatuses)
    {
        if (duplicateStatuses.Count == 0)
        {
            return rows;
        }

        var statusSet = new HashSet<string>(
            duplicateStatuses.Where(status => !string.IsNullOrWhiteSpace(status)),
            StringComparer.OrdinalIgnoreCase);

        if (statusSet.Count == 0)
        {
            return rows;
        }

        return rows
            .Where(row =>
                (statusSet.Contains("DbDuplicate") && row.IsDbDuplicate)
                || (statusSet.Contains("InFileMultiLine") && row.IsInFileMultiLine)
                || (statusSet.Contains("None") && !row.IsDbDuplicate && !row.IsInFileMultiLine))
            .ToList();
    }

    private static List<ForwarderDataUpload> ParseExcelRows(Stream stream, string filePath, string createUser)
    {
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
        var columnMap = BuildColumnMapFromDataTable(table, errors);
        ThrowIfErrors(errors);

        var rows = new List<ForwarderDataUpload>();
        var rowNumber = 1;
        foreach (DataRow dataRow in table.Rows)
        {
            rowNumber++;
            var values = ReadDataTableRow(dataRow, table.Columns.Count);
            if (IsEmptyRow(values))
            {
                continue;
            }

            rows.Add(MapRow(values, columnMap, filePath, createUser, rowNumber, errors));
        }

        ThrowIfErrors(errors);
        return rows;
    }

    private static async Task<List<ForwarderDataUpload>> ParseCsvRowsAsync(
        Stream stream,
        string filePath,
        string createUser,
        CancellationToken cancellationToken)
    {
        using var textReader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        var errors = new List<string>();
        var rows = new List<ForwarderDataUpload>();

        var headerLine = await textReader.ReadLineAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(headerLine))
        {
            return rows;
        }

        var columnMap = BuildColumnMap(ParseCsvLine(headerLine), errors);
        ThrowIfErrors(errors);

        var rowNumber = 1;
        while (true)
        {
            var line = await textReader.ReadLineAsync(cancellationToken);
            if (line is null)
            {
                break;
            }

            rowNumber++;
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var values = ParseCsvLine(line);
            if (IsEmptyRow(values))
            {
                continue;
            }

            rows.Add(MapRow(values, columnMap, filePath, createUser, rowNumber, errors));
        }

        ThrowIfErrors(errors);
        return rows;
    }

    private static Dictionary<string, int> BuildColumnMap(IReadOnlyList<string> headers, List<string> errors)
    {
        var columnMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < headers.Count; i++)
        {
            if (!ForwarderExcelColumnMap.TryResolveProperty(headers[i], out var propertyName))
            {
                continue;
            }

            columnMap[propertyName] = i;
        }

        if (!columnMap.ContainsKey(ForwarderExcelColumnMap.Type)
            || !columnMap.ContainsKey(ForwarderExcelColumnMap.InvoiceNo))
        {
            errors.Add("標題列必須包含 Type 與 InvoiceNo 欄位");
        }

        return columnMap;
    }

    private static Dictionary<string, int> BuildColumnMapFromDataTable(DataTable table, List<string> errors)
    {
        var headers = new List<string>(table.Columns.Count);
        for (var i = 0; i < table.Columns.Count; i++)
        {
            headers.Add(table.Columns[i].ColumnName);
        }

        return BuildColumnMap(headers, errors);
    }

    private static List<string> ReadDataTableRow(DataRow dataRow, int columnCount)
    {
        var values = new List<string>(columnCount);
        for (var i = 0; i < columnCount; i++)
        {
            var raw = dataRow[i];
            values.Add(raw == DBNull.Value ? string.Empty : FormatCellValue(raw));
        }

        return values;
    }

    private static ForwarderDataUpload MapRow(
        IReadOnlyList<string> values,
        IReadOnlyDictionary<string, int> columnMap,
        string filePath,
        string createUser,
        int rowNumber,
        List<string> errors)
    {
        var type = GetCellValue(values, columnMap, ForwarderExcelColumnMap.Type);
        var invoiceNo = GetCellValue(values, columnMap, ForwarderExcelColumnMap.InvoiceNo);

        if (string.IsNullOrEmpty(type) || string.IsNullOrEmpty(invoiceNo))
        {
            errors.Add($"第 {rowNumber} 列缺少必填欄位 Type 或 InvoiceNo");
        }

        decimal? quantity = ParseDecimalValue(
            GetCellValue(values, columnMap, ForwarderExcelColumnMap.Quantity),
            rowNumber,
            "Quantity",
            errors);

        return new ForwarderDataUpload
        {
            Type = type ?? string.Empty,
            InvoiceNo = invoiceNo ?? string.Empty,
            CustomerReference = TrimToNull(GetCellValue(values, columnMap, ForwarderExcelColumnMap.CustomerReference), 100),
            MaterialCode = TrimToNull(GetCellValue(values, columnMap, ForwarderExcelColumnMap.MaterialCode), 100),
            OrderMaterialName = TrimToNull(GetCellValue(values, columnMap, ForwarderExcelColumnMap.OrderMaterialName), 500),
            Quantity = quantity,
            PortOfLoading = TrimToNull(GetCellValue(values, columnMap, ForwarderExcelColumnMap.PortOfLoading), 100),
            ShipToName = TrimToNull(GetCellValue(values, columnMap, ForwarderExcelColumnMap.ShipToName), 300),
            ShipToAddress = TrimToNull(GetCellValue(values, columnMap, ForwarderExcelColumnMap.ShipToAddress), null),
            ShipToPartyCountryCode = TrimToNull(GetCellValue(values, columnMap, ForwarderExcelColumnMap.ShipToPartyCountryCode), 100),
            ShipToPortCode = TrimToNull(GetCellValue(values, columnMap, ForwarderExcelColumnMap.ShipToPortCode), 50),
            FreightCharge = TrimToNull(GetCellValue(values, columnMap, ForwarderExcelColumnMap.FreightCharge), 100),
            ConfirmedCustomDate = ParseDateValue(
                GetCellValue(values, columnMap, ForwarderExcelColumnMap.ConfirmedCustomDate),
                rowNumber,
                "Confirmed Custom Date",
                errors),
            Hawb = TrimToNull(GetCellValue(values, columnMap, ForwarderExcelColumnMap.Hawb), 50),
            Mawb = TrimToNull(GetCellValue(values, columnMap, ForwarderExcelColumnMap.Mawb), 50),
            Etd = ParseDateValue(
                GetCellValue(values, columnMap, ForwarderExcelColumnMap.Etd),
                rowNumber,
                "ETD",
                errors),
            Eta = ParseDateValue(
                GetCellValue(values, columnMap, ForwarderExcelColumnMap.Eta),
                rowNumber,
                "ETA",
                errors),
            Flight1 = TrimToNull(GetCellValue(values, columnMap, ForwarderExcelColumnMap.Flight1), 50),
            Flight2 = TrimToNull(GetCellValue(values, columnMap, ForwarderExcelColumnMap.Flight2), 50),
            Cb = TrimToNull(GetCellValue(values, columnMap, ForwarderExcelColumnMap.Cb), 50),
            Action = TrimToNull(GetCellValue(values, columnMap, ForwarderExcelColumnMap.Action), 100),
            Mdp = TrimToNull(GetCellValue(values, columnMap, ForwarderExcelColumnMap.Mdp), 50),
            FilePath = filePath,
            CreateTime = DateTime.Now,
            CreateUser = TruncateUser(createUser)
        };
    }

    private static decimal? ParseDecimalValue(string? text, int rowNumber, string fieldName, List<string> errors)
    {
        if (string.IsNullOrEmpty(text))
        {
            return null;
        }

        if (decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed)
            || decimal.TryParse(text, NumberStyles.Number, CultureInfo.CurrentCulture, out parsed))
        {
            return parsed;
        }

        errors.Add($"第 {rowNumber} 列 {fieldName} 格式不正確");
        return null;
    }

    private static DateTime? ParseDateValue(string? text, int rowNumber, string fieldName, List<string> errors)
    {
        if (string.IsNullOrEmpty(text))
        {
            return null;
        }

        if (DateTime.TryParse(text, CultureInfo.CurrentCulture, DateTimeStyles.None, out var parsed)
            || DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out parsed))
        {
            return parsed.Date;
        }

        if (double.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out var oaDate)
            && oaDate is > 0 and < 100000)
        {
            try
            {
                return DateTime.FromOADate(oaDate).Date;
            }
            catch (ArgumentException)
            {
            }
        }

        errors.Add($"第 {rowNumber} 列 {fieldName} 日期格式不正確");
        return null;
    }

    private static string? GetCellValue(
        IReadOnlyList<string> values,
        IReadOnlyDictionary<string, int> columnMap,
        string propertyName)
    {
        if (!columnMap.TryGetValue(propertyName, out var index) || index >= values.Count)
        {
            return null;
        }

        return NormalizeCellText(values[index]);
    }

    private static bool IsEmptyRow(IReadOnlyList<string> values) =>
        values.All(string.IsNullOrEmpty);

    private static string NormalizeCellText(string? value) =>
        string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();

    private static string FormatCellValue(object? value)
    {
        if (value is null or DBNull)
        {
            return string.Empty;
        }

        if (value is DateTime dateTime)
        {
            return dateTime.ToString("yyyy/MM/dd", CultureInfo.InvariantCulture);
        }

        if (value is double number && number is > 0 and < 100000)
        {
            try
            {
                return DateTime.FromOADate(number).ToString("yyyy/MM/dd", CultureInfo.InvariantCulture);
            }
            catch (ArgumentException)
            {
            }
        }

        return NormalizeCellText(value.ToString());
    }

    private static string? TrimToNull(string? value, int? maxLength)
    {
        var normalized = NormalizeCellText(value);
        if (string.IsNullOrEmpty(normalized))
        {
            return null;
        }

        var trimmed = normalized;
        if (maxLength.HasValue && trimmed.Length > maxLength.Value)
        {
            trimmed = trimmed[..maxLength.Value];
        }

        return trimmed;
    }

    private static string TruncateUser(string user)
    {
        var resolved = CrudAuditHelper.ResolveUserName(user);
        return resolved.Length <= 50 ? resolved : resolved[..50];
    }

    private static void ThrowIfErrors(List<string> errors)
    {
        if (errors.Count > 0)
        {
            throw new InvalidOperationException(string.Join("；", errors));
        }
    }

    private static List<string> ParseCsvLine(string line)
    {
        var values = new List<string>();
        var current = new StringBuilder();
        var inQuotes = false;

        for (var i = 0; i < line.Length; i++)
        {
            var ch = line[i];
            if (ch == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    current.Append('"');
                    i++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }

                continue;
            }

            if (ch == ',' && !inQuotes)
            {
                values.Add(NormalizeCellText(current.ToString()));
                current.Clear();
                continue;
            }

            current.Append(ch);
        }

        values.Add(NormalizeCellText(current.ToString()));
        return values;
    }
}

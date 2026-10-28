using System.Data;
using System.Globalization;
using System.Text;
using ExcelDataReader;
using ICP.Data;
using ICP.Helpers;
using ICP.Models.Icp;
using ICP.Models.MassUpdate;
using Microsoft.EntityFrameworkCore;

namespace ICP.Services;

public class MassUpdateImportService
{
    private readonly ApplicationDbContext _db;

    static MassUpdateImportService()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    public MassUpdateImportService(ApplicationDbContext db)
    {
        _db = db;
    }

    public static string ResolveStorageDirectory(IWebHostEnvironment environment) =>
        Path.GetFullPath(Path.Combine(environment.ContentRootPath, "uploads", "massupdate"));

    public static string ValidateAndNormalizeStoredFilePath(string storedFilePath, IWebHostEnvironment environment)
    {
        var directory = ResolveStorageDirectory(environment)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        var path = Path.GetFullPath(storedFilePath.Trim());
        if (!path.StartsWith(directory, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("檔案路徑無效");
        }

        if (!File.Exists(path))
        {
            throw new InvalidOperationException("檔案不存在");
        }

        return path;
    }

    public async Task<List<MassUpdateRow>> ParseAsync(
        string storedFilePath,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await using var stream = File.OpenRead(storedFilePath);
        var rows = Parse(stream, Path.GetExtension(storedFilePath));
        if (rows.Count == 0)
        {
            throw new InvalidOperationException("檔案中沒有可更新的資料列");
        }

        await ValidateSettingsAsync(rows, cancellationToken);
        return rows;
    }

    public async Task ValidateSettingsAsync(
        IReadOnlyList<MassUpdateRow> rows,
        CancellationToken cancellationToken = default)
    {
        var settings = await _db.SystemConfigs.AsNoTracking()
            .Where(config => config.Category == "Broker" || config.Category == "Shipper")
            .Select(config => new { config.Category, config.Key1 })
            .ToListAsync(cancellationToken);
        var errors = new List<string>();
        foreach (var category in new[] { "Broker", "Shipper" })
        {
            var valid = settings.Where(config => config.Category == category)
                .Select(config => config.Key1.Trim())
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var row in rows)
            {
                var value = category == "Broker" ? row.Broker : row.Shipper;
                if (!string.IsNullOrWhiteSpace(value) && !valid.Contains(value.Trim()))
                    errors.Add($"第 {row.RowNumber} 列 {category}「{value}」不存在於 Settings → {category} 的 Key，請先新增設定後再上傳。");
            }
        }
        AddDiSaImportRules.ThrowIfErrors(errors);
    }

    public async Task<List<MassUpdatePreviewRow>> BuildPreviewRowsAsync(
        IReadOnlyList<MassUpdateRow> rows,
        CancellationToken cancellationToken = default)
    {
        var invoices = rows.Select(row => row.InvoiceNo)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var headers = await _db.IcpHeaders.AsNoTracking()
            .Where(header => invoices.Contains(header.InvoiceNo))
            .ToListAsync(cancellationToken);
        var byInvoice = headers
            .GroupBy(header => header.InvoiceNo, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.OrdinalIgnoreCase);
        var duplicateInvoices = rows.GroupBy(row => row.InvoiceNo, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return rows.Select(row =>
        {
            byInvoice.TryGetValue(row.InvoiceNo, out var matches);
            matches ??= [];
            return new MassUpdatePreviewRow
            {
                Row = row,
                MatchedHeaderCount = matches.Count,
                IsDuplicateInFile = duplicateInvoices.Contains(row.InvoiceNo),
                DbNcdrNo = JoinDistinct(matches.Select(header => header.NcdrNo)),
                DbOwner = JoinDistinct(matches.Select(header => header.Owner)),
                DbEndUser = JoinDistinct(matches.Select(header => header.EndUser))
            };
        }).ToList();
    }

    public async Task<MassUpdateResult> SaveAsync(
        string storedFilePath,
        string? userName,
        CancellationToken cancellationToken = default)
    {
        var rows = await ParseAsync(storedFilePath, cancellationToken);
        var duplicate = rows.GroupBy(row => row.InvoiceNo, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicate is not null)
        {
            throw new InvalidOperationException($"Excel 內 Invoice No 重複：{duplicate.Key}");
        }

        var invoices = rows.Select(row => row.InvoiceNo).ToList();
        var headers = await _db.IcpHeaders
            .Where(header => invoices.Contains(header.InvoiceNo))
            .ToListAsync(cancellationToken);
        var rowsByInvoice = rows.ToDictionary(row => row.InvoiceNo, StringComparer.OrdinalIgnoreCase);
        var matchedInvoices = headers.Select(header => header.InvoiceNo)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var notFoundInvoices = invoices
            .Where(invoiceNo => !matchedInvoices.Contains(invoiceNo))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (notFoundInvoices.Count > 0)
        {
            throw new InvalidOperationException(
                $"ICP_HEADER 查無 Invoice No：{string.Join(", ", notFoundInvoices)}");
        }

        var actor = CrudAuditHelper.ResolveUserName(userName);
        var now = DateTime.Now;
        var auditLogs = new List<ShipInfoAuditLog>();
        var changedHeaderCount = 0;

        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            foreach (var header in headers)
            {
                var row = rowsByInvoice[header.InvoiceNo];
                var changes = ApplyValues(header, row);
                if (changes.Count == 0)
                {
                    continue;
                }

                changedHeaderCount++;
                CrudAuditHelper.ApplyUpdateAudit(header, userName);
                var rowKey = ShipInfoKeyHelper.BuildHeaderRowKey(header);
                var headerKey = ShipInfoKeyHelper.BuildHeaderKey(header);
                auditLogs.AddRange(changes.Select(change => new ShipInfoAuditLog
                {
                    EntityType = "Header",
                    EntityKey = rowKey,
                    HeaderKey = headerKey,
                    Action = "Update",
                    FieldName = change.FieldName,
                    OldValue = change.OldValue,
                    NewValue = change.NewValue,
                    UserName = actor,
                    ActionTime = now,
                    CreateTime = now,
                    CreateUser = actor
                }));
            }

            if (auditLogs.Count > 0)
            {
                _db.ShipInfoAuditLogs.AddRange(auditLogs);
            }

            await _db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }

        return new MassUpdateResult
        {
            UpdatedHeaderCount = changedHeaderCount,
            MatchedExcelRowCount = matchedInvoices.Count,
            NotFoundExcelRowCount = rows.Count - matchedInvoices.Count
        };
    }

    private static List<MassUpdateRow> Parse(Stream stream, string extension)
    {
        using var reader = extension.Equals(".csv", StringComparison.OrdinalIgnoreCase)
            ? ExcelReaderFactory.CreateCsvReader(stream)
            : ExcelReaderFactory.CreateReader(stream);
        var dataSet = reader.AsDataSet(new ExcelDataSetConfiguration
        {
            ConfigureDataTable = _ => new ExcelDataTableConfiguration { UseHeaderRow = false }
        });
        if (dataSet.Tables.Count == 0 || dataSet.Tables[0].Rows.Count < 2)
        {
            throw new InvalidOperationException("檔案中沒有可更新的資料列");
        }

        var table = dataSet.Tables[0];
        var columnMap = new Dictionary<int, string>();
        for (var column = 0; column < table.Columns.Count; column++)
        {
            if (MassUpdateExcelColumnMap.TryResolve(FormatCellValue(table.Rows[0][column]), out var property))
            {
                columnMap[column] = property;
            }
        }

        var missingColumns = MassUpdateExcelColumnMap.RequiredFields
            .Where(field => !columnMap.Values.Contains(field.Key, StringComparer.OrdinalIgnoreCase))
            .Select(field => $"標題列缺少必填欄位 {field.Value}").ToList();
        AddDiSaImportRules.ThrowIfErrors(missingColumns);

        var errors = new List<string>();
        var rows = new List<MassUpdateRow>();
        for (var rowIndex = 1; rowIndex < table.Rows.Count; rowIndex++)
        {
            var dataRow = table.Rows[rowIndex];
            if (columnMap.Keys.All(column => string.IsNullOrWhiteSpace(FormatCellValue(dataRow[column]))))
            {
                continue;
            }

            var excelRow = rowIndex + 1;
            var values = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            foreach (var (column, property) in columnMap)
            {
                var value = AddDiSaImportRules.NormalizeCellText(FormatCellValue(dataRow[column]));
                values[property] = MassUpdateExcelColumnMap.DateProperties.Contains(property)
                    ? AddDiSaImportRules.NormalizeDateString(value, excelRow, property, errors)
                    : value;
            }

            var invoiceNo = AddDiSaImportRules.TrimToMax(Get(values, "InvoiceNo"), 30);
            foreach (var field in MassUpdateExcelColumnMap.RequiredFields)
                if (string.IsNullOrWhiteSpace(Get(values, field.Key)))
                    errors.Add($"第 {excelRow} 列：{field.Value} 為必填");
            if (Get(values, "Flt") is { Length: > 40 })
                errors.Add($"第 {excelRow} 列：FLT 不可超過 40 字元");
            if (Get(values, "Broker") is { Length: > 30 })
                errors.Add($"第 {excelRow} 列：Broker 不可超過 30 字元");
            if (Get(values, "Shipper") is { Length: > IcpHeader.ShipperMaxLength })
                errors.Add($"第 {excelRow} 列：Shipper 不可超過 {IcpHeader.ShipperMaxLength} 字元");
            if (string.IsNullOrWhiteSpace(invoiceNo))
            {
                continue;
            }

            rows.Add(new MassUpdateRow
            {
                RowNumber = excelRow,
                InvoiceNo = invoiceNo,
                NcdrNo = Trim(values, "NcdrNo", 60),
                Owner = Trim(values, "Owner", 50),
                EndUser = Trim(values, "EndUser", 100),
                ArrivalNotice = Trim(values, "ArrivalNotice", 100),
                SaDate = Trim(values, "SaDate", 10),
                Forwarder = Trim(values, "Forwarder", 50),
                Broker = Trim(values, "Broker", 30),
                Shipper = Trim(values, "Shipper", IcpHeader.ShipperMaxLength),
                Eta = Trim(values, "Eta", 10),
                Mawb = Trim(values, "Mawb", 20),
                Hawb = Trim(values, "Hawb", 20),
                Flt = Trim(values, "Flt", 40),
                DeliveryDate = Trim(values, "DeliveryDate", 10),
                MdpFlag = Trim(values, "MdpFlag", 5),
                ReasonForDeliveryDelay = Trim(values, "ReasonForDeliveryDelay", 200),
                DelayNotificationDate = Trim(values, "DelayNotificationDate", 10)
            });
        }

        AddDiSaImportRules.ThrowIfErrors(errors);
        return rows;
    }

    private static List<FieldChange> ApplyValues(IcpHeader header, MassUpdateRow row)
    {
        var changes = new List<FieldChange>();
        Set(changes, "ArrivalNotice", header.ArrivalNotice, row.ArrivalNotice, value => header.ArrivalNotice = value);
        Set(changes, "SaDate", header.SaDate, row.SaDate, value => header.SaDate = value);
        Set(changes, "Forwarder", header.Forwarder, row.Forwarder, value => header.Forwarder = value);
        Set(changes, "Broker", header.Broker, row.Broker, value => header.Broker = value);
        Set(changes, "Shipper", header.Shipper, row.Shipper, value => header.Shipper = value);
        Set(changes, "Eta", header.Eta, row.Eta, value => header.Eta = value);
        Set(changes, "Mawb", header.Mawb, row.Mawb, value => header.Mawb = value);
        Set(changes, "Hawb", header.Hawb, row.Hawb, value => header.Hawb = value);
        Set(changes, "Flt", header.Flt, row.Flt, value => header.Flt = value);
        Set(changes, "DeliveryDate", header.DeliveryDate, row.DeliveryDate, value => header.DeliveryDate = value);
        Set(changes, "MdpFlag", header.MdpFlag, row.MdpFlag, value => header.MdpFlag = value);
        Set(changes, "ReasonForDeliveryDelay", header.ReasonForDeliveryDelay, row.ReasonForDeliveryDelay, value => header.ReasonForDeliveryDelay = value);
        Set(changes, "DelayNotificationDate", header.DelayNotificationDate, row.DelayNotificationDate, value => header.DelayNotificationDate = value);
        return changes;
    }

    private static void Set(
        ICollection<FieldChange> changes,
        string fieldName,
        string? oldValue,
        string? newValue,
        Action<string?> assign)
    {
        if (string.Equals(oldValue, newValue, StringComparison.Ordinal))
        {
            return;
        }

        changes.Add(new FieldChange(fieldName, oldValue, newValue));
        assign(newValue);
    }

    private static string? Get(IReadOnlyDictionary<string, string?> values, string key) =>
        values.TryGetValue(key, out var value) ? value : null;

    private static string? Trim(IReadOnlyDictionary<string, string?> values, string key, int maxLength) =>
        AddDiSaImportRules.TrimToMax(Get(values, key), maxLength);

    private static string? JoinDistinct(IEnumerable<string?> values)
    {
        var result = values.Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase);
        var joined = string.Join(" / ", result);
        return string.IsNullOrEmpty(joined) ? null : joined;
    }

    private static string FormatCellValue(object? value) =>
        value switch
        {
            null or DBNull => string.Empty,
            DateTime date => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            _ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty
        };

    private sealed record FieldChange(string FieldName, string? OldValue, string? NewValue);
}

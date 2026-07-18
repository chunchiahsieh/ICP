using System.Globalization;
using ClosedXML.Excel;
using ICP.Data;
using ICP.Helpers;
using ICP.Models.ShipInfo;
using ICP.Repositories;
using Microsoft.EntityFrameworkCore;

namespace ICP.Services;

public class ReportDataService : IReportDataService
{
    private static readonly HashSet<string> SharedKeyFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "InvoiceNo",
        "TetPo"
    };

    private readonly ReportMetadataProvider _metadataProvider;
    private readonly IShipInfoRepository _repository;
    private readonly ApplicationDbContext _db;

    public ReportDataService(
        ReportMetadataProvider metadataProvider,
        IShipInfoRepository repository,
        ApplicationDbContext db)
    {
        _metadataProvider = metadataProvider;
        _repository = repository;
        _db = db;
    }

    public ShipInfoPageConfig GetPageConfig(string reportKey) =>
        _metadataProvider.GetPageConfig(reportKey, CultureInfo.CurrentUICulture.Name);

    public async Task<ShipInfoTableListViewModel> QueryHeaderTableAsync(
        string reportKey,
        ShipInfoHeaderQueryModel criteria,
        CancellationToken cancellationToken = default)
    {
        var config = GetPageConfig(reportKey);
        var items = await _repository.QueryHeadersAsync(criteria, config.HeaderFields, cancellationToken);
        return new ShipInfoTableListViewModel
        {
            TableId = "reportHeaderTable",
            TableKind = "Header",
            Culture = config.Culture,
            Fields = config.HeaderFields,
            TableUi = config.HeaderTableUi,
            Items = items
        };
    }

    public async Task<ShipInfoTableListViewModel> QueryDetailTableAsync(
        string reportKey,
        ShipInfoDetailQueryModel criteria,
        CancellationToken cancellationToken = default)
    {
        var config = GetPageConfig(reportKey);
        if (string.IsNullOrWhiteSpace(criteria.HeaderKey))
        {
            return new ShipInfoTableListViewModel
            {
                TableId = "reportDetailTable",
                TableKind = "Detail",
                Culture = config.Culture,
                Fields = config.DetailFields,
                TableUi = config.DetailTableUi,
                Items = []
            };
        }

        var items = await _repository.QueryDetailsAsync(criteria, config.DetailFields, cancellationToken);
        return new ShipInfoTableListViewModel
        {
            TableId = "reportDetailTable",
            TableKind = "Detail",
            Culture = config.Culture,
            Fields = config.DetailFields,
            TableUi = config.DetailTableUi,
            Items = items,
            SelectedHeaderKey = criteria.HeaderKey
        };
    }

    public Task<IReadOnlyList<string>> GetHeaderFilterOptionsAsync(
        string reportKey,
        string column,
        string? search,
        CancellationToken cancellationToken = default)
    {
        EnsureCheckboxField(GetPageConfig(reportKey).HeaderFields, column);
        return _repository.GetDistinctHeaderValuesAsync(column, search, cancellationToken);
    }

    public Task<IReadOnlyList<string>> GetDetailFilterOptionsAsync(
        string reportKey,
        string column,
        string headerKey,
        string? search,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(headerKey))
        {
            return Task.FromResult<IReadOnlyList<string>>([]);
        }

        EnsureCheckboxField(GetPageConfig(reportKey).DetailFields, column);
        return _repository.GetDistinctDetailValuesAsync(column, headerKey, search, cancellationToken);
    }

    public async Task<(byte[] Content, string FileName)> ExportExcelAsync(
        string reportKey,
        ShipInfoHeaderQueryModel criteria,
        string reportDisplayName,
        CancellationToken cancellationToken = default)
    {
        var config = GetPageConfig(reportKey);
        var headers = await _repository.QueryHeadersAsync(criteria, config.HeaderFields, cancellationToken);
        var headerFields = ShipInfoTableViewHelper.GetVisibleFields(config.HeaderFields);
        var detailFields = ShipInfoTableViewHelper.GetVisibleFields(config.DetailFields)
            .Where(field => !SharedKeyFields.Contains(field.FieldName))
            .ToList();

        var invoiceNos = headers
            .Select(row => Convert.ToString(
                ShipInfoTableViewHelper.GetItemValue(row, "InvoiceNo"),
                CultureInfo.InvariantCulture))
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var detailEntities = invoiceNos.Count == 0
            ? []
            : await _db.IcpDetails
                .AsNoTracking()
                .Where(detail => invoiceNos.Contains(detail.InvoiceNo))
                .OrderBy(detail => detail.InvoiceSeq)
                .ThenBy(detail => detail.TetPoLine)
                .ThenBy(detail => detail.ItemNo)
                .ToListAsync(cancellationToken);

        var detailsByHeader = detailEntities
            .GroupBy(
                detail => BuildCompositeKey(detail.InvoiceNo, detail.TetPo),
                StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group.Select(ShipInfoEntityMapper.MapEntity).ToList(),
                StringComparer.OrdinalIgnoreCase);

        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("Report");
        var columns = BuildExportColumns(headerFields, detailFields, config.Culture);
        for (var index = 0; index < columns.Count; index++)
        {
            worksheet.Cell(1, index + 1).Value = columns[index].Title;
        }

        var rowIndex = 2;
        foreach (var header in headers)
        {
            var invoiceNo = Convert.ToString(
                ShipInfoTableViewHelper.GetItemValue(header, "InvoiceNo"),
                CultureInfo.InvariantCulture) ?? string.Empty;
            var tetPo = Convert.ToString(
                ShipInfoTableViewHelper.GetItemValue(header, "TetPo"),
                CultureInfo.InvariantCulture) ?? string.Empty;
            var key = BuildCompositeKey(invoiceNo, tetPo);

            if (!detailsByHeader.TryGetValue(key, out var details) || details.Count == 0)
            {
                WriteExportRow(worksheet, rowIndex++, columns, header, null);
                continue;
            }

            foreach (var detail in details)
            {
                WriteExportRow(worksheet, rowIndex++, columns, header, detail);
            }
        }

        FormatWorksheet(worksheet, columns.Count, rowIndex - 1);

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        var stamp = DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        return (stream.ToArray(), $"{SanitizeFileName(reportDisplayName, reportKey)}_{stamp}.xlsx");
    }

    private static void EnsureCheckboxField(
        IReadOnlyList<ShipInfoFieldMetadata> fields,
        string column)
    {
        var field = fields.FirstOrDefault(item =>
            string.Equals(item.FieldName, column, StringComparison.OrdinalIgnoreCase));
        if (field is null || !field.Searchable || !ShipInfoMetadataHelper.IsCheckboxFilter(field))
        {
            throw new ArgumentException("Filter column is invalid.", nameof(column));
        }
    }

    private static string BuildCompositeKey(string invoiceNo, string tetPo) =>
        $"{invoiceNo.Trim()}\u001f{tetPo.Trim()}";

    private static IReadOnlyList<ExportColumn> BuildExportColumns(
        IReadOnlyList<ShipInfoFieldMetadata> headerFields,
        IReadOnlyList<ShipInfoFieldMetadata> detailFields,
        string culture)
    {
        var columns = new List<ExportColumn>();
        var usedTitles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        AddColumns(columns, usedTitles, headerFields, "header", "Header", culture);
        AddColumns(columns, usedTitles, detailFields, "detail", "Detail", culture);
        return columns;
    }

    private static void AddColumns(
        ICollection<ExportColumn> columns,
        ISet<string> usedTitles,
        IEnumerable<ShipInfoFieldMetadata> fields,
        string source,
        string prefix,
        string culture)
    {
        foreach (var field in fields)
        {
            var title = ShipInfoTableViewHelper.ResolveLabel(field, culture);
            if (!usedTitles.Add(title))
            {
                title = $"{prefix} - {title}";
                usedTitles.Add(title);
            }

            columns.Add(new ExportColumn(title, source, field));
        }
    }

    private static void WriteExportRow(
        IXLWorksheet worksheet,
        int rowIndex,
        IReadOnlyList<ExportColumn> columns,
        IReadOnlyDictionary<string, object?> header,
        IReadOnlyDictionary<string, object?>? detail)
    {
        for (var index = 0; index < columns.Count; index++)
        {
            var column = columns[index];
            var source = column.Source.Equals("header", StringComparison.OrdinalIgnoreCase)
                ? header
                : detail;
            var value = source is null
                ? null
                : ShipInfoTableViewHelper.GetItemValue(source, column.Field.FieldName);
            SetCellValue(worksheet.Cell(rowIndex, index + 1), value, column.Field);
        }
    }

    private static void SetCellValue(IXLCell cell, object? value, ShipInfoFieldMetadata field)
    {
        switch (value)
        {
            case null:
                cell.Clear();
                return;
            case DateTime dateTime:
                cell.Value = dateTime;
                cell.Style.DateFormat.Format = field.ControlType == ShipInfoControlTypes.DateTime
                    ? "yyyy-MM-dd HH:mm:ss"
                    : "yyyy-MM-dd";
                return;
            case DateOnly dateOnly:
                cell.Value = dateOnly.ToDateTime(TimeOnly.MinValue);
                cell.Style.DateFormat.Format = "yyyy-MM-dd";
                return;
            case decimal decimalValue:
                cell.Value = decimalValue;
                return;
            case double doubleValue:
                cell.Value = doubleValue;
                return;
            case float floatValue:
                cell.Value = floatValue;
                return;
            case int intValue:
                cell.Value = intValue;
                return;
            case long longValue:
                cell.Value = longValue;
                return;
        }

        var text = ShipInfoTableViewHelper.FormatCellValue(value, field);
        if (field.ControlType is ShipInfoControlTypes.Date or ShipInfoControlTypes.DateRange or ShipInfoControlTypes.DateTime
            && DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
        {
            cell.Value = parsed;
            cell.Style.DateFormat.Format = field.ControlType == ShipInfoControlTypes.DateTime
                ? "yyyy-MM-dd HH:mm:ss"
                : "yyyy-MM-dd";
            return;
        }

        cell.Value = text;
    }

    private static void FormatWorksheet(IXLWorksheet worksheet, int columnCount, int dataRowCount)
    {
        if (columnCount == 0)
        {
            return;
        }

        var lastRow = Math.Max(1, dataRowCount);
        worksheet.Range(1, 1, lastRow, columnCount).SetAutoFilter();
        worksheet.SheetView.FreezeRows(1);
        worksheet.Row(1).Style.Font.Bold = true;
        worksheet.Columns(1, columnCount).AdjustToContents(1, 40);
    }

    private static string SanitizeFileName(string displayName, string fallback)
    {
        var name = string.IsNullOrWhiteSpace(displayName) ? fallback : displayName;
        foreach (var invalid in Path.GetInvalidFileNameChars())
        {
            name = name.Replace(invalid, '_');
        }

        return name;
    }

    private sealed record ExportColumn(
        string Title,
        string Source,
        ShipInfoFieldMetadata Field);
}

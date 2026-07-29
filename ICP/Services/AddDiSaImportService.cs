using System.Data;
using System.Globalization;
using System.Text;
using ExcelDataReader;
using ICP.Data;
using ICP.Helpers;
using ICP.Models.Icp;
using ICP.Models.ShipInfo;
using Microsoft.EntityFrameworkCore;

namespace ICP.Services;

public class AddDiSaImportService
{
    static AddDiSaImportService()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    private readonly ApplicationDbContext _db;

    public AddDiSaImportService(ApplicationDbContext db)
    {
        _db = db;
    }

    public static string ResolveStorageDirectory(IWebHostEnvironment environment) =>
        Path.GetFullPath(Path.Combine(environment.ContentRootPath, "uploads", "adddisa"));

    public static string ValidateAndNormalizeStoredFilePath(string storedFilePath, IWebHostEnvironment environment)
    {
        var uploadDirectory = ResolveStorageDirectory(environment);
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

    public async Task<List<AddDiSaImportRow>> ParseAsync(
        string storedFilePath,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await using var stream = System.IO.File.OpenRead(storedFilePath);
        var rows = ParseExcel(stream);
        if (rows.Count == 0)
        {
            throw new InvalidOperationException("檔案中沒有可匯入的資料列");
        }

        // Content validation (dates/required) already applied in ParseExcel.
        // Invoice consistency is soft for preview; Save calls ValidateForSaveAsync.
        return rows;
    }

    public async Task<List<AddDiSaImportRowViewModel>> BuildPreviewRowsAsync(
        IReadOnlyList<AddDiSaImportRow> rows,
        CancellationToken cancellationToken = default)
    {
        var dbDuplicateSet = await GetExistingKeySetAsync(rows, cancellationToken);
        var inconsistentInvoices = AddDiSaImportRules.CollectInvoiceConsistencyIssues(rows);

        return rows
            .Select(row => new AddDiSaImportRowViewModel
            {
                Row = row,
                IsDbDuplicate = dbDuplicateSet.Contains((row.InvoiceNo, row.TetPo)),
                IsInvoiceInconsistent = inconsistentInvoices.Contains(row.InvoiceNo)
            })
            .ToList();
    }

    public async Task ValidateForSaveAsync(
        IReadOnlyList<AddDiSaImportRow> rows,
        CancellationToken cancellationToken = default)
    {
        var errors = new List<string>();
        AddDiSaImportRules.ValidateInvoiceConsistency(rows, errors);
        AddDiSaImportRules.ThrowIfErrors(errors);
        await EnsureNoDuplicateKeysAsync(rows, errors, cancellationToken);
    }

    public async Task<AddDiSaImportResult> SaveAsync(
        string storedFilePath,
        string createUser,
        CancellationToken cancellationToken = default)
    {
        var rows = await ParseAsync(storedFilePath, cancellationToken);
        await ValidateForSaveAsync(rows, cancellationToken);

        var now = DateTime.Now;
        var user = TruncateUser(createUser);
        var today = now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        var headers = new List<IcpHeader>();
        var headerKeys = new HashSet<(string InvoiceNo, string TetPo)>(new InvoiceTetPoComparer());
        foreach (var row in rows)
        {
            var key = (row.InvoiceNo, row.TetPo);
            if (!headerKeys.Add(key))
            {
                continue;
            }

            var header = CloneHeader(row.Header);
            header.Id = Guid.NewGuid();
            header.InvoiceNo = row.InvoiceNo;
            header.TetPo = row.TetPo;
            header.CreateTime = now;
            header.CreateUser = user;
            if (string.IsNullOrWhiteSpace(header.CreateDate))
            {
                header.CreateDate = today;
            }

            header.DepositCaseStatus = ShipInfoCaseStatuses.NotInitiated;
            header.ArurCaseStatus = ShipInfoCaseStatuses.NotInitiated;
            headers.Add(header);
        }

        var details = new List<IcpDetail>();
        foreach (var row in rows)
        {
            var detail = CloneDetail(row.Detail);
            detail.Id = Guid.NewGuid();
            detail.InvoiceNo = row.InvoiceNo;
            detail.TetPo = row.TetPo;
            detail.CreateTime = now;
            detail.CreateUser = user;
            detail.DepositCaseStatus = ShipInfoCaseStatuses.NotInitiated;
            detail.ArurCaseStatus = ShipInfoCaseStatuses.NotInitiated;
            details.Add(detail);
        }

        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            _db.IcpHeaders.AddRange(headers);
            _db.IcpDetails.AddRange(details);
            await _db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return new AddDiSaImportResult
            {
                HeaderCount = headers.Count,
                DetailCount = details.Count
            };
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
    private async Task EnsureNoDuplicateKeysAsync(
        IReadOnlyList<AddDiSaImportRow> rows,
        List<string> errors,
        CancellationToken cancellationToken)
    {
        var existingSet = await GetExistingKeySetAsync(rows, cancellationToken);
        var keys = rows
            .Select(r => (r.InvoiceNo, r.TetPo))
            .Distinct(new InvoiceTetPoComparer())
            .ToList();

        foreach (var key in keys)
        {
            if (existingSet.Contains(key))
            {
                errors.Add($"InvoiceNo+TetPo 已存在：{key.InvoiceNo} / {key.TetPo}");
            }
        }

        AddDiSaImportRules.ThrowIfErrors(errors);
    }

    private async Task<HashSet<(string InvoiceNo, string TetPo)>> GetExistingKeySetAsync(
        IReadOnlyList<AddDiSaImportRow> rows,
        CancellationToken cancellationToken)
    {
        var keys = rows
            .Select(r => (r.InvoiceNo, r.TetPo))
            .Distinct(new InvoiceTetPoComparer())
            .ToList();

        var invoiceNos = keys.Select(k => k.InvoiceNo).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (invoiceNos.Count == 0)
        {
            return new HashSet<(string InvoiceNo, string TetPo)>(new InvoiceTetPoComparer());
        }

        var existing = await _db.IcpHeaders
            .AsNoTracking()
            .Where(h => invoiceNos.Contains(h.InvoiceNo))
            .Select(h => new { h.InvoiceNo, h.TetPo })
            .ToListAsync(cancellationToken);

        return new HashSet<(string InvoiceNo, string TetPo)>(
            existing.Select(x => (x.InvoiceNo, x.TetPo)),
            new InvoiceTetPoComparer());
    }

    private static List<AddDiSaImportRow> ParseExcel(Stream stream)
    {
        using var reader = ExcelReaderFactory.CreateReader(stream);
        var dataSet = reader.AsDataSet(new ExcelDataSetConfiguration
        {
            ConfigureDataTable = _ => new ExcelDataTableConfiguration
            {
                UseHeaderRow = false
            }
        });

        if (dataSet.Tables.Count == 0)
        {
            throw new InvalidOperationException("檔案中沒有工作表");
        }

        var table = dataSet.Tables[0];
        if (table.Rows.Count < 2)
        {
            throw new InvalidOperationException("檔案中沒有可匯入的資料列");
        }

        var headerRow = table.Rows[0];
        var columnMap = new Dictionary<int, string>();
        for (var i = 0; i < table.Columns.Count; i++)
        {
            var headerText = FormatCellValue(headerRow[i]);
            if (string.IsNullOrWhiteSpace(headerText))
            {
                continue;
            }

            if (!AddDiSaExcelColumnMap.TryResolve(headerText, out var propertyKey))
            {
                continue;
            }

            columnMap[i] = propertyKey;
        }

        foreach (var required in AddDiSaImportRules.RequiredProperties)
        {
            if (!columnMap.Values.Contains(required, StringComparer.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"標題列缺少必填欄位 {required}");
            }
        }

        var errors = new List<string>();
        var rows = new List<AddDiSaImportRow>();

        for (var r = 1; r < table.Rows.Count; r++)
        {
            var dataRow = table.Rows[r];
            if (IsEmptyRow(dataRow, columnMap.Keys))
            {
                continue;
            }

            var excelRowNumber = r + 1;
            var values = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            foreach (var (colIndex, propertyKey) in columnMap)
            {
                var raw = AddDiSaImportRules.NormalizeCellText(FormatCellValue(dataRow[colIndex]));
                if (AddDiSaExcelColumnMap.IsDateProperty(propertyKey))
                {
                    values[propertyKey] = AddDiSaImportRules.NormalizeDateString(raw, excelRowNumber, propertyKey, errors);
                }
                else
                {
                    values[propertyKey] = raw;
                }
            }

            var invoiceNo = AddDiSaImportRules.TrimToMax(
                AddDiSaImportRules.RequireNonEmpty(
                    GetValue(values, AddDiSaExcelColumnMap.InvoiceNo),
                    excelRowNumber,
                    "INVOICE_NO",
                    errors),
                AddDiSaImportRules.InvoiceNoMaxLength) ?? string.Empty;
            var tetPo = AddDiSaImportRules.TrimToMax(
                AddDiSaImportRules.RequireNonEmpty(
                    GetValue(values, AddDiSaExcelColumnMap.TetPo),
                    excelRowNumber,
                    "TET_PO",
                    errors),
                AddDiSaImportRules.TetPoMaxLength) ?? string.Empty;

            if (string.IsNullOrEmpty(invoiceNo) || string.IsNullOrEmpty(tetPo))
            {
                continue;
            }

            var header = BuildHeader(values);
            header.InvoiceNo = invoiceNo;
            header.TetPo = tetPo;

            var detail = BuildDetail(values);
            detail.InvoiceNo = invoiceNo;
            detail.TetPo = tetPo;

            rows.Add(new AddDiSaImportRow
            {
                RowNumber = excelRowNumber,
                InvoiceNo = invoiceNo,
                TetPo = tetPo,
                Mawb = header.Mawb,
                Hawb = header.Hawb,
                Flt = header.Flt,
                Eta = header.Eta,
                Header = header,
                Detail = detail
            });
        }

        AddDiSaImportRules.ThrowIfErrors(errors);
        return rows;
    }

    private static IcpHeader BuildHeader(IReadOnlyDictionary<string, string?> values) =>
        new()
        {
            CreateDate = Trim(values, "CreateDate", 20),
            SaDate = Trim(values, "SaDate", 10),
            Forwarder = Trim(values, "Forwarder", 50),
            Broker = Trim(values, "Broker", 30),
            Etd = Trim(values, "Etd", 10),
            Eta = Trim(values, "Eta", 10),
            InvoiceDate = Trim(values, "InvoiceDate", 10),
            Mawb = Trim(values, "Mawb", 20),
            Hawb = Trim(values, "Hawb", 20),
            Flt = Trim(values, "Flt", 20),
            Freight = Trim(values, "Freight", 10),
            DestinationPort = Trim(values, "DestinationPort", 10),
            DestinationCountry = Trim(values, "DestinationCountry", 3),
            Warehouse = Trim(values, "Warehouse", 20),
            InvoiceType = Trim(values, "InvoiceType", 10),
            Incoterms = Trim(values, "Incoterms", 20),
            OrderType = Trim(values, "OrderType", 20),
            DeliveryDate = Trim(values, "DeliveryDate", 10),
            DeliveryTo = Trim(values, "DeliveryTo", 20),
            Bu = Trim(values, "Bu", 40),
            OrderPriority = ParseInt(GetValue(values, "OrderPriority")),
            MdpFlag = Trim(values, "MdpFlag", 5),
            TotalCartons = ParseDouble(GetValue(values, "TotalCartons")),
            NcdrNo = Trim(values, "NcdrNo", 60),
            NcdrRequestor = Trim(values, "NcdrRequestor", 40),
            EndUserCode = Trim(values, "EndUserCode", 30),
            EndUser = Trim(values, "EndUser", 100),
            RtNo = Trim(values, "RtNo", IcpHeader.RtNoMaxLength),
            Receiver = Trim(values, "Receiver", 200),
            Owner = Trim(values, "Owner", 50),
            MachineNo = Trim(values, "MachineNo", 50),
            MachineType = Trim(values, "MachineType", 50),
            ShipReason = Trim(values, "ShipReason", 50),
            Forklift = Trim(values, "Forklift", 50),
            MovingLabor = Trim(values, "MovingLabor", 50),
            CarMethod = Trim(values, "CarMethod", 50),
            ArriveTime = Trim(values, "ArriveTime", 50),
            WasteDisposal = Trim(values, "WasteDisposal", 50),
            DriverDetails = Trim(values, "DriverDetails", 50),
            OrderReason = Trim(values, "OrderReason", 50),
            ArrivalNoticeFlag = Trim(values, "ArrivalNoticeFlag", 5),
            ArrivalNotice = Trim(values, "ArrivalNotice", 100),
            ReasonForDeliveryDelay = Trim(values, "ReasonForDeliveryDelay", 200),
            DelayNotificationDate = Trim(values, "DelayNotificationDate", 10),
            DeliveryNo = Trim(values, "DeliveryNo", 30),
            SoldToPartyCode = Trim(values, "SoldToPartyCode", 30),
            SoldToParty = Trim(values, "SoldToParty", 100),
            ShipToPartyCode = Trim(values, "ShipToPartyCode", 30),
            ShipToParty = Trim(values, "ShipToParty", 100),
            ShipToPartyAddress = Trim(values, "ShipToPartyAddress", 200),
            EmgFlight = Trim(values, "EmgFlight", 5),
            WbsElement = Trim(values, "WbsElement", 30),
            Deposit = Trim(values, "Deposit", IcpHeader.DepositMaxLength),
            SapRemarks = Trim(values, "SapRemarks", 1000),
            Notes = Trim(values, "Notes", 1000),
            Cancellation = Trim(values, "Cancellation", 10),
            ReasonForCancellation = Trim(values, "ReasonForCancellation", 200),
            AttachedFile = Trim(values, "AttachedFile", 1000)
        };

    private static IcpDetail BuildDetail(IReadOnlyDictionary<string, string?> values) =>
        new()
        {
            TetPoLine = Trim(values, "TetPoLine", 35),
            InvoiceSeq = ParseDouble(GetValue(values, "InvoiceSeq")),
            ItemNo = Trim(values, "ItemNo", 47),
            Description = Trim(values, "Description", 60),
            Qty = ParseDecimal(GetValue(values, "Qty")),
            Uom = Trim(values, "Uom", 10),
            Coo = Trim(values, "Coo", 50),
            Price = ParseDouble(GetValue(values, "Price")),
            Amount = ParseDouble(GetValue(values, "Amount")),
            Currency = Trim(values, "Currency", 3),
            Rate = ParseDecimal(GetValue(values, "Rate")),
            PackingType = Trim(values, "PackingType", 50),
            CartonNo = ParseDouble(GetValue(values, "CartonNo")),
            Length = ParseDouble(GetValue(values, "Length")),
            Width = ParseDouble(GetValue(values, "Width")),
            Hight = ParseDouble(GetValue(values, "Hight")),
            GrossWeight = ParseDecimal(GetValue(values, "GrossWeight")),
            NetWeightOfTheItem = ParseDouble(GetValue(values, "NetWeightOfTheItem")),
            DeliveryLineNo = ParseDouble(GetValue(values, "DeliveryLineNo")),
            Eccn = Trim(values, "Eccn", 10),
            ElFlag = Trim(values, "ElFlag", 5),
            SdsFlag = Trim(values, "SdsFlag", 5),
            Hazmat = Trim(values, "Hazmat", 5)
        };

    private static IcpHeader CloneHeader(IcpHeader source) =>
        new()
        {
            CreateDate = source.CreateDate,
            SaDate = source.SaDate,
            InvoiceNo = source.InvoiceNo,
            Forwarder = source.Forwarder,
            Broker = source.Broker,
            Etd = source.Etd,
            Eta = source.Eta,
            InvoiceDate = source.InvoiceDate,
            Mawb = source.Mawb,
            Hawb = source.Hawb,
            Flt = source.Flt,
            Freight = source.Freight,
            DestinationPort = source.DestinationPort,
            DestinationCountry = source.DestinationCountry,
            Warehouse = source.Warehouse,
            InvoiceType = source.InvoiceType,
            Incoterms = source.Incoterms,
            OrderType = source.OrderType,
            DeliveryDate = source.DeliveryDate,
            DeliveryTo = source.DeliveryTo,
            Bu = source.Bu,
            TetPo = source.TetPo,
            OrderPriority = source.OrderPriority,
            MdpFlag = source.MdpFlag,
            TotalCartons = source.TotalCartons,
            NcdrNo = source.NcdrNo,
            NcdrRequestor = source.NcdrRequestor,
            EndUserCode = source.EndUserCode,
            EndUser = source.EndUser,
            RtNo = source.RtNo,
            Receiver = source.Receiver,
            Owner = source.Owner,
            MachineNo = source.MachineNo,
            MachineType = source.MachineType,
            ShipReason = source.ShipReason,
            Forklift = source.Forklift,
            MovingLabor = source.MovingLabor,
            CarMethod = source.CarMethod,
            ArriveTime = source.ArriveTime,
            WasteDisposal = source.WasteDisposal,
            DriverDetails = source.DriverDetails,
            OrderReason = source.OrderReason,
            ArrivalNoticeFlag = source.ArrivalNoticeFlag,
            ArrivalNotice = source.ArrivalNotice,
            ReasonForDeliveryDelay = source.ReasonForDeliveryDelay,
            DelayNotificationDate = source.DelayNotificationDate,
            DeliveryNo = source.DeliveryNo,
            SoldToPartyCode = source.SoldToPartyCode,
            SoldToParty = source.SoldToParty,
            ShipToPartyCode = source.ShipToPartyCode,
            ShipToParty = source.ShipToParty,
            ShipToPartyAddress = source.ShipToPartyAddress,
            EmgFlight = source.EmgFlight,
            WbsElement = source.WbsElement,
            Deposit = source.Deposit,
            SapRemarks = source.SapRemarks,
            Notes = source.Notes,
            Cancellation = source.Cancellation,
            ReasonForCancellation = source.ReasonForCancellation,
            AttachedFile = source.AttachedFile
        };

    private static IcpDetail CloneDetail(IcpDetail source) =>
        new()
        {
            InvoiceNo = source.InvoiceNo,
            TetPo = source.TetPo,
            TetPoLine = source.TetPoLine,
            InvoiceSeq = source.InvoiceSeq,
            ItemNo = source.ItemNo,
            Description = source.Description,
            Qty = source.Qty,
            Uom = source.Uom,
            Coo = source.Coo,
            Price = source.Price,
            Amount = source.Amount,
            Currency = source.Currency,
            Rate = source.Rate,
            PackingType = source.PackingType,
            CartonNo = source.CartonNo,
            Length = source.Length,
            Width = source.Width,
            Hight = source.Hight,
            GrossWeight = source.GrossWeight,
            NetWeightOfTheItem = source.NetWeightOfTheItem,
            DeliveryLineNo = source.DeliveryLineNo,
            Eccn = source.Eccn,
            ElFlag = source.ElFlag,
            SdsFlag = source.SdsFlag,
            Hazmat = source.Hazmat
        };

    private static string? GetValue(IReadOnlyDictionary<string, string?> values, string key) =>
        values.TryGetValue(key, out var value) ? value : null;

    private static string? Trim(IReadOnlyDictionary<string, string?> values, string key, int maxLength) =>
        AddDiSaImportRules.TrimToMax(GetValue(values, key), maxLength);

    private static int? ParseInt(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            || int.TryParse(text, NumberStyles.Integer, CultureInfo.CurrentCulture, out value)
            ? value
            : null;
    }

    private static double? ParseDouble(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            || double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out value)
            ? value
            : null;
    }

    private static decimal? ParseDecimal(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        return decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var value)
            || decimal.TryParse(text, NumberStyles.Number, CultureInfo.CurrentCulture, out value)
            ? value
            : null;
    }

    private static bool IsEmptyRow(DataRow row, IEnumerable<int> columnIndexes)
    {
        foreach (var index in columnIndexes)
        {
            if (!string.IsNullOrWhiteSpace(FormatCellValue(row[index])))
            {
                return false;
            }
        }

        return true;
    }

    private static string FormatCellValue(object? value)
    {
        if (value is null or DBNull)
        {
            return string.Empty;
        }

        if (value is DateTime dateTime)
        {
            return dateTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }

        if (value is double or float or decimal)
        {
            return Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
        }

        return AddDiSaImportRules.NormalizeCellText(
                   Convert.ToString(value, CultureInfo.InvariantCulture))
               ?? string.Empty;
    }

    private static string TruncateUser(string? user)
    {
        var resolved = string.IsNullOrWhiteSpace(user) ? "System" : user.Trim();
        return resolved.Length <= 100 ? resolved : resolved[..100];
    }

    private sealed class InvoiceTetPoComparer : IEqualityComparer<(string InvoiceNo, string TetPo)>
    {
        public bool Equals((string InvoiceNo, string TetPo) x, (string InvoiceNo, string TetPo) y) =>
            string.Equals(x.InvoiceNo, y.InvoiceNo, StringComparison.OrdinalIgnoreCase)
            && string.Equals(x.TetPo, y.TetPo, StringComparison.OrdinalIgnoreCase);

        public int GetHashCode((string InvoiceNo, string TetPo) obj) =>
            HashCode.Combine(
                StringComparer.OrdinalIgnoreCase.GetHashCode(obj.InvoiceNo ?? string.Empty),
                StringComparer.OrdinalIgnoreCase.GetHashCode(obj.TetPo ?? string.Empty));
    }
}

using System.Globalization;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using TEL.IntegrationHub.Models;
using TEL.IntegrationHub.Models.Ilc;

namespace TEL.IntegrationHub.Services;

public interface IIlcArurWriteService
{
    /// <summary>
    /// Writes RT_ARUR_HEADER from ShipInfo ARUR case snapshot.
    /// Idempotent on RT_NO: if row exists, skips insert.
    /// </summary>
    Task<IlcArurWriteResult> WriteFromShipInfoCaseAsync(
        ShipInfoCaseInitiatedMessage message,
        CancellationToken cancellationToken = default);
}

public sealed class IlcArurWriteResult
{
    public bool SkippedDuplicate { get; init; }

    public string? RtNo { get; init; }
}

public sealed class IlcArurWriteService : IIlcArurWriteService
{
    private readonly string _connectionString;
    private readonly ILogger<IlcArurWriteService> _logger;

    public IlcArurWriteService(
        IConfiguration configuration,
        ILogger<IlcArurWriteService> logger)
    {
        _connectionString = configuration.GetConnectionString("ILC_Connection")
            ?? throw new InvalidOperationException("ConnectionStrings:ILC_Connection is required.");
        _logger = logger;
    }

    public async Task<IlcArurWriteResult> WriteFromShipInfoCaseAsync(
        ShipInfoCaseInitiatedMessage message,
        CancellationToken cancellationToken = default)
    {
        if (!TryParseHeader(message.Payload?.Snapshot, out var header))
        {
            throw new InvalidOperationException("ShipInfo ARUR snapshot.header is missing or invalid.");
        }

        var rtNo = Truncate(
            FirstNonEmpty(message.Payload?.CaseNo, GetString(header, "RtNo"), GetString(header, "RT_NO")),
            16);
        if (string.IsNullOrWhiteSpace(rtNo))
        {
            throw new InvalidOperationException("ARUR RT_NO (caseNo / RtNo) is required.");
        }

        var forklift = GetString(header, "Forklift") ?? string.Empty;
        var wasteDisposal = GetString(header, "WasteDisposal") ?? string.Empty;
        var driverDetails = GetString(header, "DriverDetails") ?? string.Empty;
        var movingLabor = GetString(header, "MovingLabor");
        var needsStacker = ContainsIgnoreCase(forklift, "需要堆高機");
        var driverYes = IsYes(driverDetails);
        var wasteYes = IsYes(wasteDisposal);

        var row = new IlcRtArurHeader
        {
            RtNo = rtNo,
            CreateBy = Truncate(message.Payload?.Actor?.UserName, 50),
            CreateDate = DateTime.Now,
            EmailTo = null,
            ShipToCode = Truncate(GetString(header, "DeliveryTo"), 30),
            ShipTo = null,
            ArriveDate = CombineArriveDate(
                GetString(header, "DeliveryDate"),
                GetString(header, "ArriveTime")),
            ReceiptInfo = Truncate(GetString(header, "Receiver"), 255),
            WhCode = Truncate(GetString(header, "Warehouse"), 3),
            TetPo = Truncate(GetString(header, "TetPo", "TETPO"), 30),
            InvoiceNo = Truncate(GetString(header, "InvoiceNo"), 30),
            Attachment = Truncate(GetString(header, "AttachedFile"), 1000),
            Mawb = Truncate(GetString(header, "Mawb", "MAWB"), 30),
            Hawb = Truncate(GetString(header, "Hawb", "HAWB"), 30),
            Flt = Truncate(GetString(header, "Flt", "FLT"), 30),
            Eta = Truncate(GetString(header, "Eta", "ETA"), 30),
            Remark = Truncate(BuildRemark(GetString(header, "Notes"), needsStacker, movingLabor, wasteYes, driverYes), 2000),
            IsSDriver = driverYes ? "Y" : "N",
            IsSStacker = needsStacker ? "Y" : "N",
            ArrivalType = "1",
            RequestType = null,
            DependType = null,
            Status = "5",
            A1Start = "A8",
            CreateSys = "I"
        };

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        if (await ExistsRtNoAsync(connection, rtNo, cancellationToken))
        {
            _logger.LogInformation("ILC RT_ARUR_HEADER already exists for RT_NO={RtNo}; skip insert", rtNo);
            return new IlcArurWriteResult { SkippedDuplicate = true, RtNo = rtNo };
        }

        await InsertAsync(connection, row, cancellationToken);
        _logger.LogInformation(
            "ILC RT_ARUR_HEADER written RT_NO={RtNo} InvoiceNo={InvoiceNo}",
            rtNo,
            row.InvoiceNo);
        return new IlcArurWriteResult { RtNo = rtNo };
    }

    private static async Task<bool> ExistsRtNoAsync(
        SqlConnection connection,
        string rtNo,
        CancellationToken cancellationToken)
    {
        await using var cmd = new SqlCommand(
            """
            SELECT TOP (1) 1
            FROM dbo.RT_ARUR_HEADER
            WHERE RT_NO = @RT_NO;
            """,
            connection);
        cmd.Parameters.AddWithValue("@RT_NO", rtNo);
        var result = await cmd.ExecuteScalarAsync(cancellationToken);
        return result is not null and not DBNull;
    }

    private static async Task InsertAsync(
        SqlConnection connection,
        IlcRtArurHeader row,
        CancellationToken cancellationToken)
    {
        await using var cmd = new SqlCommand(
            """
            INSERT INTO dbo.RT_ARUR_HEADER
            (
                RT_NO, CreateBy, CreateDate, EmailTo, ShipToCode, ShipTo,
                ArriveDate, ReceiptInfo, WHCode, TETPO, InvoiceNo, Attachment,
                MAWB, HAWB, FLT, ETA, Remark, isSDriver, isSStacker,
                ArrivalType, RequestType, DependType, Status, A1_Start, CreateSys
            )
            VALUES
            (
                @RT_NO, @CreateBy, @CreateDate, @EmailTo, @ShipToCode, @ShipTo,
                @ArriveDate, @ReceiptInfo, @WHCode, @TETPO, @InvoiceNo, @Attachment,
                @MAWB, @HAWB, @FLT, @ETA, @Remark, @isSDriver, @isSStacker,
                @ArrivalType, @RequestType, @DependType, @Status, @A1_Start, @CreateSys
            );
            """,
            connection);
        cmd.Parameters.AddWithValue("@RT_NO", row.RtNo);
        cmd.Parameters.AddWithValue("@CreateBy", (object?)row.CreateBy ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@CreateDate", row.CreateDate);
        cmd.Parameters.AddWithValue("@EmailTo", (object?)row.EmailTo ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@ShipToCode", (object?)row.ShipToCode ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@ShipTo", (object?)row.ShipTo ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@ArriveDate", (object?)row.ArriveDate ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@ReceiptInfo", (object?)row.ReceiptInfo ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@WHCode", (object?)row.WhCode ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@TETPO", (object?)row.TetPo ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@InvoiceNo", (object?)row.InvoiceNo ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Attachment", (object?)row.Attachment ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@MAWB", (object?)row.Mawb ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@HAWB", (object?)row.Hawb ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@FLT", (object?)row.Flt ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@ETA", (object?)row.Eta ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Remark", (object?)row.Remark ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@isSDriver", row.IsSDriver);
        cmd.Parameters.AddWithValue("@isSStacker", row.IsSStacker);
        cmd.Parameters.AddWithValue("@ArrivalType", row.ArrivalType);
        cmd.Parameters.AddWithValue("@RequestType", (object?)row.RequestType ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@DependType", (object?)row.DependType ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Status", row.Status);
        cmd.Parameters.AddWithValue("@A1_Start", row.A1Start);
        cmd.Parameters.AddWithValue("@CreateSys", row.CreateSys);
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    private static string BuildRemark(
        string? notes,
        bool needsStacker,
        string? movingLabor,
        bool wasteYes,
        bool driverYes)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(notes))
        {
            parts.Add(notes.Trim());
        }

        if (needsStacker && !string.IsNullOrWhiteSpace(movingLabor))
        {
            parts.Add($"請安排堆高機{movingLabor.Trim()}");
        }

        if (wasteYes)
        {
            parts.Add("請處理廢棄物");
        }

        if (driverYes)
        {
            parts.Add("請回報司機資訊");
        }

        return parts.Count == 0 ? string.Empty : string.Join(Environment.NewLine, parts);
    }

    private static DateTime? CombineArriveDate(string? deliveryDate, string? arriveTime)
    {
        if (string.IsNullOrWhiteSpace(deliveryDate))
        {
            return null;
        }

        var dateRaw = deliveryDate.Trim().Split(' ', 'T')[0];
        string[] dateFormats = ["yyyy-MM-dd", "yyyy/MM/dd", "yyyy/M/d", "yyyy-M-d"];
        if (!DateTime.TryParseExact(
                dateRaw,
                dateFormats,
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var date))
        {
            if (!DateTime.TryParse(deliveryDate, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out date))
            {
                return null;
            }

            date = date.Date;
        }

        if (string.IsNullOrWhiteSpace(arriveTime))
        {
            return date.Date;
        }

        var timeRaw = arriveTime.Trim();
        string[] timeFormats = ["HH:mm", "H:mm", "HH:mm:ss", "H:mm:ss"];
        if (TimeSpan.TryParseExact(timeRaw, timeFormats, CultureInfo.InvariantCulture, out var time)
            || TimeSpan.TryParse(timeRaw, CultureInfo.InvariantCulture, out time))
        {
            return date.Date.Add(time);
        }

        return date.Date;
    }

    private static bool TryParseHeader(object? snapshot, out Dictionary<string, string?> header)
    {
        header = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        if (snapshot is null)
        {
            return false;
        }

        using var doc = snapshot switch
        {
            JsonElement el => JsonDocument.Parse(el.GetRawText()),
            JsonDocument jd => JsonDocument.Parse(jd.RootElement.GetRawText()),
            _ => JsonDocument.Parse(JsonSerializer.Serialize(snapshot))
        };

        var root = doc.RootElement;
        if (!root.TryGetProperty("header", out var headerEl) && !root.TryGetProperty("Header", out headerEl))
        {
            return false;
        }

        if (headerEl.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        foreach (var prop in headerEl.EnumerateObject())
        {
            header[prop.Name] = prop.Value.ValueKind switch
            {
                JsonValueKind.Null or JsonValueKind.Undefined => null,
                JsonValueKind.String => prop.Value.GetString(),
                JsonValueKind.Number => prop.Value.GetRawText(),
                JsonValueKind.True => "true",
                JsonValueKind.False => "false",
                _ => prop.Value.ToString()
            };
        }

        return header.Count > 0;
    }

    private static string? GetString(Dictionary<string, string?> map, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (map.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value))
            {
                return value.Trim();
            }

            if (map.TryGetValue(key, out value))
            {
                return value;
            }
        }

        return null;
    }

    private static string? FirstNonEmpty(params string?[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value.Trim();
            }
        }

        return null;
    }

    private static bool IsYes(string value) =>
        string.Equals(value.Trim(), "是", StringComparison.OrdinalIgnoreCase)
        || string.Equals(value.Trim(), "Y", StringComparison.OrdinalIgnoreCase)
        || string.Equals(value.Trim(), "Yes", StringComparison.OrdinalIgnoreCase)
        || string.Equals(value.Trim(), "true", StringComparison.OrdinalIgnoreCase);

    private static bool ContainsIgnoreCase(string source, string value) =>
        source.Contains(value, StringComparison.OrdinalIgnoreCase);

    private static string? Truncate(string? value, int maxLength)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        return value.Length <= maxLength ? value : value[..maxLength];
    }
}

using System.Globalization;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using TEL.IntegrationHub.Models;
using TEL.IntegrationHub.Models.Ilc;

namespace TEL.IntegrationHub.Services;

public interface IIlcDepositWriteService
{
    /// <summary>
    /// Writes Deposit_Head / Deposit_Import / Deposit_Buyer from ShipInfo case snapshot.
    /// Idempotent on InvNo: if Head already exists, skips insert.
    /// </summary>
    Task<IlcDepositWriteResult> WriteFromShipInfoCaseAsync(
        ShipInfoCaseInitiatedMessage message,
        CancellationToken cancellationToken = default);
}

public sealed class IlcDepositWriteResult
{
    public bool SkippedDuplicate { get; init; }

    public int? HeadKeyId { get; init; }

    public int ImportCount { get; init; }

    public int BuyerCount { get; init; }
}

public sealed class IlcDepositWriteService : IIlcDepositWriteService
{
    private readonly string _connectionString;
    private readonly ILogger<IlcDepositWriteService> _logger;

    public IlcDepositWriteService(
        IConfiguration configuration,
        ILogger<IlcDepositWriteService> logger)
    {
        _connectionString = configuration.GetConnectionString("ILC_Connection")
            ?? throw new InvalidOperationException("ConnectionStrings:ILC_Connection is required.");
        _logger = logger;
    }

    public async Task<IlcDepositWriteResult> WriteFromShipInfoCaseAsync(
        ShipInfoCaseInitiatedMessage message,
        CancellationToken cancellationToken = default)
    {
        if (!TryParseSnapshot(message.Payload?.Snapshot, out var header, out var details))
        {
            throw new InvalidOperationException("ShipInfo Deposit snapshot.header/details is missing or invalid.");
        }

        var invNo = Truncate(GetString(header, "InvoiceNo"), 50);
        if (string.IsNullOrWhiteSpace(invNo))
        {
            throw new InvalidOperationException("Deposit snapshot header.InvoiceNo is required.");
        }

        var now = DateTime.Now;
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var tx = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken);

        try
        {
            var existingKeyId = await FindHeadKeyIdByInvNoAsync(connection, tx, invNo, cancellationToken);
            if (existingKeyId.HasValue)
            {
                await tx.CommitAsync(cancellationToken);
                _logger.LogInformation(
                    "ILC Deposit_Head already exists for InvNo={InvNo} keyID={KeyId}; skip insert",
                    invNo,
                    existingKeyId.Value);
                return new IlcDepositWriteResult
                {
                    SkippedDuplicate = true,
                    HeadKeyId = existingKeyId.Value
                };
            }

            var head = new IlcDepositHead
            {
                Status = "0",
                InvNo = invNo,
                Gepo = Truncate(GetString(header, "EndUser"), 50),
                Bu = Truncate(GetString(header, "Bu", "BU"), 50),
                SubmitDate = now,
                CreateDate = now,
                Creator = "SYSTEM"
            };

            var headKeyId = await InsertHeadAsync(connection, tx, head, cancellationToken);
            var importCount = 0;
            var buyerCount = 0;

            foreach (var detail in details)
            {
                var import = new IlcDepositImport
                {
                    HeadkeyId = headKeyId,
                    InvNo = invNo,
                    Seq = Truncate(GetString(detail, "InvoiceSeq"), 50),
                    ItemNo = Truncate(GetString(detail, "ItemNo"), 500),
                    Description = Truncate(GetString(detail, "Description"), 500),
                    Qty = Truncate(GetString(detail, "Qty"), 50),
                    InvPrice = GetDouble(detail, "Price"),
                    InvTotalPrice = GetDouble(detail, "Amount"),
                    Mawb = Truncate(GetString(header, "Mawb", "MAWB"), 50),
                    Hawb = Truncate(GetString(header, "Hawb", "HAWB"), 50),
                    InvDate = GetDateTime(header, "InvoiceDate"),
                    FlightNo = Truncate(GetString(header, "Flt", "FLT"), 500),
                    Creator = "SYSTEM",
                    CreateDate = now
                };
                await InsertImportAsync(connection, tx, import, cancellationToken);
                importCount++;

                var buyer = new IlcDepositBuyer
                {
                    HeadkeyId = headKeyId,
                    ItemNo = Truncate(GetString(detail, "ItemNo"), 50),
                    Description = Truncate(GetString(detail, "Description"), 50),
                    Qty = Truncate(GetString(detail, "Qty"), 50),
                    Creator = "SYSTEM",
                    CreateDate = now
                };
                await InsertBuyerAsync(connection, tx, buyer, cancellationToken);
                buyerCount++;
            }

            await tx.CommitAsync(cancellationToken);
            _logger.LogInformation(
                "ILC Deposit written InvNo={InvNo} keyID={KeyId} import={ImportCount} buyer={BuyerCount}",
                invNo,
                headKeyId,
                importCount,
                buyerCount);

            return new IlcDepositWriteResult
            {
                HeadKeyId = headKeyId,
                ImportCount = importCount,
                BuyerCount = buyerCount
            };
        }
        catch
        {
            await tx.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private static async Task<int?> FindHeadKeyIdByInvNoAsync(
        SqlConnection connection,
        SqlTransaction tx,
        string invNo,
        CancellationToken cancellationToken)
    {
        await using var cmd = new SqlCommand(
            """
            SELECT TOP (1) keyID
            FROM dbo.Deposit_Head
            WHERE InvNo = @InvNo
            ORDER BY keyID DESC;
            """,
            connection,
            tx);
        cmd.Parameters.AddWithValue("@InvNo", invNo);
        var result = await cmd.ExecuteScalarAsync(cancellationToken);
        return result is null or DBNull ? null : Convert.ToInt32(result, CultureInfo.InvariantCulture);
    }

    private static async Task<int> InsertHeadAsync(
        SqlConnection connection,
        SqlTransaction tx,
        IlcDepositHead head,
        CancellationToken cancellationToken)
    {
        await using var cmd = new SqlCommand(
            """
            INSERT INTO dbo.Deposit_Head
            (
                Status, InvNo, Gepo, BU, SubmitDate, CreateDate, Creator
            )
            OUTPUT INSERTED.keyID
            VALUES
            (
                @Status, @InvNo, @Gepo, @BU, @SubmitDate, @CreateDate, @Creator
            );
            """,
            connection,
            tx);
        cmd.Parameters.AddWithValue("@Status", head.Status);
        cmd.Parameters.AddWithValue("@InvNo", (object?)head.InvNo ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Gepo", (object?)head.Gepo ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@BU", (object?)head.Bu ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@SubmitDate", head.SubmitDate);
        cmd.Parameters.AddWithValue("@CreateDate", head.CreateDate);
        cmd.Parameters.AddWithValue("@Creator", head.Creator);
        var result = await cmd.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt32(result, CultureInfo.InvariantCulture);
    }

    private static async Task InsertImportAsync(
        SqlConnection connection,
        SqlTransaction tx,
        IlcDepositImport row,
        CancellationToken cancellationToken)
    {
        await using var cmd = new SqlCommand(
            """
            INSERT INTO dbo.Deposit_Import
            (
                HeadkeyID, InvNo, SEQ, ItemNo, Description, Qty,
                InvPrice, InvTotalPrice, MAWB, HAWB, InvDate, FlightNo,
                Creator, CreateDate
            )
            VALUES
            (
                @HeadkeyID, @InvNo, @SEQ, @ItemNo, @Description, @Qty,
                @InvPrice, @InvTotalPrice, @MAWB, @HAWB, @InvDate, @FlightNo,
                @Creator, @CreateDate
            );
            """,
            connection,
            tx);
        cmd.Parameters.AddWithValue("@HeadkeyID", row.HeadkeyId);
        cmd.Parameters.AddWithValue("@InvNo", (object?)row.InvNo ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@SEQ", (object?)row.Seq ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@ItemNo", (object?)row.ItemNo ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Description", (object?)row.Description ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Qty", (object?)row.Qty ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@InvPrice", (object?)row.InvPrice ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@InvTotalPrice", (object?)row.InvTotalPrice ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@MAWB", (object?)row.Mawb ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@HAWB", (object?)row.Hawb ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@InvDate", (object?)row.InvDate ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@FlightNo", (object?)row.FlightNo ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Creator", row.Creator);
        cmd.Parameters.AddWithValue("@CreateDate", row.CreateDate);
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task InsertBuyerAsync(
        SqlConnection connection,
        SqlTransaction tx,
        IlcDepositBuyer row,
        CancellationToken cancellationToken)
    {
        await using var cmd = new SqlCommand(
            """
            INSERT INTO dbo.Deposit_Buyer
            (
                HeadkeyID, ItemNo, Description, Qty, Creator, CreateDate
            )
            VALUES
            (
                @HeadkeyID, @ItemNo, @Description, @Qty, @Creator, @CreateDate
            );
            """,
            connection,
            tx);
        cmd.Parameters.AddWithValue("@HeadkeyID", row.HeadkeyId);
        cmd.Parameters.AddWithValue("@ItemNo", (object?)row.ItemNo ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Description", (object?)row.Description ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Qty", (object?)row.Qty ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Creator", row.Creator);
        cmd.Parameters.AddWithValue("@CreateDate", row.CreateDate);
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    private static bool TryParseSnapshot(
        object? snapshot,
        out Dictionary<string, string?> header,
        out List<Dictionary<string, string?>> details)
    {
        header = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        details = [];

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

        header = ToStringMap(headerEl);

        if (root.TryGetProperty("details", out var detailsEl)
            || root.TryGetProperty("Details", out detailsEl))
        {
            if (detailsEl.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in detailsEl.EnumerateArray())
                {
                    details.Add(ToStringMap(item));
                }
            }
        }

        return header.Count > 0;
    }

    private static Dictionary<string, string?> ToStringMap(JsonElement element)
    {
        var map = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        if (element.ValueKind != JsonValueKind.Object)
        {
            return map;
        }

        foreach (var prop in element.EnumerateObject())
        {
            map[prop.Name] = prop.Value.ValueKind switch
            {
                JsonValueKind.Null or JsonValueKind.Undefined => null,
                JsonValueKind.String => prop.Value.GetString(),
                JsonValueKind.Number => prop.Value.GetRawText(),
                JsonValueKind.True => "true",
                JsonValueKind.False => "false",
                _ => prop.Value.ToString()
            };
        }

        return map;
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

    private static double? GetDouble(Dictionary<string, string?> map, params string[] keys)
    {
        var raw = GetString(map, keys);
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        return double.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out var value)
            ? value
            : null;
    }

    private static DateTime? GetDateTime(Dictionary<string, string?> map, params string[] keys)
    {
        var raw = GetString(map, keys);
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        string[] formats =
        [
            "yyyy-MM-dd",
            "yyyy-MM-dd HH:mm:ss",
            "yyyy/MM/dd",
            "o",
            "O"
        ];

        if (DateTime.TryParseExact(
                raw.Trim(),
                formats,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AllowWhiteSpaces,
                out var exact))
        {
            return exact;
        }

        return DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var parsed)
            ? parsed
            : null;
    }

    private static string? Truncate(string? value, int maxLength)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        return value.Length <= maxLength ? value : value[..maxLength];
    }
}

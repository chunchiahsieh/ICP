using System.Data;
using System.Globalization;
using ICP.Data;
using ICP.Models.Icp;
using Microsoft.EntityFrameworkCore;

namespace ICP.Services;

public sealed class ArurComparisonQuery
{
    public string? InvoiceNo { get; set; }
    public string? RtNo { get; set; }
    public string? Result { get; set; }
    public string? Field { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
}

public sealed record ArurComparisonRow(int Number, string Result, string RtNo, string InvoiceNo,
    string Field, string TargetField, string IcpValue, string IlcValue);
public sealed record ArurComparisonPage(List<ArurComparisonRow> Rows, int Total, int Page, int PageSize);
public sealed class ArurComparisonDataSourceException : Exception
{
    public ArurComparisonDataSourceException() : base("The configured ILC database does not contain dbo.RT_ARUR_HEADER.") { }
}

// This service is read-only. Scan bounded batches to filter comparison results before paging.
public sealed class ArurComparisonService(ApplicationDbContext db, IlcDbContext ilc)
{
    public static readonly string[] Fields = ["InvoiceNo", "TetPo", "Mawb", "Hawb", "Flt", "Eta",
        "DeliveryTo", "Warehouse", "ArriveTime", "Receiver", "Forklift", "DriverDetails", "Notes", "ShipTo", "AttachedFile"];

    public async Task<ArurComparisonPage> QueryAsync(ArurComparisonQuery query, bool export, CancellationToken ct)
    {
        var size = Math.Clamp(query.PageSize, 1, 100);
        var page = Math.Max(1, query.Page);
        var rows = new List<ArurComparisonRow>();
        var total = 0;
        var headers = db.IcpHeaders.AsNoTracking().Where(h =>
            (h.RtNo != null && h.RtNo != "") || h.ArurCaseStatus == "Initiated");
        if (!string.IsNullOrWhiteSpace(query.InvoiceNo)) headers = headers.Where(h => h.InvoiceNo.Contains(query.InvoiceNo.Trim()));
        if (!string.IsNullOrWhiteSpace(query.RtNo)) headers = headers.Where(h => h.RtNo != null && h.RtNo.Contains(query.RtNo.Trim()));
        var addresses = await db.SystemConfigs.AsNoTracking().Where(x => !x.IsDeleted && x.Category == "DeliveryToList")
            .OrderBy(x => x.Id).ToListAsync(ct);
        await ilc.Database.OpenConnectionAsync(ct);
        try
        {
            await EnsureArurTableAvailableAsync(ct);
            for (var offset = 0; ; offset += 200)
            {
                var batch = await headers.OrderBy(h => h.Id).Skip(offset).Take(200).ToListAsync(ct);
                if (batch.Count == 0) break;
                var ownerIds = batch.Select(h => h.Id.ToString("D")).ToList();
                var attachments = await db.Attachments.AsNoTracking().Where(a => !a.IsDeleted && a.AttachmentType == "ICP_HEADER" && ownerIds.Contains(a.AttachmentOwnerId))
                    .OrderBy(a => a.Id).ToListAsync(ct);
                var keys = batch.Select(h => Clean(h.RtNo)).Where(k => k.Length > 0).Distinct().ToArray();
                var targets = new Dictionary<string, List<Dictionary<string, string>>>(StringComparer.OrdinalIgnoreCase);
                if (keys.Length > 0)
                {
                    await using var command = ilc.Database.GetDbConnection().CreateCommand();
                    var names = new List<string>();
                    for (var i = 0; i < keys.Length; i++)
                    {
                        var parameter = command.CreateParameter();
                        parameter.ParameterName = "@k" + i; parameter.Value = keys[i]; parameter.DbType = DbType.String;
                        command.Parameters.Add(parameter); names.Add(parameter.ParameterName);
                    }
                    command.CommandText = "SELECT RT_NO, InvoiceNo, TETPO, MAWB, HAWB, FLT, ETA, ShipToCode, WHCode, ArriveDate, ReceiptInfo, isSStacker, isSDriver, Remark, ShipTo, Attachment FROM dbo.RT_ARUR_HEADER WHERE RT_NO IN (" + string.Join(',', names) + ")";
                    await using var reader = await command.ExecuteReaderAsync(ct);
                    while (await reader.ReadAsync(ct))
                    {
                        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                        for (var i = 0; i < reader.FieldCount; i++) values[reader.GetName(i)] = reader.IsDBNull(i) ? "" : reader.GetValue(i) is DateTime date ? date.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) : Clean(Convert.ToString(reader.GetValue(i), CultureInfo.InvariantCulture));
                        var key = values["RT_NO"];
                        if (!targets.TryGetValue(key, out var matches)) targets[key] = matches = [];
                        matches.Add(values);
                    }
                }
                foreach (var h in batch)
                {
                    targets.TryGetValue(Clean(h.RtNo), out var matches);
                    var target = matches?.Count == 1 ? matches[0] : null;
                    var address = addresses.FirstOrDefault(a => a.Key1 == h.DeliveryTo)?.Value4;
                    var files = attachments.Where(a => a.AttachmentOwnerId == h.Id.ToString("D")).Select(a => a.RelativePath).ToArray();
                    foreach (var (field, column, value) in Map(h, address, files))
                    {
                        if (!string.IsNullOrEmpty(query.Field) && query.Field != field) continue;
                        var actual = target?.GetValueOrDefault(column) ?? "";
                        var expected = Clean(value);
                        var equal = field == "AttachedFile" ? AttachmentsEqual(files, actual)
                            : field is "Eta" or "ArriveTime" ? DateEqual(expected, actual, field == "Eta")
                            : Clean(expected) == Clean(actual);
                        var result = matches?.Count > 1 ? "Ambiguous" : target == null ? "Missing" : equal ? "Equal" : "Different";
                        if (!string.IsNullOrEmpty(query.Result) && query.Result != result) continue;
                        total++;
                        if (export || ((long)total > (long)(page - 1) * size && (long)total <= (long)page * size))
                            rows.Add(new(total, result, Clean(h.RtNo), h.InvoiceNo, field, column, expected, actual));
                    }
                }
            }
        }
        finally { await ilc.Database.CloseConnectionAsync(); }
        var lastPage = Math.Max(1, (int)Math.Ceiling(total / (double)size));
        if (!export && page > lastPage)
        {
            query.Page = lastPage;
            return await QueryAsync(query, false, ct);
        }
        return new(rows, total, page, size);
    }

    private async Task EnsureArurTableAvailableAsync(CancellationToken ct)
    {
        await using var command = ilc.Database.GetDbConnection().CreateCommand();
        command.CommandText = "SELECT OBJECT_ID(N'dbo.RT_ARUR_HEADER', N'U')";
        var objectId = await command.ExecuteScalarAsync(ct);
        if (objectId is null || objectId == DBNull.Value)
            throw new ArurComparisonDataSourceException();
    }

    private static IEnumerable<(string Field, string Column, string? Value)> Map(IcpHeader h, string? address, string[] files)
    {
        yield return ("InvoiceNo", "InvoiceNo", h.InvoiceNo);
        yield return ("TetPo", "TETPO", h.TetPo);
        yield return ("Mawb", "MAWB", h.Mawb);
        yield return ("Hawb", "HAWB", h.Hawb);
        yield return ("Flt", "FLT", h.Flt);
        yield return ("Eta", "ETA", DisplayDate(h.Eta, true));
        yield return ("DeliveryTo", "ShipToCode", h.DeliveryTo);
        yield return ("Warehouse", "WHCode", h.Warehouse);
        yield return ("ArriveTime", "ArriveDate", DisplayDate(h.ArriveTime, false));
        yield return ("Receiver", "ReceiptInfo", h.Receiver);
        yield return ("Forklift", "isSStacker", IsY(h.Forklift) ? "Y" : "N");
        yield return ("DriverDetails", "isSDriver", IsY(h.DriverDetails) ? "Y" : "N");
        yield return ("Notes", "Remark", BuildRemark(h));
        yield return ("ShipTo", "ShipTo", address);
        yield return ("AttachedFile", "Attachment", string.Join(',', files));
    }

    public static string Clean(string? value) => (value ?? "").Trim().Replace("\r\n", "\n").Replace('\r', '\n');
    public static bool IsY(string? value) => string.Equals(value?.Trim(), "Y", StringComparison.OrdinalIgnoreCase);
    public static string BuildRemark(IcpHeader h)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(h.Notes)) parts.Add(h.Notes.Trim());
        if (IsY(h.Forklift)) parts.Add("請安排堆高機");
        if (IsY(h.WasteDisposal)) parts.Add("請處理廢棄物");
        if (IsY(h.DriverDetails)) parts.Add("請回報司機資訊");
        if (!string.IsNullOrWhiteSpace(h.MovingLabor)) parts.Add($"({h.MovingLabor.Trim()})");
        return string.Join('\n', parts);
    }
    public static string DisplayDate(string? value, bool dateOnly) => DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var date)
        ? date.ToString(dateOnly ? "yyyy-MM-dd" : "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) : Clean(value);
    public static bool DateEqual(string left, string right, bool dateOnly) => DisplayDate(left, dateOnly) == DisplayDate(right, dateOnly);
    public static bool AttachmentsEqual(string[] files, string actual)
    {
        var remaining = actual.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).Select(x => x.Replace('\\', '/')).ToList();
        foreach (var file in files)
        {
            var relative = file.Replace('\\', '/').TrimStart('/');
            var index = remaining.FindIndex(x => string.Equals(x, relative, StringComparison.OrdinalIgnoreCase) || x.EndsWith("/" + relative, StringComparison.OrdinalIgnoreCase));
            if (index < 0) return false;
            remaining.RemoveAt(index);
        }
        return remaining.Count == 0;
    }
}

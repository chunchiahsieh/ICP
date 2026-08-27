using System.Data;
using System.Globalization;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using TEL.IntegrationHub.Data;
using TEL.IntegrationHub.Models;
using TEL.IntegrationHub.Models.Ilc;

namespace TEL.IntegrationHub.Services;

public interface IIlcArurWriteService { Task<IlcArurWriteResult> WriteFromShipInfoCaseAsync(ShipInfoCaseInitiatedMessage message, CancellationToken cancellationToken = default); }
public sealed class IlcArurWriteResult { public bool SkippedDuplicate { get; init; } public string? RtNo { get; init; } }

public sealed class IlcArurWriteService : IIlcArurWriteService
{
    private readonly string _ilc; private readonly IcpDbContext _icp; private readonly FiestaDbContext _fiesta; private readonly ILogger<IlcArurWriteService> _logger;
    public IlcArurWriteService(IConfiguration c, IcpDbContext icp, FiestaDbContext fiesta, ILogger<IlcArurWriteService> logger) { _ilc = c.GetConnectionString("ILC_Connection") ?? throw new InvalidOperationException("ConnectionStrings:ILC_Connection is required."); _icp = icp; _fiesta = fiesta; _logger = logger; }

    public async Task<IlcArurWriteResult> WriteFromShipInfoCaseAsync(ShipInfoCaseInitiatedMessage message, CancellationToken ct = default)
    {
        if (!TryHeader(message.Payload?.Snapshot, out var h)) throw new InvalidOperationException("ShipInfo ARUR snapshot.header is missing or invalid.");
        var empId = ResolveEmpId(message.Payload?.Actor?.UserName);
        if (string.IsNullOrWhiteSpace(empId)) throw new InvalidOperationException("ARUR operator EmpID is required.");
        var person = await _fiesta.MailGroups.AsNoTracking().Where(x => x.EmpId == empId).OrderBy(x => x.Uid).FirstOrDefaultAsync(ct);
        if (person is null || string.IsNullOrWhiteSpace(person.EmpId) || string.IsNullOrWhiteSpace(person.Address)) throw new InvalidOperationException($"MailGroup operator not found or incomplete for EmpID '{empId}'.");
        var deliveryTo = Get(h, "DeliveryTo");
        var shipTo = await _icp.SystemConfigs.AsNoTracking().Where(x => !x.IsDeleted && x.Category == "DeliveryToList" && x.Key1 == deliveryTo).Select(x => x.Value4).FirstOrDefaultAsync(ct);
        if (string.IsNullOrWhiteSpace(shipTo)) throw new InvalidOperationException($"DeliveryToList address not found for DeliveryTo '{deliveryTo ?? "(null)"}'.");
        var now = DateTime.Now; var tetPo = Get(h, "TetPo", "TETPO"); var invoiceNo = Get(h, "InvoiceNo"); var forklift = IsY(Get(h, "Forklift")); var driver = IsY(Get(h, "DriverDetails")); var waste = IsY(Get(h, "WasteDisposal"));
        var row = new IlcRtArurHeader { Subject = $"AR {tetPo} {invoiceNo}", CreateBy = person.EmpId.Trim(), CreateDate = now, EditBy = person.EmpId.Trim(), EditDate = now, EmailTo = person.Address.Trim(), ShipToCode = deliveryTo, ShipTo = shipTo.Trim(), ArriveDate = ParseDate(Get(h, "ArriveTime")), ReceiptInfo = Get(h, "Receiver"), WhCode = Get(h, "Warehouse"), TetPo = tetPo, InvoiceNo = invoiceNo, Attachment = Get(h, "AttachedFile"), Mawb = Get(h, "Mawb", "MAWB"), Hawb = Get(h, "Hawb", "HAWB"), Flt = Get(h, "Flt", "FLT"), Eta = Get(h, "Eta", "ETA"), Remark = Remark(Get(h, "Notes"), forklift, Get(h, "MovingLabor"), waste, driver), IsSDriver = driver ? "Y" : "N", IsSStacker = forklift ? "Y" : "N", ArrivalType = "1", DependType = "1", RequestType = "2", Status = "5", A1Start = "A8", CreateSys = "ICP" };
        Validate(row);
        await using var conn = new SqlConnection(_ilc); await conn.OpenAsync(ct); await using var tx = (SqlTransaction)await conn.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        try { row.RtNo = await ClaimAsync(conn, tx, now, ct); await InsertAsync(conn, tx, row, ct); await tx.CommitAsync(ct); _logger.LogInformation("ILC RT_ARUR_HEADER written RT_NO={RtNo}", row.RtNo); return new IlcArurWriteResult { RtNo = row.RtNo }; }
        catch { await tx.RollbackAsync(CancellationToken.None); throw; }
    }

    private static async Task<string> ClaimAsync(SqlConnection c, SqlTransaction tx, DateTime now, CancellationToken ct)
    {
        var day = now.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
        await using var cmd = new SqlCommand("dbo.SP_GEN_SEQNO", c, tx)
        {
            CommandType = CommandType.StoredProcedure
        };
        cmd.Parameters.Add(new SqlParameter("@SYS_ID", SqlDbType.NVarChar, 20) { Value = "ICP" });
        cmd.Parameters.Add(new SqlParameter("@GROUP_1", SqlDbType.NVarChar, 10) { Value = "PRT" });
        cmd.Parameters.Add(new SqlParameter("@GROUP_2", SqlDbType.NVarChar, 10) { Value = day });
        cmd.Parameters.Add(new SqlParameter("@GROUP_3", SqlDbType.NVarChar, 10) { Value = "" });
        cmd.Parameters.Add(new SqlParameter("@GROUP_4", SqlDbType.NVarChar, 10) { Value = "" });
        var maxSeq = new SqlParameter("@MAXSEQ", SqlDbType.Decimal)
        {
            Precision = 6,
            Scale = 0,
            Direction = ParameterDirection.Output
        };
        cmd.Parameters.Add(maxSeq);
        await cmd.ExecuteNonQueryAsync(ct);
        if (maxSeq.Value is null or DBNull)
        {
            throw new InvalidOperationException("SP_GEN_SEQNO did not return @MAXSEQ.");
        }

        var number = Convert.ToInt32(maxSeq.Value, CultureInfo.InvariantCulture);
        if (number > 999)
        {
            throw new InvalidOperationException($"ARUR RT_NO daily sequence exceeded 999 for {day}.");
        }

        return $"PRT-{day}-{number:000}";
    }

    private static async Task InsertAsync(SqlConnection c, SqlTransaction tx, IlcRtArurHeader r, CancellationToken ct)
    {
        const string sql = """INSERT INTO dbo.RT_ARUR_HEADER (RT_NO,Subject,CreateBy,CreateDate,EmailTo,Attachment,Status,DocumentType,ArrivalType,WHCode,InvoiceNo,TETPO,FLT,Remark,isDD,isSDriver,isSStacker,DependType,ShipToCode,ArriveDate,MAWB,HAWB,R_isPriority,R_isAdvance,DepID,NowPage,ReceiptInfo,ETA,ShipTo,RequestType,A1_Start,EditBy,EditDate,CreateSys) VALUES (@RT_NO,@Subject,@CreateBy,@CreateDate,@EmailTo,@Attachment,@Status,@DocumentType,@ArrivalType,@WHCode,@InvoiceNo,@TETPO,@FLT,@Remark,@isDD,@isSDriver,@isSStacker,@DependType,@ShipToCode,@ArriveDate,@MAWB,@HAWB,@R_isPriority,@R_isAdvance,@DepID,@NowPage,@ReceiptInfo,@ETA,@ShipTo,@RequestType,@A1_Start,@EditBy,@EditDate,@CreateSys);""";
        await using var cmd = new SqlCommand(sql, c, tx); Add(cmd,"@RT_NO",r.RtNo); Add(cmd,"@Subject",r.Subject); Add(cmd,"@CreateBy",r.CreateBy); Add(cmd,"@CreateDate",r.CreateDate); Add(cmd,"@EmailTo",r.EmailTo); Add(cmd,"@Attachment",r.Attachment); Add(cmd,"@Status",r.Status); Add(cmd,"@DocumentType",r.DocumentType); Add(cmd,"@ArrivalType",r.ArrivalType); Add(cmd,"@WHCode",r.WhCode); Add(cmd,"@InvoiceNo",r.InvoiceNo); Add(cmd,"@TETPO",r.TetPo); Add(cmd,"@FLT",r.Flt); Add(cmd,"@Remark",r.Remark); Add(cmd,"@isDD",r.IsDd); Add(cmd,"@isSDriver",r.IsSDriver); Add(cmd,"@isSStacker",r.IsSStacker); Add(cmd,"@DependType",r.DependType); Add(cmd,"@ShipToCode",r.ShipToCode); Add(cmd,"@ArriveDate",r.ArriveDate); Add(cmd,"@MAWB",r.Mawb); Add(cmd,"@HAWB",r.Hawb); Add(cmd,"@R_isPriority",r.RIsPriority); Add(cmd,"@R_isAdvance",r.RIsAdvance); Add(cmd,"@DepID",r.DepId); Add(cmd,"@NowPage",r.NowPage); Add(cmd,"@ReceiptInfo",r.ReceiptInfo); Add(cmd,"@ETA",r.Eta); Add(cmd,"@ShipTo",r.ShipTo); Add(cmd,"@RequestType",r.RequestType); Add(cmd,"@A1_Start",r.A1Start); Add(cmd,"@EditBy",r.EditBy); Add(cmd,"@EditDate",r.EditDate); Add(cmd,"@CreateSys",r.CreateSys); await cmd.ExecuteNonQueryAsync(ct);
    }
    private static void Add(SqlCommand c,string n,object? v)=>c.Parameters.AddWithValue(n,v??DBNull.Value);
    private static void Validate(IlcRtArurHeader r) { var f=new List<string>(); Check("WHCode",r.WhCode,3,f); Check("TETPO",r.TetPo,30,f); Check("Attachment",r.Attachment,1000,f); Check("Subject",r.Subject,50,f); if(f.Count>0) throw new InvalidOperationException("ARUR validation failed: "+string.Join("; ",f)); }
    private static void Check(string n,string? v,int max,ICollection<string> f){if(v?.Length>max)f.Add($"{n}: source length {v.Length} exceeds maximum {max}");}
    private static string Remark(string? notes,bool forklift,string? labor,bool waste,bool driver){var p=new List<string>();if(!string.IsNullOrWhiteSpace(notes))p.Add(notes.Trim());if(forklift)p.Add("請安排堆高機");if(waste)p.Add("請處理廢棄物");if(driver)p.Add("請回報司機資訊");if(!string.IsNullOrWhiteSpace(labor))p.Add($"({labor.Trim()})");return string.Join(Environment.NewLine,p);}
    private static DateTime? ParseDate(string? v)=>DateTime.TryParse(v,CultureInfo.InvariantCulture,DateTimeStyles.AssumeLocal,out var d)?d:null;
    private static bool IsY(string? v)=>string.Equals(v?.Trim(),"Y",StringComparison.OrdinalIgnoreCase);
    private static bool TryHeader(object? raw,out Dictionary<string,string?> h){h=new(StringComparer.OrdinalIgnoreCase);if(raw is null)return false;using var d=raw switch{JsonElement element=>JsonDocument.Parse(element.GetRawText()),JsonDocument document=>JsonDocument.Parse(document.RootElement.GetRawText()),_=>JsonDocument.Parse(JsonSerializer.Serialize(raw))};if(!d.RootElement.TryGetProperty("header",out var headerElement)&&!d.RootElement.TryGetProperty("Header",out headerElement))return false;if(headerElement.ValueKind!=JsonValueKind.Object)return false;foreach(var p in headerElement.EnumerateObject())h[p.Name]=p.Value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined?null:p.Value.ToString();return h.Count>0;}
    private static string? Get(Dictionary<string,string?> h,params string[] keys){foreach(var k in keys)if(h.TryGetValue(k,out var v))return string.IsNullOrWhiteSpace(v)?null:v.Trim();return null;}
    private static string? ResolveEmpId(string? userName)
    {
        if (string.IsNullOrWhiteSpace(userName)) return null;
        var trimmed = userName.Trim();
        var separator = trimmed.LastIndexOf('\\');
        return separator >= 0 && separator < trimmed.Length - 1 ? trimmed[(separator + 1)..] : trimmed;
    }
}

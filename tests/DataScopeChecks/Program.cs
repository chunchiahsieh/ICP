using System.Text.Json;
using ICP.Data;
using ICP.Models;
using ICP.Models.Auth;
using ICP.Models.Icp;
using ICP.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

// No database connection is opened: test predicates and SQL Server translation.
using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
    .UseSqlServer("Server=localhost;Database=DataScopeChecks;Integrated Security=true;TrustServerCertificate=true").Options);
using var fiesta = new FiestaDbContext(new DbContextOptionsBuilder<FiestaDbContext>().Options);
var http = new DefaultHttpContext { Session = new TestSession() };
var accessor = new HttpContextAccessor { HttpContext = http };
var permissions = new UserResourcePermissionService(db, fiesta, accessor, Options.Create(new AppAuthOptions()));
var service = new PageDataScopeService(db, permissions, accessor);
int count = 0;
void Check(bool result, string name)
{
    if (!result) throw new Exception("FAIL: " + name);
    Console.WriteLine("PASS: " + name); count++;
}
string Scope(string table, string field, string op, params string[] values) => JsonSerializer.Serialize(new
{
    version = 1, conditions = new[] { new { table, field, @operator = op, values } }
});
var mdp = Scope("ICP_HEADER", "MDP_FLAG", "Equal", "Y");
var broker = Scope("ICP_HEADER", "BROKER", "In", "LOG KWE", "LOG YUANFAN");
var rows = new[]
{
    new IcpHeader { InvoiceNo = "yes", MdpFlag = "Y", Broker = "LOG KWE" },
    new IcpHeader { InvoiceNo = "no", MdpFlag = "N", Broker = "OTHER" },
    new IcpHeader { InvoiceNo = "null", MdpFlag = null, Broker = null }
}.AsQueryable();
Check(PageDataScopeService.ApplyDefinitions(rows, db.Model, [mdp]).Select(r => r.InvoiceNo).SequenceEqual(["yes"]), "MDP_FLAG = Y excludes N and null");
Check(PageDataScopeService.ApplyDefinitions(rows, db.Model, [broker]).Count() == 1, "BROKER IN exact values");
Check(PageDataScopeService.ApplyDefinitions(rows, db.Model, [Scope("ICP_HEADER", "BROKER", "Contains", "KWE")]).Count() == 1, "Contains and null handling");
var andScope = """
{"version":1,"conditions":[{"table":"ICP_HEADER","field":"MDP_FLAG","operator":"Equal","values":["Y"]},{"table":"ICP_HEADER","field":"BROKER","operator":"Equal","values":["OTHER"]}]}
""";
Check(!PageDataScopeService.ApplyDefinitions(rows, db.Model, [andScope]).Any(), "Conditions use AND");
Check(PageDataScopeService.ApplyDefinitions(rows, db.Model, [mdp, Scope("ICP_HEADER", "MDP_FLAG", "Equal", "N")]).Count() == 2, "Role scopes use OR");
foreach (var invalid in new string?[] { null, "ALL", "{bad", "{}", "{\"version\":1,\"conditions\":null}", Scope("WrongTable", "MDP_FLAG", "Equal", "Y"), Scope("ICP_HEADER", "WrongField", "Equal", "Y") })
    Check(PageDataScopeService.ApplyDefinitions(rows, db.Model, [mdp, invalid]).Count() == 3, "Unrestricted/invalid fallback: " + invalid);

void SetPermissions(params UserResourceItem[] items) => http.Session.SetString(UserResourcePermissionService.SessionKey,
    JsonSerializer.Serialize(items, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));
SetPermissions(
    new() { ResourceCode = "Views.Function.ShipInfo.View", IsAllowed = true, DataScopes = [mdp] },
    new() { ResourceCode = "Views.Report.ShippingReport.View", IsAllowed = true, DataScopes = [Scope("ICP_HEADER", "BU", "Equal", "CT")] },
    new() { ResourceCode = "Views.Report.MassDataReport.View", IsAllowed = true, DataScopes = [broker] },
    new() { ResourceCode = "Views.Broker.TariffData.View", IsAllowed = true, DataScopes = [Scope("TariffData", "Broker", "Equal", "LOG KWE")] });
foreach (var (controller, expected) in new[] { ("ShipInfo", "MDP_FLAG"), ("ShippingReport", "BU"), ("MassDataReport", "BROKER") })
{
    http.Request.RouteValues["controller"] = controller;
    var sql = service.Apply(db.IcpHeaders).ToQueryString();
    Check(sql.Contains("WHERE") && sql.Split("WHERE")[1].Contains(expected), controller + " SQL uses own View scope");
    var detailSql = service.ApplyDetails(db.IcpDetails).ToQueryString();
    Check(detailSql.Contains("EXISTS") && detailSql.Contains(expected) && detailSql.Contains("TET_PO"), controller + " details restricted by invoice and PO");
}
http.Request.RouteValues["controller"] = "TariffData";
var tariffSql = service.Apply(db.TariffDataRecords).ToQueryString();
Check(tariffSql.Contains("WHERE") && tariffSql.Contains("LOG KWE"), "Tariff SQL applies Broker scope");
foreach (var controller in new[] { "CompareIcpVsArUr", "ForwarderDataUpload" })
{
    http.Request.RouteValues["controller"] = controller;
    Check(!service.Apply(db.IcpHeaders).ToQueryString().Contains("WHERE"), controller + " unchanged");
}
Console.WriteLine($"{count} checks passed.");

sealed class TestSession : ISession
{
    private readonly Dictionary<string, byte[]> data = [];
    public bool IsAvailable => true;
    public string Id => "test";
    public IEnumerable<string> Keys => data.Keys;
    public void Clear() => data.Clear();
    public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task LoadAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public void Remove(string key) => data.Remove(key);
    public void Set(string key, byte[] value) => data[key] = value;
    public bool TryGetValue(string key, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out byte[]? value) => data.TryGetValue(key, out value);
}

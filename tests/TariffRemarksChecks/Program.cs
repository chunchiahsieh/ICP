using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Text.Json;
using ICP;
using ICP.Controllers;
using ICP.Data;
using ICP.Helpers;
using ICP.Models;
using ICP.Models.Auth;
using ICP.Models.Icp;
using ICP.Models.Tariff;
using ICP.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

// Model inspection, SQL translation and early controller exits only; never open a database connection.
using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
    .UseSqlServer("Server=localhost;Database=TariffRemarksChecks;Integrated Security=true;TrustServerCertificate=true").Options);
var http = new DefaultHttpContext { Session = new TestSession() };
http.Request.RouteValues["controller"] = "TariffData";
var accessor = new HttpContextAccessor { HttpContext = http };
var permissions = new UserResourcePermissionService(null!, null!, accessor, Options.Create(new AppAuthOptions()));
var scope = new PageDataScopeService(db, permissions, accessor);
// Null data dependencies make any unexpected access fail rather than query a real database.
var controller = new TariffDataController(null!, null!, Options.Create(new TariffDataOptions()),
    null!, null!, new TestLocalizer(), NullLogger<TariffDataController>.Instance, null!, null!, permissions)
{
    ControllerContext = new ControllerContext { HttpContext = http }
};
int count = 0;
void Check(bool result, string name)
{
    if (!result) throw new Exception("FAIL: " + name);
    Console.WriteLine("PASS: " + name);
    count++;
}
void SetPermissions(params UserResourceItem[] items) => http.Session.SetString(UserResourcePermissionService.SessionKey,
    JsonSerializer.Serialize(items, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));
UserResourceItem Grant(string code, params string[] scopes) => new()
{
    ResourceCode = code, ResourceType = code == TariffDataPermissionCodes.View ? "Page" : "Button",
    IsAllowed = true, DataScopes = scopes.Select(value => (string?)value).ToList()
};
bool IsStatus(IActionResult result, int status) => result is ObjectResult { StatusCode: var actual } && actual == status;
bool IsValid(TariffLogRemarksEditModel model) => Validator.TryValidateObject(model, new ValidationContext(model), [], true);
string Scope(string broker) => JsonSerializer.Serialize(new
{
    version = 1, conditions = new[] { new { table = "TariffData", field = "Broker", @operator = "Equal", values = new[] { broker } } }
});

foreach (var scenario in new[] { "no grants", "View only", "Edit only", "explicitly denied Edit" })
{
    SetPermissions(scenario switch
    {
        "View only" => [Grant(TariffDataPermissionCodes.View)],
        "Edit only" => [Grant(TariffDataPermissionCodes.Edit)],
        "explicitly denied Edit" => [Grant(TariffDataPermissionCodes.View), new UserResourceItem
        {
            ResourceCode = TariffDataPermissionCodes.Edit, ResourceType = "Button", IsAllowed = false
        }],
        _ => []
    });
    Check(IsStatus(await controller.GetLogRemarks(1), 403), $"GET denies {scenario} before database access");
    Check(IsStatus(await controller.SaveLogRemarks(new() { Id = 1, LOGRemarks = "changed" }), 403),
        $"POST denies {scenario} before database access");
}

var registry = new ResourceRouteRegistryService(null!);
Check(!registry.IsRegisteredResourceCode(TariffDataPermissionCodes.Edit), "Permission route registry starts empty");
foreach (var (action, method) in new[] { ("GetLogRemarks", "GET"), ("SaveLogRemarks", "POST") })
{
    Check(PermissionRequestResolver.Resolve(registry, "TariffData", action, method, new RouteValueDictionary(),
        new Dictionary<string, object?>()) == TariffDataPermissionCodes.Edit,
        action + " requires Edit even before permission scanning");
}

SetPermissions(Grant(TariffDataPermissionCodes.View), Grant(TariffDataPermissionCodes.Edit));
foreach (var character in new[] { 'A', '\u5099' })
{
    var maximum = new string(character, 100);
    var tooLong = new string(character, 101);
    Check(IsValid(new() { Id = 1, LOGRemarks = maximum, OriginalLOGRemarks = maximum }),
        $"Request model accepts 100 {(character == 'A' ? "ASCII" : "Chinese")} characters");
    Check(!IsValid(new() { Id = 1, LOGRemarks = tooLong }), "Request model rejects 101-character remarks");
    Check(!IsValid(new() { Id = 1, OriginalLOGRemarks = tooLong }), "Request model rejects 101-character original remarks");
    Check(IsStatus(await controller.SaveLogRemarks(new() { Id = 1, LOGRemarks = tooLong }), 400),
        "POST rejects overlength remarks before database access even without MVC model validation");
    Check(IsStatus(await controller.SaveLogRemarks(new() { Id = 1, OriginalLOGRemarks = tooLong }), 400),
        "POST rejects overlength original remarks before database access");
}
Check(IsValid(new() { Id = 1, LOGRemarks = null, OriginalLOGRemarks = null }), "Request permits clearing NULL remarks");
Check(IsValid(new() { Id = 1, LOGRemarks = "", OriginalLOGRemarks = "" }), "Request permits clearing empty remarks");
Check(IsStatus(await controller.SaveLogRemarks(new() { Id = 0 }), 400), "POST rejects invalid row ID before database access");
controller.ModelState.AddModelError("Id", "Malformed ID");
Check(IsStatus(await controller.SaveLogRemarks(new() { Id = 1 }), 400), "POST rejects invalid model state before database access");
controller.ModelState.Clear();

var saveMethod = typeof(TariffDataController).GetMethod(nameof(TariffDataController.SaveLogRemarks))!;
Check(saveMethod.IsDefined(typeof(HttpPostAttribute)) && saveMethod.IsDefined(typeof(ValidateAntiForgeryTokenAttribute))
    && !saveMethod.IsDefined(typeof(IgnoreAntiforgeryTokenAttribute)), "Saving remarks requires POST and antiforgery validation");
var getMethod = typeof(TariffDataController).GetMethod(nameof(TariffDataController.GetLogRemarks))!;
Check(getMethod.GetCustomAttribute<ResponseCacheAttribute>() is { NoStore: true, Location: ResponseCacheLocation.None },
    "Remarks read responses disable caching");

var remarksProperty = db.Model.FindEntityType(typeof(TariffData))!.FindProperty(nameof(TariffData.LOGRemarks))!;
Check(remarksProperty.GetMaxLength() == 100 && remarksProperty.IsNullable, "EF maps nullable remarks with a 100-character limit");
Check(typeof(TariffData).GetProperty(nameof(TariffData.LOGRemarks))!.GetCustomAttribute<MaxLengthAttribute>()?.Length == 100,
    "Entity validation limits remarks to 100 characters");

SetPermissions(Grant(TariffDataPermissionCodes.View, Scope("VISIBLE BROKER")),
    Grant(TariffDataPermissionCodes.Edit, Scope("OTHER BROKER")));
var original = "Case sensitive trailing space ";
var sql = scope.Apply(db.TariffDataRecords).Where(row => row.Id == 42
        && EF.Functions.Collate(row.LOGRemarks ?? string.Empty, "Latin1_General_100_BIN2") == original
        && EF.Functions.DataLength(row.LOGRemarks ?? string.Empty) == original.Length * 2)
    .ToQueryString();
Check(sql.Contains("WHERE") && sql.Contains("VISIBLE BROKER") && !sql.Contains("OTHER BROKER"),
    "Remarks query uses the page's View data scope");
Check(sql.Contains("[Id] = CAST(42 AS bigint)") || sql.Contains("[Id] = 42"), "Remarks query targets the requested row ID");
Check(sql.Contains("COLLATE Latin1_General_100_BIN2") && sql.Contains("DATALENGTH("),
    "SQL Server translates exact optimistic-concurrency comparison including byte length");
Check(sql.Contains("COALESCE(") && sql.Contains("LOGRemarks"), "Concurrency query handles stored NULL remarks");

var fullRemarks = new string('\u5099', 100);
var existing = new TariffData { LOGRemarks = fullRemarks, DescriptionOfGoods = "Before import" };
TariffCustomsImportRules.ApplyImportRow(existing, new TariffData { LOGRemarks = "Untrusted upload", DescriptionOfGoods = "After import" });
Check(existing.LOGRemarks == fullRemarks && existing.DescriptionOfGoods == "After import",
    "Broker imports preserve existing manually edited remarks while updating import fields");
Check(TariffTableViewHelper.FormatCellValue(existing, nameof(TariffData.LOGRemarks)) == fullRemarks,
    "Table and export formatting retain all 100 remark characters");
existing.LOGRemarks = null;
Check(TariffTableViewHelper.FormatCellValue(existing, nameof(TariffData.LOGRemarks)) == "", "NULL remarks render as an empty cell");

var sourceRoot = FindSourceRoot();
var scanned = new PermissionScannerService(new TestEnvironment { ContentRootPath = sourceRoot }).Scan();
Check(scanned.Any(item => item.ResourceCode == TariffDataPermissionCodes.Edit && item.ResourceType == "Button"),
    "Permission scanner discovers the tariff Edit button for existing permission assignment");
Console.WriteLine($"{count} checks passed; no database connections were opened.");

static string FindSourceRoot()
{
    foreach (var start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
    {
        for (var directory = new DirectoryInfo(start); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "ICP.csproj"))) return directory.FullName;
            var candidate = Path.Combine(directory.FullName, "ICP");
            if (File.Exists(Path.Combine(candidate, "ICP.csproj"))) return candidate;
        }
    }
    throw new InvalidOperationException("Cannot locate ICP sources for the permission scan check.");
}

sealed class TestSession : ISession
{
    private readonly Dictionary<string, byte[]> data = [];
    public bool IsAvailable => true;
    public string Id => "tariff-remarks-checks";
    public IEnumerable<string> Keys => data.Keys;
    public void Clear() => data.Clear();
    public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task LoadAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public void Remove(string key) => data.Remove(key);
    public void Set(string key, byte[] value) => data[key] = value;
    public bool TryGetValue(string key, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out byte[]? value) => data.TryGetValue(key, out value);
}

sealed class TestLocalizer : IStringLocalizer<SharedResource>
{
    public LocalizedString this[string name] => new(name, name);
    public LocalizedString this[string name, params object[] arguments] => new(name, string.Format(name, arguments));
    public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
}

sealed class TestEnvironment : IWebHostEnvironment
{
    public string ApplicationName { get; set; } = "ICP";
    public string EnvironmentName { get; set; } = "Development";
    public string ContentRootPath { get; set; } = "";
    public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    public string WebRootPath { get; set; } = "";
    public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
}

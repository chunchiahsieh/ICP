using System.ComponentModel.DataAnnotations.Schema;
using System.Data.Common;
using System.Reflection;
using ICP.Data;
using ICP.Helpers;
using ICP.Models.Auth;
using ICP.Models.Icp;
using ICP.Models.Ilc;
using ICP.Models.Tariff;
using ICP.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Options;

// In-memory behavior, EF model inspection and SQL translation only.
// Fail immediately if a future check accidentally attempts a database connection.
var connectionGuard = new NoDatabaseConnections();
using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
    .UseSqlServer("Server=localhost;Database=TariffCreatorChecks;Integrated Security=true;TrustServerCertificate=true")
    .AddInterceptors(connectionGuard).Options);
int count = 0;
void Check(bool result, string name)
{
    if (!result) throw new Exception("FAIL: " + name);
    Console.WriteLine("PASS: " + name);
    count++;
}
bool HasIds(IEnumerable<TariffData> rows, params long[] expected) => rows.Select(row => row.Id).SequenceEqual(expected);

Check(UserDisplayNameHelper.NormalizeAccount("  TEL123  ") == "TEL123", "Bare TELID is trimmed");
Check(UserDisplayNameHelper.NormalizeAccount(@"  DOMAIN\TEL123  ") == "TEL123", "Domain account resolves to its TELID");
Check(UserDisplayNameHelper.NormalizeAccount(@"ROOT\DOMAIN\TEL123") == "TEL123", "Normalization uses the last account segment");
Check(string.IsNullOrEmpty(UserDisplayNameHelper.NormalizeAccount(null))
    && string.IsNullOrEmpty(UserDisplayNameHelper.NormalizeAccount(" \t ")), "Missing accounts normalize to an empty key");

var directoryUsers = new[]
{
    new UserInfoAd { TelId = " TEL123 ", DisplayName = "  Alice Chen  " },
    new UserInfoAd { TelId = @"DOMAIN\tel123", DisplayName = "Alice Chen" },
    new UserInfoAd { TelId = "TEL456", DisplayName = "王小明" },
    new UserInfoAd { TelId = "TEL789", DisplayName = " " },
    new UserInfoAd { TelId = "TEL789", DisplayName = "Carol" },
    new UserInfoAd { TelId = "CONFLICT", DisplayName = "One Person" },
    new UserInfoAd { TelId = @"DOMAIN\conflict", DisplayName = "Other Person" },
    new UserInfoAd { TelId = "CONFLICT", DisplayName = "One Person" },
    new UserInfoAd { TelId = "EMPTYNAME", DisplayName = null },
    new UserInfoAd { TelId = " ", DisplayName = "No Account" },
    new UserInfoAd { TelId = null, DisplayName = "No Account" }
};
var names = UserDisplayNameHelper.BuildDisplayNameMap(directoryUsers);
Check(names.TryGetValue("tel123", out var alice) && alice == "Alice Chen",
    "Map accepts matching duplicates, trims display names and ignores account case");
Check(names.TryGetValue("TEL789", out var carol) && carol == "Carol", "Blank directory names do not hide a usable name");
Check(!names.ContainsKey("CONFLICT"), "Conflicting duplicate names stay excluded even after another matching duplicate");
Check(!names.ContainsKey("EMPTYNAME") && !names.ContainsKey(""), "Missing directory names or accounts are excluded");
Check(UserDisplayNameHelper.ResolveDisplayName(@"  domain\TeL123  ", names) == "Alice Chen", "Domain-qualified lookup ignores account case");
Check(UserDisplayNameHelper.ResolveDisplayName("TEL456", names) == "王小明", "Unicode display names are retained");
var unknownAccount = @" DOMAIN\UNKNOWN ";
Check(UserDisplayNameHelper.ResolveDisplayName(unknownAccount, names) == unknownAccount, "Unmapped account falls back to its exact stored value");
Check(UserDisplayNameHelper.ResolveDisplayName(@"DOMAIN\CONFLICT", names) == @"DOMAIN\CONFLICT", "Ambiguous account uses its stored value");
Check(string.IsNullOrEmpty(UserDisplayNameHelper.ResolveDisplayName(null, names)), "Missing account has an empty display value");
Check(directoryUsers[0].TelId == " TEL123 " && directoryUsers[0].DisplayName == "  Alice Chen  ", "Directory data is not mutated by lookup creation");

using var directory = new IlcDbContext(new DbContextOptionsBuilder<IlcDbContext>()
    .UseSqlServer("Server=localhost;Database=TariffCreatorDirectoryChecks;Integrated Security=true;TrustServerCertificate=true")
    .AddInterceptors(connectionGuard).Options);
var auth = new UserAuthService(directory, new HttpContextAccessor(), Options.Create(new AppAuthOptions()), null!);
Check((await auth.GetUserDisplayNamesAsync([null, "", " \t ", @"DOMAIN\"])).Count == 0,
    "Directory service skips querying when accounts contain no TELIDs");
var batch = Enumerable.Range(1, 500).Select(index => $"TEL{index:000}").ToArray();
var directorySql = directory.UserInfoAd.AsNoTracking()
    .Where(user => user.TelId != null && batch.Contains(user.TelId.Trim().ToUpper()))
    .Select(user => new UserInfoAd { TelId = user.TelId, DisplayName = user.DisplayName }).ToQueryString();
Check(directorySql.Contains("UPPER(") && directorySql.Contains("[TELID]") && directorySql.Contains("TEL500"),
    "SQL Server translates the normalized 500-account directory predicate");
Check(directorySql.Contains("[DisplayName]") && !directorySql.Contains("[EmailAddress]"),
    "Directory projection selects account and display name without unrelated directory data");

var rawAccount = @"  DOMAIN\TeL123  ";
var row = new TariffData { Id = 1, CreateUser = rawAccount };
row.CreateUserDisplayName = UserDisplayNameHelper.ResolveDisplayName(row.CreateUser, names);
Check(TariffTableViewHelper.FormatCellValue(row, nameof(TariffData.CreateUser)) == "Alice Chen",
    "Shared table and export formatting shows the creator display name");
Check(TariffTableViewHelper.FormatCellValue(row, "createuser") == "Alice Chen", "Creator formatting accepts field-name case differences");
Check(row.CreateUser == rawAccount, "Resolving and formatting a creator preserves the exact audit account");
var fallback = new TariffData { Id = 2, CreateUser = unknownAccount };
Check(TariffTableViewHelper.FormatCellValue(fallback, nameof(TariffData.CreateUser)) == unknownAccount,
    "Missing display name formats the raw account");
fallback.CreateUserDisplayName = " \t ";
Check(TariffTableViewHelper.FormatCellValue(fallback, nameof(TariffData.CreateUser)) == unknownAccount,
    "Blank display name formats the raw account");
Check(TariffTableViewHelper.FormatCellValue(new TariffData(), nameof(TariffData.CreateUser)) == "",
    "Empty creator renders an empty cell");

var entity = db.Model.FindEntityType(typeof(TariffData))!;
Check(typeof(TariffData).GetProperty(nameof(TariffData.CreateUserDisplayName))!.IsDefined(typeof(NotMappedAttribute)),
    "Display name is explicitly excluded from database mapping");
Check(entity.FindProperty(nameof(TariffData.CreateUserDisplayName)) is null, "EF does not map the transient creator display name");
Check(entity.FindProperty(nameof(TariffData.CreateUser))?.GetMaxLength() == 50, "Raw creator account retains its existing EF mapping");
db.Attach(row);
row.CreateUserDisplayName = "Another display label";
db.ChangeTracker.DetectChanges();
Check(db.Entry(row).State == EntityState.Unchanged && row.CreateUser == rawAccount,
    "Changing the display label cannot dirty the persisted audit record");
db.ChangeTracker.Clear();

var searchableCreator = new TariffTableFieldMetadata { FieldName = "CreateUser", Searchable = true, FilterType = "Text" };
TariffTableFieldMetadata[] creatorFields = [searchableCreator];
var rows = new List<TariffData>
{
    new() { Id = 1, CreateUser = @"DOMAIN\TEL123", CreateUserDisplayName = "Alice Chen", Broker = "Visible Broker" },
    new() { Id = 2, CreateUser = @"DOMAIN\TEL456", CreateUserDisplayName = "王小明", Broker = "Visible Broker" },
    new() { Id = 3, CreateUser = "service.bot", Broker = "Visible Broker" },
    new() { Id = 4, CreateUser = @"DOMAIN\TEL999", CreateUserDisplayName = "Alice Jones", Broker = "Other Broker" },
    new() { Id = 5, CreateUser = "SPECIAL", CreateUserDisplayName = "100%_[Exact]", Broker = "Visible Broker" }
};
TariffDataQueryModel CreatorSearch(string term) => new() { Text = new() { ["CreateUser"] = term } };
List<TariffData> Search(string term) => TariffQueryFilterApplier.ApplyCreateUserTextFilter(rows, CreatorSearch(term), creatorFields);
Check(HasIds(Search(" alice "), 1, 4), "Creator search matches display names with trimmed case-insensitive text");
Check(HasIds(Search("tel123"), 1), "Creator search still matches the raw TELID when a display name exists");
Check(HasIds(Search(@"domain\tel456"), 2), "Creator search accepts the full domain account");
Check(HasIds(Search("小明"), 2), "Creator search matches part of a Unicode name");
Check(HasIds(Search("BOT"), 3), "Creator search finds an unmapped raw account");
Check(HasIds(Search("%_"), 5), "Creator search treats SQL wildcard characters as literal text");
Check(HasIds(Search("absent")), "Unmatched creator search returns no rows");
Check(HasIds(Search(" \t "), 1, 2, 3, 4, 5), "Blank creator search leaves all rows available");
Check(HasIds(TariffQueryFilterApplier.ApplyCreateUserTextFilter(rows, new(), creatorFields), 1, 2, 3, 4, 5),
    "Missing creator search leaves all rows available");
Check(HasIds(TariffQueryFilterApplier.ApplyCreateUserTextFilter(rows,
    new TariffDataQueryModel { Text = new() { ["createuser"] = "alice" } }, creatorFields), 1, 4),
    "Creator search identifies the field without case sensitivity");
foreach (var (label, fields) in new (string, TariffTableFieldMetadata[])[]
{
    ("absent", []),
    ("hidden", [new() { FieldName = "CreateUser", Visible = false, Searchable = true, FilterType = "Text" }]),
    ("not searchable", [new() { FieldName = "CreateUser", Searchable = false, FilterType = "Text" }]),
    ("checkbox", [new() { FieldName = "CreateUser", Searchable = true, FilterType = "Checkbox" }])
})
{
    Check(HasIds(TariffQueryFilterApplier.ApplyCreateUserTextFilter(rows, CreatorSearch("alice"), fields), 1, 2, 3, 4, 5),
        $"Creator text search ignores a field that is {label}");
}

TariffTableFieldMetadata[] allFields =
[
    searchableCreator,
    new() { FieldName = "Broker", Searchable = true, FilterType = "Checkbox" },
    new() { FieldName = "DescriptionOfGoods", Searchable = true, FilterType = "Text" },
    new() { FieldName = "ImportDate", Searchable = true, FilterType = "DateRange" }
];
var combined = CreatorSearch("Alice");
combined.Checkbox["Broker"] = ["Visible Broker"];
var scopedRows = TariffQueryFilterApplier.ApplyFilters(rows.AsQueryable().Where(item => item.Id != 2), combined,
    allFields, deferCreateUserTextFilter: true).ToList();
Check(HasIds(TariffQueryFilterApplier.ApplyCreateUserTextFilter(scopedRows, combined, allFields), 1),
    "Creator matching combines with preceding scope and checkbox filters");
combined.Text["DescriptionOfGoods"] = "cargo";
combined.DateFrom["ImportDate"] = "2026-01-01";
combined.DateTo["ImportDate"] = "2026-09-30";
var sql = TariffQueryFilterApplier.ApplyFilters(db.TariffDataRecords.Where(item => item.Id > 100), combined,
    allFields, deferCreateUserTextFilter: true).ToQueryString();
var where = sql[sql.IndexOf("WHERE", StringComparison.Ordinal)..];
Check(where.Contains("[Id] >") && where.Contains("[Broker]") && where.Contains("Visible Broker"),
    "Deferred SQL retains the pre-existing scope and checkbox conditions");
Check(where.Contains("[DescriptionOfGoods]") && where.Contains("cargo"), "Deferred SQL retains other text search");
Check(where.Contains("[ImportDate] >=") && where.Contains("[ImportDate] <="), "Deferred SQL retains date bounds");
Check(!where.Contains("[CreateUser]") && !sql.Contains("CreateUserDisplayName") && !sql.Contains("Alice"),
    "Deferred SQL does not reject display-name matches through a raw-account predicate");
var legacySql = TariffQueryFilterApplier.ApplyFilters(db.TariffDataRecords, CreatorSearch("TEL123"), creatorFields).ToQueryString();
Check(legacySql[(legacySql.IndexOf("WHERE", StringComparison.Ordinal))..].Contains("[CreateUser]")
    && legacySql.Contains("TEL123"), "Default query behavior still supports the raw creator text predicate");

var sortable = new List<TariffData>
{
    new() { Id = 10, CreateUser = "A_RAW", CreateUserDisplayName = "Zulu" },
    new() { Id = 11, CreateUser = "Z_RAW", CreateUserDisplayName = "Alice" },
    new() { Id = 12, CreateUser = "Bob" }
};
var sortConfig = new TariffTablePageConfig
{
    Fields = creatorFields,
    InitialSort = new() { FieldName = "CreateUser", Direction = "asc" }
};
Check(HasIds(TariffTableSortHelper.Apply(sortable, sortConfig), 11, 12, 10),
    "Creator ascending sort follows displayed names and the raw fallback");
sortConfig.InitialSort.Direction = "desc";
Check(HasIds(TariffTableSortHelper.Apply(sortable, sortConfig), 10, 12, 11), "Creator descending sort follows displayed names");
Check(sortable[0].CreateUser == "A_RAW" && sortable[1].CreateUser == "Z_RAW" && HasIds(sortable, 10, 11, 12),
    "Sorting preserves raw audit accounts and input order");
Check(rows[0].CreateUser == @"DOMAIN\TEL123" && rows[0].CreateUserDisplayName == "Alice Chen",
    "Searching preserves raw audit accounts and resolved display names");
Check(connectionGuard.Attempts == 0, "All checks completed without a database connection attempt");
Console.WriteLine($"{count} checks passed; no database connections were opened.");

sealed class NoDatabaseConnections : DbConnectionInterceptor
{
    public int Attempts { get; private set; }

    public override InterceptionResult ConnectionOpening(DbConnection connection, ConnectionEventData eventData,
        InterceptionResult result)
    {
        Attempts++;
        throw new InvalidOperationException("Database connections are forbidden in TariffCreatorChecks.");
    }

    public override ValueTask<InterceptionResult> ConnectionOpeningAsync(DbConnection connection, ConnectionEventData eventData,
        InterceptionResult result, CancellationToken cancellationToken = default)
    {
        Attempts++;
        throw new InvalidOperationException("Database connections are forbidden in TariffCreatorChecks.");
    }
}

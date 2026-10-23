using System.Reflection;
using ICP.Controllers;
using ICP.Data;
using ICP.Models;
using ICP.Models.Icp;
using ICP.Models.Tariff;
using ICP.Helpers;
using ICP.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Http;
using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
db.TariffDataRecords.AddRange(
    new TariffData { InvoiceNumber = "I1", COO = "JP", Currency = "USD", TermsOfTrade = "FOB" },
    new TariffData { InvoiceNumber = "I2", COO = "JP", Currency = "USD", TermsOfTrade = "FOB" },
    new TariffData { InvoiceNumber = "I3", COO = "TW", Currency = "JPY", TermsOfTrade = "CIF" });
db.SystemConfigs.AddRange(
    new SystemConfig { Category = "Country of Origin", Key1 = "JP", Value1 = "Japan" },
    new SystemConfig { Category = "Country of Origin", Key1 = "TW", Value1 = "Taiwan" },
    new SystemConfig { Category = "Country of Origin", Key1 = "US", Value1 = "United States" },
    new SystemConfig { Category = "Country of Origin", Key1 = "DE", IsDeleted = true },
    new SystemConfig { Category = "Other", Key1 = "FR" });
await db.SaveChangesAsync();
var scope = new PageDataScopeService(db, null!, new HttpContextAccessor());
var controller = new TariffDataController(db, null!, Options.Create(new TariffDataOptions()), null!, null!, null!, null!, scope, null!, null!);
var method = typeof(TariffDataController).GetMethod("GetDistinctColumnValuesAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;
int count = 0;
void Check(bool value, string name) { if (!value) throw new Exception(name); Console.WriteLine("PASS: " + name); count++; }
foreach (var (field, expected, term) in new[] { ("COO", new[] { "JP", "TW", "US" }, "JP"), ("Currency", new[] { "JPY", "USD" }, "USD"), ("TermsOfTrade", new[] { "CIF", "FOB" }, "FOB") })
{
    var options = await (Task<List<string>>)method.Invoke(controller, new object?[] { field, null, CancellationToken.None })!;
    Check(options.SequenceEqual(expected), field + " returns expected options (COO uses active country settings, including unused codes)");
    var searched = await (Task<List<string>>)method.Invoke(controller, new object?[] { field, term, CancellationToken.None })!;
    Check(searched.SequenceEqual(new[] { term }), field + " options support search");
    var filtered = TariffQueryFilterApplier.ApplyFilters(db.TariffDataRecords, new TariffDataQueryModel { Checkbox = new() { [field] = [term] } },
        new[] { new TariffTableFieldMetadata { FieldName = field, Searchable = true, FilterType = "Checkbox" } }).ToList();
    Check(filtered.Count == 2, field + " selected option filters matching rows");
}
Console.WriteLine($"{count} checks passed; no production database connection.");

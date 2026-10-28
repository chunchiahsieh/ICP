using ClosedXML.Excel;
using ICP;
using ICP.Controllers.Setting;
using ICP.Data;
using ICP.Helpers;
using ICP.Models.Icp;
using ICP.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using System.Text.Json;
using System.Reflection;

int checks=0;
void Check(bool ok,string name) {if(!ok) throw new Exception(name);checks++;Console.WriteLine("PASS "+name);}
using var connection=new SqliteConnection("Data Source=:memory:");await connection.OpenAsync();
using var db=new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options);
await db.Database.ExecuteSqlRawAsync("""
CREATE TABLE SystemConfigs (Id INTEGER PRIMARY KEY AUTOINCREMENT,Category TEXT NOT NULL,FunctionCode TEXT NULL,Key1 TEXT NOT NULL,Key2 TEXT NOT NULL,Value1 TEXT NULL,Value2 TEXT NULL,Value3 TEXT NULL,Value4 TEXT NULL,Value5 TEXT NULL,Value6 TEXT NULL,CreateTime TEXT NOT NULL,CreateUser TEXT NULL,UpdateTime TEXT NULL,UpdateUser TEXT NULL);
""");
await db.Database.ExecuteSqlRawAsync("CREATE UNIQUE INDEX UX_SystemConfigs ON SystemConfigs(Category, IFNULL(FunctionCode,''),Key1,Key2);");
var services=new ServiceCollection();services.AddLogging();services.AddLocalization();using var provider=services.BuildServiceProvider();
var localizer=provider.GetRequiredService<IStringLocalizer<SharedResource>>();
SystemConfigControllerBase Controller(string category) {
 var type=typeof(AirSeaController).Assembly.GetType("ICP.Controllers.Setting."+category+"Controller")!;
 var instance=(SystemConfigControllerBase)(category=="Customized"
 ? Activator.CreateInstance(type,db,localizer,new PageDataScopeService(db,null!,new HttpContextAccessor()))!
 : Activator.CreateInstance(type,db,localizer)!);
 instance.ControllerContext=new ControllerContext {HttpContext=new DefaultHttpContext()};return instance;
}
SystemConfig Row(string category,string key="001",string? value="Value") => new(){Category=category,Key1=key,Key2="",Value1=value,Value2="Name",Value3="Address",Value4="City",Value5="Phone",Value6="Area",CreateTime=DateTime.UtcNow};
FormFile File(byte[] bytes,string name="Settings.xlsx") => new(new MemoryStream(bytes),0,bytes.Length,"file",name);
bool Success(IActionResult result) => result is JsonResult json&&JsonSerializer.SerializeToElement(json.Value).GetProperty("success").GetBoolean();
foreach(var category in SettingCategories.All) {
 var actual=category=="CountryOfOrigin"?"Country of Origin":category=="Customized"?"YesNo":category;
 db.SystemConfigs.Add(Row(actual));
}
await db.SaveChangesAsync();
foreach(var category in SettingCategories.All) {
 var controller=Controller(category);
 var download=(FileContentResult)await controller.DownloadExcel();
 using var book=new XLWorkbook(new MemoryStream(download.FileContents));
 Check(book.Worksheet(1).LastRowUsed()!.RowNumber()==2,category+" download contains only its own page");
 Check(book.Worksheet(1).Cell(2,3).GetString()=="001",category+" download preserves leading zeroes");
 var countBefore=await db.SystemConfigs.CountAsync();
 Check(Success(await controller.UploadExcel(File(download.FileContents))),category+" exported file can be reimported");
 Check(await db.SystemConfigs.CountAsync()==countBefore,category+" replace keeps every other page unchanged");
 var actionArguments=new Dictionary<string,object?>();
 Check(PermissionRequestResolver.Resolve(null!,category,"DownloadExcel","GET",new RouteValueDictionary(),actionArguments)==$"Views.Setting.{category}.View",category+" download requires View");
 Check(PermissionRequestResolver.Resolve(null!,category,"UploadExcel","POST",new RouteValueDictionary(),actionArguments)==$"Views.Setting.{category}.Create",category+" upload requires Create");
}
var air=Controller("AirSea");
var original=await db.SystemConfigs.AsNoTracking().Where(r=>r.Category=="AirSea").Select(r=>r.Key1).ToArrayAsync();
async Task Unchanged(string name,byte[] bytes) {
 Check(!Success(await air.UploadExcel(File(bytes))),name+" rejected");
 db.ChangeTracker.Clear();
 Check((await db.SystemConfigs.Where(r=>r.Category=="AirSea").Select(r=>r.Key1).ToArrayAsync()).SequenceEqual(original),name+" leaves old data intact");
}
await Unchanged("empty workbook",SettingsExcelHelper.Export([]));
await Unchanged("wrong page workbook",SettingsExcelHelper.Export(new[]{Row("Broker")}));
await Unchanged("missing Key",SettingsExcelHelper.Export(new[]{Row("AirSea","")}));
await Unchanged("overlong value",SettingsExcelHelper.Export(new[]{Row("AirSea","New",new string('x',1001))}));
await Unchanged("duplicate keys",SettingsExcelHelper.Export(new[]{Row("AirSea","DUP"),Row("AirSea","dup")}));
byte[] Mutate(Action<IXLWorksheet> action) {using var book=new XLWorkbook(new MemoryStream(SettingsExcelHelper.Export(new[]{Row("AirSea")})));action(book.Worksheet(1));using var stream=new MemoryStream();book.SaveAs(stream);return stream.ToArray();}
await Unchanged("wrong headers",Mutate(s=>s.Cell(1,1).Value="Unexpected"));
await Unchanged("formula",Mutate(s=>s.Cell(2,5).FormulaA1="1+1"));
Check(!Success(await air.UploadExcel(File(SettingsExcelHelper.Export(new[]{Row("AirSea")}),"Bad.xls"))),"legacy xls rejected without changing data");
await db.Database.ExecuteSqlRawAsync("CREATE TRIGGER RejectReplacement BEFORE INSERT ON SystemConfigs WHEN NEW.Key1='FAIL' BEGIN SELECT RAISE(ABORT,'test failure'); END;");
await Unchanged("database insertion failure rolls back deletion",SettingsExcelHelper.Export(new[]{Row("AirSea","FAIL")}));
var custom=Controller("Customized");
Check(!Success(await custom.UploadExcel(File(SettingsExcelHelper.Export(new[]{Row("Broker")})))),"Customized cannot import another Settings category");
Check((await db.SystemConfigs.CountAsync(r=>r.Category=="Broker"))==1,"Broker remains after rejected Customized import");
var external=Row("YesNo","EXTERNAL","Outside replacement scope");
db.SystemConfigs.Add(external);
await db.SaveChangesAsync();
var partial=new PartialCustomizedController(db,localizer,new PageDataScopeService(db,null!,new HttpContextAccessor()));
partial.ControllerContext=new ControllerContext {HttpContext=new DefaultHttpContext()};
var conflict=await partial.UploadExcel(File(SettingsExcelHelper.Export(new[]{Row("YesNo","EXTERNAL","Outside replacement scope")})));
Check(conflict is BadRequestObjectResult bad
    && JsonSerializer.SerializeToElement(bad.Value).GetProperty("message").GetString() is string message
    && message.Contains("第 2 列") && message.Contains($"Id {external.Id}"),
    "outside-scope duplicate reports Excel row and existing Id");
db.ChangeTracker.Clear();
Check(await db.SystemConfigs.AnyAsync(r=>r.Id==external.Id) && await db.SystemConfigs.AnyAsync(r=>r.Category=="YesNo"&&r.Key1=="001"),"outside-scope duplicate rolls back replacement deletion");
var existing=(await db.SystemConfigs.AsNoTracking().SingleAsync(r=>r.Category=="DeliveryToList"));
Check(existing.Value2=="Name"&&existing.Value3=="Address"&&existing.Value6=="Area","DeliveryToList roundtrip preserves all fields");
Check(typeof(SystemConfigControllerBase).GetMethod("UploadExcel")!.GetCustomAttribute<ValidateAntiForgeryTokenAttribute>() is not null,"upload requires antiforgery token");
var airRow=await db.SystemConfigs.AsNoTracking().SingleAsync(r=>r.Category=="AirSea");
Check(Success(await air.Delete(airRow.Id)),"single delete succeeds");
Check(!await db.SystemConfigs.AnyAsync(r=>r.Id==airRow.Id),"single delete physically removes row");
Check(Success(await air.UploadExcel(File(SettingsExcelHelper.Export(new[]{airRow})))),"same setting can be imported after delete");
var readdedId=await db.SystemConfigs.Where(r=>r.Category=="AirSea").Select(r=>r.Id).SingleAsync();
Check(Success(await air.BatchDelete(new ICP.Models.SystemConfigBatchDeleteModel {Ids=[readdedId]})),"batch delete succeeds");
Check(!await db.SystemConfigs.AnyAsync(r=>r.Id==readdedId),"batch delete physically removes row");
var blankRow=new SystemConfig {Category="AirSea",Key1="BLANK",Key2="",Value1=null,Value2=null};
var blankWorkbook=SettingsExcelHelper.Export(new[]{blankRow});
var blankParsed=SettingsExcelHelper.Parse(new MemoryStream(blankWorkbook)).Single().Model;
Check(blankParsed.FunctionCode is null && blankParsed.Value1 is null && blankParsed.Value2 is null && blankParsed.Key2=="", "Excel blank cells parse as NULL while Key2 stays empty");
Check(Success(await air.UploadExcel(File(blankWorkbook))),"blank setting imports successfully");
var importedBlank=await db.SystemConfigs.AsNoTracking().SingleAsync(r=>r.Category=="AirSea");
Check(importedBlank.FunctionCode is null && importedBlank.Value1 is null && importedBlank.Value2 is null && importedBlank.Key2=="", "Excel export-import preserves NULL fields");
Check(Success(await air.Save(new ICP.Models.SystemConfigEditModel {Key1="MANUAL",Value1=""})),"manual save accepts blank value");
Check((await db.SystemConfigs.AsNoTracking().SingleAsync(r=>r.Category=="AirSea"&&r.Key1=="MANUAL")).Value1 is null,"manual blank value stores NULL");
Console.WriteLine($"{checks} checks passed; SQLite in-memory database only, no production changes.");

public sealed class PartialCustomizedController(ApplicationDbContext db,IStringLocalizer<SharedResource> localizer,PageDataScopeService scope)
    : CustomizedController(db,localizer,scope)
{
    protected override IQueryable<SystemConfig> ReplacementScopeQuery(IQueryable<SystemConfig> query) =>
        base.ReplacementScopeQuery(query).Where(row=>row.Key1!="EXTERNAL");
}

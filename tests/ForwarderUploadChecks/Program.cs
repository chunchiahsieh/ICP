using System.ComponentModel.DataAnnotations.Schema;
using System.Reflection;
using ClosedXML.Excel;
using ICP.Controllers;
using ICP.Data;
using ICP.Helpers;
using ICP.Models;
using ICP.Models.Icp;
using ICP.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.FileProviders;
using Microsoft.AspNetCore.Routing;
using System.Text.Json;

int checks=0;
void Check(bool ok,string message) {if(!ok) throw new Exception(message);checks++;Console.WriteLine("PASS "+message);}
var temp=Path.Combine(Path.GetTempPath(),"forwarder-checks-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(temp);
using var connection=new SqliteConnection("Data Source=:memory:");await connection.OpenAsync();
using var db=new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options);
foreach(var type in new[]{typeof(SystemConfig),typeof(ForwarderDataUpload),typeof(ForwarderDataUploadArchive)}) {
 var entity=db.Model.FindEntityType(type)!;var store=StoreObjectIdentifier.Table(entity.GetTableName()!,entity.GetSchema());
 var columns=entity.GetProperties().Select(p=>"\""+p.GetColumnName(store)+"\" "+(p.Name=="Id"?"INTEGER PRIMARY KEY AUTOINCREMENT":p.ClrType==typeof(bool)?"INTEGER DEFAULT 0":(Nullable.GetUnderlyingType(p.ClrType)??p.ClrType)==typeof(long)?"INTEGER":"TEXT"));
 await db.Database.ExecuteSqlRawAsync("CREATE TABLE \""+entity.GetTableName()+"\" ("+string.Join(",",columns)+");");
}
db.SystemConfigs.Add(new SystemConfig {Category="Broker",Key1="BROKER-KEY",Value1="Broker Display",CreateTime=DateTime.Now});await db.SaveChangesAsync();
var service=new ForwarderDataImportService(db);
var headers=new[]{"Type","Invoice No","Customer Reference","Material Code","Order Material Name","Quantity","Port of Loading","Ship to Name","Ship to Address","Ship to Party Country Code","Ship to Port Code","Freight charge","Confirmed Custom Date","HAWB","MAWB","ETD","ETA","FLIGHT 1#","FLIGHT 2#","C/B","Action","MDP"};
Dictionary<string,object> Valid() {
 var row=headers.ToDictionary(h=>h,h=>(object)"VALUE");
 row["Invoice No"]="001234";row["Quantity"]=12.5;row["Freight charge"]=25;row["Material Code"]="000987";
 foreach(var f in new[]{"Confirmed Custom Date","ETD","ETA"}) row[f]="2026-10-20";
 foreach(var f in new[]{"Customer Reference","FLIGHT 2#","Action","MDP"}) row[f]="";
 row["C/B"]="BROKER-KEY";return row;
}
string Write(Dictionary<string,object>? row=null,string sheetName="Upload Data",string? omit=null,bool dateCells=false) {
 var file=Path.Combine(temp,DateTime.Now.ToString("yyyyMMddHHmmssfff")+"_"+Guid.NewGuid()+".xlsx");
 using var book=new XLWorkbook();var sheet=book.AddWorksheet(sheetName);var hs=headers.Where(h=>h!=omit).ToArray();row??=Valid();
 for(var c=0;c<hs.Length;c++) {
  sheet.Cell(1,c+1).Value=hs[c];
  var value=row[hs[c]];
  if(dateCells&&hs[c] is "Confirmed Custom Date" or "ETD" or "ETA") {sheet.Cell(2,c+1).Value=new DateTime(2026,10,20);sheet.Cell(2,c+1).Style.NumberFormat.Format="yyyy/mm/dd";}
  else sheet.Cell(2,c+1).Value=XLCellValue.FromObject(value);
 }
 book.AddWorksheet("Other Data").Cell("A1").Value="ignored";book.SaveAs(file);return file;
}
async Task Reject(string name,Dictionary<string,object> row,string? omit=null,string sheet="Upload Data") {
 var before=await db.ForwarderDataUploads.CountAsync();var rejected=false;
 try {await service.ParseAsync(Write(row,sheet,omit),"tester");}catch(InvalidOperationException ex){rejected=ex.Message.Contains(name,StringComparison.OrdinalIgnoreCase);}
 Check(rejected,name+" invalid upload rejected");Check(await db.ForwarderDataUploads.CountAsync()==before,name+" existing data unchanged");
}
try {
 Check(ForwarderImportRules.RequiredFields.Count==18,"18 required fields");
 var parsed=await service.ParseAsync(Write(),"tester");
 Check(parsed.Count==1&&parsed[0].Quantity==12.5m,"numeric Quantity remains numeric, not OA date");
 Check(parsed[0].FreightCharge=="25","numeric Freight charge remains numeric text");
 Check(parsed[0].MaterialCode=="000987"&&parsed[0].InvoiceNo=="001234","text identifiers retain leading zeroes");
 Check(parsed[0].Flight2 is null&&parsed[0].Action is null&&parsed[0].Mdp is null&&parsed[0].CustomerReference is null,"four optional fields may be blank");
 foreach(var field in ForwarderImportRules.RequiredFields.Values) {var row=Valid();row[field]=" ";await Reject(field,row);}
 foreach(var field in ForwarderImportRules.RequiredFields.Values) await Reject(field,Valid(),omit:field);
 foreach(var field in new[]{"Confirmed Custom Date","ETD","ETA"}) {
  var row=Valid();row[field]="2026/10/20";var values=await service.ParseAsync(Write(row),"tester");
  Check(values.Count==1,field+" accepts yyyy/MM/dd");
  foreach(var bad in new[]{"20261020","2026-1-02","2026/02/30"}) {row=Valid();row[field]=bad;await Reject(field,row);}
 }
 Check((await service.ParseAsync(Write(dateCells:true),"tester"))[0].Eta==new DateTime(2026,10,20),"typed Excel date cells accepted");
 var display=new ForwarderDataUploadRowViewModel {Row=parsed[0]};
 foreach(var field in new[]{"ConfirmedCustomDate","Etd","Eta"}) Check(ForwarderTableViewHelper.FormatCellValue(display,field)=="2026-10-20",field+" displays yyyy-MM-dd");
 var wrong=Valid();wrong["C/B"]="Broker Display";await Reject("C/B",wrong);
 var broker=await db.SystemConfigs.SingleAsync();broker.IsDeleted=true;await db.SaveChangesAsync();await Reject("C/B",Valid());broker.IsDeleted=false;await db.SaveChangesAsync();
 await Reject("Upload Data",Valid(),sheet:"Wrong");
 var history=new ForwarderUploadHistoryService(db,new Env {ContentRootPath=temp},new ForwarderDataUploadOptions {StoragePath=temp});
 Check((await history.GetAsync()).Count==0,"preview-only files are not history");
 var savedPath=Write();var saved=await service.SaveAsync(savedPath,"tester");Check(saved.Success,"valid file saves successfully");
 var rows=await history.GetAsync();Check(rows.Count==1&&rows[0].FileExists&&rows[0].RowCount==1,"successful batch appears with download availability");
 Check(await history.GetDownloadPathAsync(Path.GetFileName(savedPath))==savedPath,"saved original downloadable");
 Check(await history.GetDownloadPathAsync(Path.GetFileName(Write())) is null,"pending unsaved file cannot be downloaded");
 Check(await history.GetDownloadPathAsync("../outside.xlsx") is null,"path traversal rejected");
 var overwrite=await service.SaveAsync(Write(),"tester",true);Check(overwrite.Success&&overwrite.OverwrittenCount==1,"existing overwrite archive behavior preserved");
 rows=await history.GetAsync();Check(rows.Count==2,"overwritten batch remains in history");
 Check(await history.GetDownloadPathAsync(Path.GetFileName(savedPath))==savedPath,"overwritten original still downloadable");
 File.Delete(savedPath);Check(!(await history.GetAsync()).Single(r=>r.StoredFileName==Path.GetFileName(savedPath)).FileExists,"missing original is flagged");
 Check(await history.GetDownloadPathAsync(Path.GetFileName(savedPath)) is null,"missing original returns no download");
 foreach(var action in new[]{"GetUploadHistory","DownloadUploadedFile"}) Check(PermissionRequestResolver.Resolve(null!,"ForwarderDataUpload",action,"GET",new RouteValueDictionary(),new Dictionary<string,object?>())=="Views.Forwarder.ForwarderDataUpload.View",action+" requires page View permission");
 using(var sample=new XLWorkbook("ICP/Files/ForwarderDataUploadTemplate.xlsx")) {
  var upload=sample.Worksheet(1);
  Check(upload.Name=="Upload Data","new Sample first sheet is Upload Data");
  var templateHeaders=upload.Row(1).CellsUsed().Select(cell=>cell.GetString()).ToArray();
  Check(templateHeaders.Length==22,"new Sample contains 22 upload columns");
  Check(templateHeaders.All(header=>ForwarderExcelColumnMap.TryResolveProperty(header,out _)),"all supplied headers including Invoice No. map to code");
  var guide=sample.Worksheet("填寫範例");
  foreach(var (header,index) in templateHeaders.Select((header,index)=>(header,index))) {
   ForwarderExcelColumnMap.TryResolveProperty(header,out var property);
   var mandatory=ForwarderImportRules.RequiredFields.ContainsKey(property);
   Check(guide.Cell(1,index+1).GetString().Contains(mandatory?"必填":"選填"),header+" required/optional marking agrees with code");
  }
  db.SystemConfigs.AddRange(new SystemConfig {Category="Broker",Key1="YUAN FAN",CreateTime=DateTime.Now},new SystemConfig {Category="Broker",Key1="KWE",CreateTime=DateTime.Now});await db.SaveChangesAsync();
  using var fixture=new XLWorkbook();var fixtureSheet=fixture.AddWorksheet("Upload Data");
  for(var c=1;c<=22;c++) {
   fixtureSheet.Cell(1,c).Value=upload.Cell(1,c).Value;
   for(var r=4;r<=7;r++) {fixtureSheet.Cell(r-2,c).Value=guide.Cell(r,c).Value;fixtureSheet.Cell(r-2,c).Style=guide.Cell(r,c).Style;}
  }
  var fixturePath=Path.Combine(temp,"supplied-template-examples.xlsx");fixture.SaveAs(fixturePath);
  var actual=await service.ParseAsync(fixturePath,"tester");
  Check(actual.Count==4,"all four supplied examples parse using current importer and Broker keys");
  Check(actual[0].InvoiceNo=="080362200600"&&actual[2].InvoiceNo=="010165563000","supplied examples retain invoice leading zeroes");
  Check(actual[0].Quantity==5&&actual[1].Quantity==6,"supplied example quantities parse correctly");
  Check(actual.All(row=>row.ConfirmedCustomDate.HasValue&&row.Etd.HasValue&&row.Eta.HasValue),"supplied Excel date cells parse correctly");
  var blankRejected=false;try{await service.ParseAsync("ICP/Files/ForwarderDataUploadTemplate.xlsx","tester");}catch(InvalidOperationException ex){blankRejected=ex.Message.Contains("沒有可匯入");}
  Check(blankRejected,"empty Upload Data does not import the reference examples sheet");
 }
 Console.WriteLine($"{checks} checks passed; temporary workbook fixtures and SQLite memory database only.");
} finally {Directory.Delete(temp,true);}
class Env:IWebHostEnvironment {
 public string ApplicationName {get;set;}="ICP";public string EnvironmentName {get;set;}="Development";
 public string ContentRootPath {get;set;}="";public string WebRootPath {get;set;}="";
 public IFileProvider ContentRootFileProvider {get;set;}=new NullFileProvider();public IFileProvider WebRootFileProvider {get;set;}=new NullFileProvider();
}

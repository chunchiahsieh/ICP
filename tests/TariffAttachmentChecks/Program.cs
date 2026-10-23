using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using System.Reflection;
using ICP;
using ICP.Controllers;
using ICP.Data;
using ICP.Helpers;
using ICP.Models;
using ICP.Models.Icp;
using ICP.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

int checks=0;
void Check(bool ok,string name) { if(!ok) throw new Exception(name);checks++;Console.WriteLine("PASS "+name); }
JsonElement Data(IActionResult result) => JsonSerializer.SerializeToElement(((JsonResult)result).Value);
var temp=Path.Combine(Path.GetTempPath(),"tariff-attachments-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(temp);
using var db=new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
db.TariffDataRecords.AddRange(new TariffData { HAWB="001234",InvoiceNumber="I1" },new TariffData {HAWB="001234",InvoiceNumber="I2"});await db.SaveChangesAsync();
var services=new ServiceCollection();services.AddLogging();services.AddLocalization();using var provider=services.BuildServiceProvider();
var env=new TestEnv {ContentRootPath=temp};
var scope=new PageDataScopeService(db,null!,new HttpContextAccessor());
var controller=new TariffDataController(db,env,Options.Create(new TariffDataOptions {StoragePath=temp}),null!,null!,provider.GetRequiredService<IStringLocalizer<SharedResource>>(),NullLogger<TariffDataController>.Instance,scope,null!,null!) {ControllerContext=new ControllerContext {HttpContext=new DefaultHttpContext()}};
FormFile Upload(string filename,string content="test") => new(new MemoryStream(System.Text.Encoding.UTF8.GetBytes(content)),0,System.Text.Encoding.UTF8.GetByteCount(content),"file",filename);
try {
 foreach(var ext in new[]{".pdf",".xls",".xlsx"}) {
  var result=Data(await controller.UploadImportDoc(Upload("001234"+ext)));
  Check(result.GetProperty("success").GetBoolean(),"Import Doc accepts "+ext);
  Check(db.TariffDataRecords.All(row=>row.ImportDoc=="import-doc/001234"+ext),"shared HAWB rows record "+ext+" path");
  Check(File.Exists(Path.Combine(temp,"import-doc","001234"+ext)),"file stored in import-doc: "+ext);
  await Task.Delay(5);
 }
 var listed=Data(await controller.ListAttachments("001234","import-doc"));
 Check(listed.GetProperty("files").GetArrayLength()==3,"current file plus two prior versions listed");
 Check(listed.GetProperty("files")[0].GetProperty("isCurrent").GetBoolean(),"current listed first");
 var download=(PhysicalFileResult)await controller.DownloadAttachment("import-doc","001234");
 Check(download.ContentType.Contains("spreadsheetml"),"xlsx download MIME");
 var old=listed.GetProperty("files")[2].GetProperty("originalFileName").GetString();
 Check(await controller.DownloadAttachment("import-doc","001234",old) is PhysicalFileResult,"historical file downloadable");
 Check((await controller.DownloadAttachment("import-doc","001234","../other.pdf")) is NotFoundResult,"unlisted filename cannot be downloaded");
 Check(!Data(await controller.UploadImportDoc(Upload("MISSING.pdf"))).GetProperty("success").GetBoolean(),"unknown HAWB rejected");
 foreach(var ext in new[]{".tif",".exe",".txt",".pdf.exe"}) Check(!Data(await controller.UploadImportDoc(Upload("001234"+ext))).GetProperty("success").GetBoolean(),"Import Doc rejects "+ext);
 Check(!Data(await controller.UploadImportDoc(null)).GetProperty("success").GetBoolean(),"empty upload rejected");
 Check(Data(await controller.UploadDeclarationPdf(Upload("001234.TIF"))).GetProperty("success").GetBoolean(),"Declaration accepts uppercase TIF");
 Check(((PhysicalFileResult)await controller.DownloadAttachment("pdf","001234")).ContentType=="image/tiff","TIF download MIME");
 Check(TariffAttachmentHelper.CollectHawbsWithDeclarationPdf(temp,[]).Contains("001234"),"TIF included in declaration presence");
 Check(!Data(await controller.UploadDeclarationPdf(Upload("001234.xlsx"))).GetProperty("success").GetBoolean(),"Declaration rejects xlsx");
 Check(Data(await controller.UploadDeclarationPdf(Upload("001234.pdf"))).GetProperty("success").GetBoolean(),"Declaration still accepts PDF");
 Check(Data(await controller.UploadCost(Upload("001234.xls"))).GetProperty("success").GetBoolean(),"Cost still accepts xls");
 Check(!Data(await controller.UploadCost(Upload("001234.pdf"))).GetProperty("success").GetBoolean(),"Cost still rejects PDF");
 var has=TariffAttachmentHelper.CollectHawbsWithImportDoc(temp,db.TariffDataRecords);
 Check(has.Contains("001234"),"Import Doc presence includes current attachment");
 Check(TariffAttachmentHelper.ApplyPresenceFilter(db.TariffDataRecords,["Has"],has).Count()==2,"Has selects all HAWB rows");
 Check(TariffAttachmentHelper.ApplyPresenceFilter(db.TariffDataRecords,["None"],has).Count()==0,"None excludes existing attachment");
 Check(Data(await controller.DeleteAttachment("001234","import-doc")).GetProperty("success").GetBoolean(),"delete current Import Doc");
 Check(db.TariffDataRecords.All(row=>row.ImportDoc=="import-doc/001234.xls"),"deletion promotes previous extension and updates all rows");
 Check(File.Exists(Path.Combine(temp,"import-doc","001234.xls")),"previous version restored on disk");
 await controller.DeleteAttachment("001234","import-doc");await controller.DeleteAttachment("001234","import-doc");
 Check(db.TariffDataRecords.All(row=>row.ImportDoc==null),"last deletion clears paths");
 Check(!TariffAttachmentHelper.CollectHawbsWithImportDoc(temp,db.TariffDataRecords).Contains("001234"),"recycled attachments excluded from presence");
 Check(TariffMetadataHelper.IsAttachmentPresenceField("ImportDoc")&&TariffMetadataHelper.IsVirtualField("ImportDoc"),"Import Doc uses attachment filter/rendering");
 var importExisting=typeof(TariffDataImportService).GetMethod("ApplyExistingAttachments",BindingFlags.NonPublic|BindingFlags.Instance)!;
 var importService=new TariffDataImportService(db,env,Options.Create(new TariffDataOptions {StoragePath=temp}),new EphemeralDataProtectionProvider());
 File.WriteAllText(Path.Combine(temp,"import-doc","001234.pdf"),"restored");
 var row=new TariffData {HAWB="001234"};importExisting.Invoke(importService,new object[]{new[]{row}});
 Check(row.ImportDoc=="import-doc/001234.pdf","customs reimport reconnects existing Import Doc");
 var modelProperty=db.Model.FindEntityType(typeof(TariffData))!.FindProperty(nameof(TariffData.ImportDoc))!;
 Check(modelProperty.GetMaxLength()==500&&modelProperty.IsNullable,"Import Doc database path is nullable nvarchar(500)");
 var fields = new List<ICP.Models.Tariff.TariffTableFieldMetadata> {
  new() {FieldName="ImportDoc",Visible=true,Searchable=true,FilterType="Checkbox"}
 };
 var filterMethod=typeof(TariffDataController).GetMethod("ApplyAttachmentPresenceFiltersAsync",BindingFlags.NonPublic|BindingFlags.Instance)!;
 var filtered=await (Task<IQueryable<TariffData>>)filterMethod.Invoke(controller,new object[]{db.TariffDataRecords,new ICP.Models.Tariff.TariffDataQueryModel {Checkbox=new(){["ImportDoc"]=["Has"]}},fields,CancellationToken.None})!;
 Check(filtered.Count()==2,"controller Import Doc Has filter finds shared HAWB disk attachment");
 var exportMethod=typeof(TariffDataController).GetMethod("ResolveExportCellValue",BindingFlags.NonPublic|BindingFlags.Instance);
 if(exportMethod is not null) Check(!string.IsNullOrWhiteSpace((string?)exportMethod.Invoke(controller,new object[]{row,"ImportDoc",1,temp})),"Import Doc Excel state resolves");
 Console.WriteLine($"{checks} checks passed; temporary files and in-memory database only.");
} finally { Directory.Delete(temp,true); }
class TestEnv:IWebHostEnvironment {
 public string ApplicationName {get;set;}="ICP";public string EnvironmentName {get;set;}="Development";
 public string ContentRootPath {get;set;}="";public string WebRootPath {get;set;}="";
 public IFileProvider ContentRootFileProvider {get;set;}=new NullFileProvider();public IFileProvider WebRootFileProvider {get;set;}=new NullFileProvider();
}

using System.Text.Json;
using ICP;
using ICP.Data;
using ICP.Helpers;
using ICP.Models.Icp;
using ICP.Models.Report;
using ICP.Models.ShipInfo;
using ICP.Repositories;
using ICP.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Localization;
using ClosedXML.Excel;

int checks=0;
void Check(bool ok,string message) { if(!ok) throw new Exception(message); checks++; Console.WriteLine("PASS "+message); }
var root=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../../../"));
var options=JsonSerializer.Deserialize<ShipInfoProDataTableFieldsOptions>(File.ReadAllText(Path.Combine(root,"ICP/Config/mass-data-report-table-fields.json")))!;
var services=new ServiceCollection(); services.AddLogging(); services.AddLocalization();
services.Configure<ShipInfoProDataTableFieldsOptions>(ReportKeys.MassDataReport,o=>{o.Header=options.Header;o.Detail=options.Detail;}); services.AddSingleton<ReportTableFieldsOptions>();
using var provider=services.BuildServiceProvider();
var metadata=new ReportMetadataProvider(provider.GetRequiredService<ReportTableFieldsOptions>(),provider.GetRequiredService<IStringLocalizer<SharedResource>>(),provider.GetRequiredService<IStringLocalizerFactory>(),NullLogger<ReportMetadataProvider>.Instance);
var config=metadata.GetPageConfig(ReportKeys.MassDataReport,"en");
var expected=new[]{"InvoiceNo","NcdrNo","Owner","EndUser","ArrivalNotice","SaDate","Forwarder","Broker","Shipper","Eta","Mawb","Hawb","Flt","DeliveryDate","MdpFlag","ReasonForDeliveryDelay","DelayNotificationDate"};
Check(ShipInfoTableViewHelper.GetVisibleFields(config.HeaderFields).Select(f=>f.FieldName).SequenceEqual(expected),"17 requested columns in exact order");
Check(config.DetailFields.Count==0,"no detail fields in page config");
Check(config.HeaderInitialSort!.FieldName=="CreateTime"&&config.HeaderInitialSort.Direction=="desc","default CreateTime descending");
var multi=new[]{"InvoiceNo","NcdrNo","Mawb","Hawb"};
Check(config.HeaderFields.Where(f=>f.MultiValueText).Select(f=>f.FieldName).Order().SequenceEqual(multi.Order()),"only four requested fields use multiline exact matching");
foreach(var field in new[]{"SaDate","Eta","DeliveryDate","DelayNotificationDate"}) Check(config.HeaderFields.Single(f=>f.FieldName==field).FilterType=="DateRange",field+" uses DateRange");
using var db=new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
foreach(var (invoice,create,sa) in new[]{("1",new DateTime(2026,10,20),"2026-01-02"),("2",new DateTime(2026,10,19),"2026-10-22"),("12",new DateTime(2026,10,21),"2026-01-01")})
 db.IcpHeaders.Add(new IcpHeader {InvoiceNo=invoice,TetPo="P",NcdrNo=invoice,Mawb=invoice,Hawb=invoice,CreateTime=create,SaDate=sa,DelayNotificationDate="2026-10-20",Broker=invoice=="12"?null:"B"});
db.IcpDetails.AddRange(new IcpDetail {InvoiceNo="1",TetPo="P",InvoiceSeq=1,ItemNo="A"},new IcpDetail {InvoiceNo="1",TetPo="P",InvoiceSeq=2,ItemNo="B"}); await db.SaveChangesAsync();
var scope=new PageDataScopeService(db,null!,new HttpContextAccessor());var report=new ReportDataService(metadata,new ShipInfoRepository(db,scope),db,scope);
var result=await report.QueryHeaderTableAsync(ReportKeys.MassDataReport,new());
Check(!result.ShowHeaderSelection,"no header selection radio column");
Check(result.Items.Select(r=>r["InvoiceNo"]?.ToString()).SequenceEqual(new[]{"12","1","2"}),"query sorts by creation time rather than SA date or invoice");
foreach(var field in multi) {
 var selected=await report.QueryHeaderTableAsync(ReportKeys.MassDataReport,new(){Text=new(){[field]=" 1\r\n2\n1\n "}});
 Check(selected.Items.Select(r=>r["InvoiceNo"]?.ToString()).SequenceEqual(new[]{"1","2"}),field+" multiline IN returns 1 and 2, excludes 12");
}
var dates=await report.QueryHeaderTableAsync(ReportKeys.MassDataReport,new(){DateFrom=new(){["DelayNotificationDate"]="2026-10-20"},DateTo=new(){["DelayNotificationDate"]="2026-10-20"}});
Check(dates.Items.Count==3,"Delay Notification Date range works");
var blank=await report.GetHeaderFilterOptionsAsync(ReportKeys.MassDataReport,"Broker",null);
Check(blank.Contains(ShipInfoDistinctValuesHelper.BlankFilterValue),"checkbox retains blank option");
var export=await report.ExportExcelAsync(ReportKeys.MassDataReport,new(){Text=new(){["InvoiceNo"]="1\n2"}},"Mass Data Report");
using var workbook=new XLWorkbook(new MemoryStream(export.Content));var sheet=workbook.Worksheet(1);
Check(sheet.LastColumnUsed()!.ColumnNumber()==17,"Excel contains exactly 17 Header columns");
Check(sheet.Row(1).CellsUsed().Select(c=>c.GetString()).SequenceEqual(ShipInfoTableViewHelper.GetVisibleFields(report.GetPageConfig(ReportKeys.MassDataReport).HeaderFields).Select(f=>ShipInfoTableViewHelper.ResolveLabel(f,report.GetPageConfig(ReportKeys.MassDataReport).Culture))),"Excel labels and order equal visible Header");
Check(sheet.LastRowUsed()!.RowNumber()==3,"two Headers export two rows despite multiple Detail records");
Check(sheet.Cell(2,1).GetString()=="1"&&sheet.Cell(3,1).GetString()=="2","Excel uses multiline filter and CreateTime ordering");
Check(File.ReadAllText(Path.Combine(root,"ICP/Config/mass-data-report-table-fields.json"))==File.ReadAllText(Path.Combine(root,"ICP/Config/mass-data-report-table-fields.example.json")),"production and example config match");
Console.WriteLine($"{checks} checks passed. No production database connection.");

using System.Text.Json;
using ICP.Models.Forwarder;
using ICP.Models.Tariff;
using ICP.Services;
using Microsoft.Extensions.DependencyInjection;

int checks=0;
void Check(bool ok,string text){if(!ok) throw new Exception(text);checks++;Console.WriteLine("PASS "+text);}
var options=JsonSerializer.Deserialize<ForwarderTableFieldsOptions>(File.ReadAllText("ICP/Config/forwarder-upload-history-table-fields.json"))!;
var services=new ServiceCollection();services.Configure<ForwarderTableFieldsOptions>(ForwarderHistoryTableMetadataProvider.OptionsName,o=>{o.List=options.List;o.InitialSort=options.InitialSort;o.TableUi=options.TableUi;});services.AddSingleton<ForwarderHistoryTableMetadataProvider>();
using var provider=services.BuildServiceProvider();var metadata=provider.GetRequiredService<ForwarderHistoryTableMetadataProvider>();var config=metadata.GetConfig();
Check(config.Fields.Select(f=>f.FieldName).SequenceEqual(new[]{"FileName","ImportedTime","ImportedBy","RowCount","Status","DownloadOriginal"}),"six fields in config order");
Check(config.Fields.Single(f=>f.FieldName=="ImportedTime").FilterType=="DateRange","date range configured");
var rows=new[]{new ForwarderUploadHistoryRow("1.xlsx","January.xlsx",new DateTime(2026,1,1,0,0,0),"USER1",12,true),new ForwarderUploadHistoryRow("2.xlsx","October.xlsx",new DateTime(2026,10,20,23,59,59),"USER2",6,false),new ForwarderUploadHistoryRow("3.xlsx","October-extra.xlsx",new DateTime(2026,10,21),"",20,true)};
List<ForwarderUploadHistoryRow> Filter(TariffDataQueryModel criteria)=>ForwarderHistoryTableMetadataProvider.Filter(rows,criteria,metadata.GetConfig());
Check(Filter(new()).Select(r=>r.StoredFileName).SequenceEqual(new[]{"3.xlsx","2.xlsx","1.xlsx"}),"default newest first before paging");
Check(Filter(new(){Text=new(){["FileName"]="october"}}).Count==2,"file name text matching case-insensitive");
Check(Filter(new(){Text=new(){["RowCount"]="12"}}).Single().RowCount==12,"numeric count text filter");
Check(Filter(new(){Checkbox=new(){["ImportedBy"]=["USER1","USER2"]}}).Count==2,"uploader multi-select OR");
Check(Filter(new(){Checkbox=new(){["ImportedBy"]=["__BLANK__"]}}).Single().ImportedBy=="","blank uploader supported");
Check(Filter(new(){Checkbox=new(){["Status"]=["Imported"]}}).Count==3,"import status checkbox");
Check(Filter(new(){Checkbox=new(){["DownloadOriginal"]=["Has"]}}).Count==2,"download available filter");
Check(Filter(new(){Checkbox=new(){["DownloadOriginal"]=["None"]}}).Single().StoredFileName=="2.xlsx","missing-file filter");
Check(Filter(new(){DateFrom=new(){["ImportedTime"]="2026-10-20"},DateTo=new(){["ImportedTime"]="2026-10-20"}}).Single().StoredFileName=="2.xlsx","date range includes full end day");
Check(Filter(new(){Text=new(){["FileName"]="October"},Checkbox=new(){["DownloadOriginal"]=["Has"]}}).Single().StoredFileName=="3.xlsx","different column filters combine with AND");
Check(Filter(new(){Text=new(){["Unknown"]="BAD"}}).Count==3,"unconfigured filters ignored");
options.List!.Fields.Single(f=>f.FieldName=="FileName").Searchable=false;
Check(Filter(new(){Text=new(){["FileName"]="BAD"}}).Count==3,"searchable false disables filtering");
options.List.Fields.Single(f=>f.FieldName=="FileName").Visible=false;
Check(metadata.GetConfig().Fields.Count(f=>f.Visible)==5,"visibility comes from config");
options.List.Fields.Reverse();Check(metadata.GetConfig().Fields.First().FieldName=="DownloadOriginal","config reordering applied");
options.InitialSort=new(){FieldName="RowCount",Direction="asc"};
// Named options already hold the original sort object, so update that object itself.
config.InitialSort!.FieldName="RowCount";config.InitialSort.Direction="asc";
Check(Filter(new()).Select(r=>r.RowCount).SequenceEqual(new[]{6,12,20}),"numeric sorting uses values rather than string order");
options.List.Fields.Single(f=>f.FieldName=="ImportedTime").FilterType="Date";
Check(Filter(new(){Date=new(){["ImportedTime"]="2026-10-20"}}).Single().StoredFileName=="2.xlsx","Date filter also supported by config");
Check(File.ReadAllText("ICP/Config/forwarder-upload-history-table-fields.json")==File.ReadAllText("ICP/Config/forwarder-upload-history-table-fields.example.json"),"example and active config match");
Console.WriteLine($"{checks} checks passed; no database or file changes.");

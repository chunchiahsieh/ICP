using ICP.Models.Icp;
using ICP.Services;

var count = 0;
void Check(bool condition, string name)
{
    if (!condition) throw new InvalidOperationException(name);
    count++;
}
Check(ArurComparisonService.Clean(null) == "", "null equals empty");
Check(ArurComparisonService.Clean(" a\r\nb ") == "a\nb", "trim and line endings");
Check(ArurComparisonService.DateEqual("2026/09/08", "2026-09-08", true), "date format equivalence");
Check(!ArurComparisonService.DateEqual("2026-09-08", "2026-09-09", true), "date mismatch");
Check(!ArurComparisonService.DateEqual("2026-09-08 10:00", "2026-09-08 11:00", false), "arrival time preserved");
Check(!ArurComparisonService.DateEqual("bad date", "", false), "invalid date not silently blanked");
Check(ArurComparisonService.IsY(" y ") && !ArurComparisonService.IsY("yes"), "writer Y conversion");
Check(ArurComparisonService.BuildRemark(new IcpHeader { Notes = " note ", Forklift = "Y", WasteDisposal = "Y", DriverDetails = "Y", MovingLabor = "2" })
    == "請安排堆高機\n(2)\n請處理廢棄物\n請回報司機資訊\nnote", "writer remark order");
Check(ArurComparisonService.AttachmentsEqual(["ATTACHED_FILE/a.pdf", "ATTACHED_FILE/b.pdf"], @"D:\ILC\ATTACHED_FILE\b.pdf,D:\ILC\ATTACHED_FILE\a.pdf"), "attachment roots and order");
Check(!ArurComparisonService.AttachmentsEqual(["ATTACHED_FILE/a.pdf"], @"D:\ILC\other\a.pdf"), "attachment owner path mismatch");
Check(!ArurComparisonService.AttachmentsEqual([], "extra.pdf"), "extra attachment");
Check(!ArurComparisonService.AttachmentsEqual(["a.pdf"], ""), "missing attachment");
Check(ArurComparisonService.AttachmentsEqual([], ""), "empty attachments");
Check(ArurComparisonService.Fields.Distinct().Count() == 15, "15 unique comparison fields");
var map = typeof(ArurComparisonService).GetMethod("Map", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
var mapped = (IEnumerable<(string Field, string Column, string? Value)>)map.Invoke(null,
    new object[] { new IcpHeader { DeliveryTo = "KEY" }, "address", Array.Empty<string>(), "VALUE1" })!;
Check(mapped.Single(x => x.Column == "ShipToCode").Value == "VALUE1", "comparison expects Value1 instead of Key1");
Console.WriteLine($"Passed {count} ARUR comparison checks.");

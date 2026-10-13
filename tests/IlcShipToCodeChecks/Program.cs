using TEL.IntegrationHub.Services;
if (IlcArurWriteService.ResolveShipToCode(" VALUE1 ", "KEY") != "VALUE1") throw new Exception("Value1 mapping failed");
foreach (var value in new string?[] { null, "", "   " })
{
    bool rejected = false;
    try { IlcArurWriteService.ResolveShipToCode(value, "KEY"); }
    catch (InvalidOperationException ex) { rejected = ex.Message.Contains("Value1") && ex.Message.Contains("KEY"); }
    if (!rejected) throw new Exception("Missing Value1 must be rejected without Key1 fallback");
}
Console.WriteLine("4 ILC ShipToCode checks passed; no database writes.");

var remark = typeof(IlcArurWriteService).GetMethod("Remark", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
var actual = (string)remark.Invoke(null, new object[] { " Note ", true, " 啟德全套 ", true, true })!;
if (actual != string.Join(Environment.NewLine, new[] { "請安排堆高機", "(啟德全套)", "請處理廢棄物", "請回報司機資訊", "Note" })) throw new Exception("Remark ordering failed");
if ((string)remark.Invoke(null, new object[] { "", false, "", false, false })! != "") throw new Exception("Empty remark should remain empty");
Console.WriteLine("Remark order and empty-field checks passed.");

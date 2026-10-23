using ClosedXML.Excel;
using ICP.Models;
using ICP.Models.Icp;
using System.ComponentModel.DataAnnotations;

namespace ICP.Helpers;

public static class SettingsExcelHelper
{
    public static readonly string[] Columns = ["Category", "FunctionCode", "Key1", "Key2", "Value1", "Value2", "Value3", "Value4", "Value5", "Value6"];

    public static byte[] Export(IEnumerable<SystemConfig> rows)
    {
        using var book = new XLWorkbook();
        var sheet = book.AddWorksheet("Settings");
        for (var c = 0; c < Columns.Length; c++) sheet.Cell(1, c + 1).Value = Columns[c];
        var r = 2;
        foreach (var row in rows)
        {
            for (var c = 0; c < Columns.Length; c++)
            {
                sheet.Cell(r, c + 1).Style.NumberFormat.Format = "@";
                sheet.Cell(r, c + 1).Value = (string?)typeof(SystemConfig).GetProperty(Columns[c])!.GetValue(row) ?? "";
            }
            r++;
        }
        sheet.Row(1).Style.Font.Bold = true;
        sheet.SheetView.FreezeRows(1);
        sheet.Columns().AdjustToContents(1, 50);
        using var stream = new MemoryStream();
        book.SaveAs(stream);
        return stream.ToArray();
    }

    public static List<(int RowNumber, SystemConfigEditModel Model)> Parse(Stream stream)
    {
        using var book = new XLWorkbook(stream);
        var sheet = book.Worksheet(1);
        if (sheet.LastRowUsed()?.RowNumber() > 50001) throw new InvalidOperationException("最多可上傳 50000 筆資料。");
        if (sheet.Row(1).LastCellUsed()?.Address.ColumnNumber != Columns.Length
            || !Columns.Select((name, index) => sheet.Cell(1, index + 1).GetString().Trim() == name).All(x => x))
            throw new InvalidOperationException("Excel 欄位或順序不符，請先下載此 Settings 頁面的 Excel 作為範本。");
        var result = new List<(int, SystemConfigEditModel)>();
        var errors = new List<string>();
        foreach (var row in sheet.RowsUsed().Skip(1))
        {
            if (row.LastCellUsed()?.Address.ColumnNumber > Columns.Length)
                errors.Add($"第 {row.RowNumber()} 列有超出範本欄位的資料。");
            if (row.Cells(1, Columns.Length).All(cell => cell.IsEmpty())) continue;
            var model = new SystemConfigEditModel();
            for (var c = 0; c < Columns.Length; c++)
            {
                var cell = row.Cell(c + 1);
                if (cell.HasFormula) errors.Add($"第 {row.RowNumber()} 列 {Columns[c]} 不可使用公式，請貼上純值。");
                typeof(SystemConfigEditModel).GetProperty(Columns[c])!.SetValue(model, cell.GetFormattedString().Trim());
            }
            var validation = new List<ValidationResult>();
            Validator.TryValidateObject(model, new ValidationContext(model), validation, true);
            errors.AddRange(validation.Select(error => $"第 {row.RowNumber()} 列：{error.ErrorMessage}"));
            result.Add((row.RowNumber(), model));
        }
        if (result.Count == 0) errors.Add("Excel 沒有資料，原設定不會被清空。");
        if (errors.Count > 0) throw new InvalidOperationException(string.Join(Environment.NewLine, errors.Take(30)));
        return result;
    }

    public static List<string> DuplicateErrors(IEnumerable<(int RowNumber, SystemConfig Entity)> rows) => rows
        .GroupBy(row => (row.Entity.Category.ToUpperInvariant(), (row.Entity.FunctionCode ?? "").ToUpperInvariant(),
            row.Entity.Key1.ToUpperInvariant(), row.Entity.Key2.ToUpperInvariant()))
        .Where(group => group.Count() > 1)
        .Select(group => $"重複鍵（Category / FunctionCode / Key1 / Key2），Excel 列：{string.Join(", ", group.Select(row => row.RowNumber))}")
        .ToList();
}

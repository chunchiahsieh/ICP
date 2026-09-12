using System.Xml.Linq;
using ClosedXML.Excel;
using ICP.Models.LocalizationManagement;

namespace ICP.Services;

/// <summary>
/// Reads and updates only the existing SharedResource keys used by ICP.
/// Resource files are backed up before every write so a change can be restored.
/// </summary>
public sealed class LocalizationResourceManagementService
{
    private static readonly string[] ExcelHeaders = ["Resource Key", "Default", "Traditional Chinese", "English", "Japanese"];
    private static readonly SemaphoreSlim WriteLock = new(1, 1);

    private static readonly IReadOnlyDictionary<string, string> ResourceFiles =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Default"] = "SharedResource.resx",
            ["ZhTw"] = "SharedResource.zh-TW.resx",
            ["En"] = "SharedResource.en.resx",
            ["Ja"] = "SharedResource.ja.resx"
        };

    private readonly string _resourcesPath;
    private readonly string _backupPath;

    public LocalizationResourceManagementService(IWebHostEnvironment environment)
    {
        _resourcesPath = Path.Combine(environment.ContentRootPath, "Resources");
        _backupPath = Path.Combine(environment.ContentRootPath, "LocalizationBackups");
    }

    public async Task<IReadOnlyList<LocalizationResourceRow>> GetRowsAsync(CancellationToken cancellationToken = default)
    {
        await WriteLock.WaitAsync(cancellationToken);
        try
        {
            var documents = LoadDocuments();
            var defaultValues = GetValues(documents["Default"]);
            var zhTwValues = GetValues(documents["ZhTw"]);
            var enValues = GetValues(documents["En"]);
            var jaValues = GetValues(documents["Ja"]);

            return defaultValues.Keys
                .OrderBy(key => key, StringComparer.Ordinal)
                .Select(key => new LocalizationResourceRow
                {
                    Key = key,
                    Default = defaultValues[key],
                    ZhTw = zhTwValues.GetValueOrDefault(key, string.Empty),
                    En = enValues.GetValueOrDefault(key, string.Empty),
                    Ja = jaValues.GetValueOrDefault(key, string.Empty)
                })
                .ToList();
        }
        finally
        {
            WriteLock.Release();
        }
    }

    public async Task<int> SaveAsync(IReadOnlyCollection<LocalizationResourceRow> rows, CancellationToken cancellationToken = default)
    {
        if (rows.Count == 0)
        {
            return 0;
        }

        await WriteLock.WaitAsync(cancellationToken);
        try
        {
            var documents = LoadDocuments();
            var canonicalKeys = GetValues(documents["Default"]).Keys.ToHashSet(StringComparer.Ordinal);
            var duplicateKey = rows.GroupBy(row => row.Key, StringComparer.Ordinal).FirstOrDefault(group => group.Count() > 1)?.Key;

            if (!string.IsNullOrWhiteSpace(duplicateKey))
            {
                throw new InvalidOperationException($"Duplicate resource key: {duplicateKey}");
            }

            var unknownKey = rows.Select(row => row.Key).FirstOrDefault(key => string.IsNullOrWhiteSpace(key) || !canonicalKeys.Contains(key));
            if (!string.IsNullOrWhiteSpace(unknownKey))
            {
                throw new InvalidOperationException($"Unknown resource key: {unknownKey}");
            }

            var changedCount = ApplyChanges(documents, rows);
            if (changedCount == 0)
            {
                return 0;
            }

            var backupFolder = CreateBackup();
            try
            {
                SaveDocuments(documents);
            }
            catch
            {
                RestoreBackup(backupFolder);
                throw;
            }

            return changedCount;
        }
        finally
        {
            WriteLock.Release();
        }
    }

    public async Task<byte[]> ExportExcelAsync(CancellationToken cancellationToken = default)
    {
        var rows = await GetRowsAsync(cancellationToken);
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("Translations");
        for (var index = 0; index < ExcelHeaders.Length; index++)
        {
            worksheet.Cell(1, index + 1).Value = ExcelHeaders[index];
        }

        worksheet.Row(1).Style.Font.Bold = true;
        worksheet.Row(1).Style.Fill.BackgroundColor = XLColor.LightSteelBlue;
        worksheet.SheetView.FreezeRows(1);
        worksheet.Columns(1, 5).Style.Alignment.WrapText = true;
        worksheet.Column(1).Width = 48;
        worksheet.Columns(2, 5).Width = 42;

        var rowIndex = 2;
        foreach (var row in rows)
        {
            worksheet.Cell(rowIndex, 1).Value = row.Key;
            worksheet.Cell(rowIndex, 2).Value = row.Default;
            worksheet.Cell(rowIndex, 3).Value = row.ZhTw;
            worksheet.Cell(rowIndex, 4).Value = row.En;
            worksheet.Cell(rowIndex, 5).Value = row.Ja;
            rowIndex++;
        }

        worksheet.Range(1, 1, Math.Max(rowIndex - 1, 1), ExcelHeaders.Length).CreateTable();
        await using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    public async Task<LocalizationExcelImportResult> ImportExcelAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        var rows = ReadExcelRows(stream);
        var changedCount = await SaveAsync(rows, cancellationToken);
        return new LocalizationExcelImportResult(rows.Count, changedCount);
    }

    private static List<LocalizationResourceRow> ReadExcelRows(Stream stream)
    {
        try
        {
            using var workbook = new XLWorkbook(stream);
            var worksheet = workbook.Worksheet("Translations");
            for (var index = 0; index < ExcelHeaders.Length; index++)
            {
                var actual = worksheet.Cell(1, index + 1).GetString().Trim();
                if (!string.Equals(actual, ExcelHeaders[index], StringComparison.Ordinal))
                {
                    throw new InvalidDataException("The Excel file does not use the ICP localization export format.");
                }
            }

            var rows = new List<LocalizationResourceRow>();
            foreach (var excelRow in worksheet.RowsUsed().Skip(1))
            {
                var key = excelRow.Cell(1).GetString().Trim();
                if (string.IsNullOrEmpty(key) && excelRow.Cells(1, ExcelHeaders.Length).All(cell => string.IsNullOrWhiteSpace(cell.GetString())))
                {
                    continue;
                }

                rows.Add(new LocalizationResourceRow
                {
                    Key = key,
                    Default = excelRow.Cell(2).GetString(),
                    ZhTw = excelRow.Cell(3).GetString(),
                    En = excelRow.Cell(4).GetString(),
                    Ja = excelRow.Cell(5).GetString()
                });
            }

            if (rows.Count == 0)
            {
                throw new InvalidDataException("The Excel file does not contain any translation rows.");
            }

            return rows;
        }
        catch (InvalidDataException)
        {
            throw;
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or InvalidOperationException)
        {
            throw new InvalidDataException("The selected file is not a valid ICP localization Excel file.", ex);
        }
    }

    private Dictionary<string, XDocument> LoadDocuments()
    {
        var documents = new Dictionary<string, XDocument>(StringComparer.Ordinal);
        foreach (var (language, fileName) in ResourceFiles)
        {
            var path = Path.Combine(_resourcesPath, fileName);
            if (!File.Exists(path))
            {
                throw new FileNotFoundException("Localization resource file was not found.", path);
            }

            documents[language] = XDocument.Load(path, System.Xml.Linq.LoadOptions.PreserveWhitespace);
        }

        return documents;
    }

    private static Dictionary<string, string> GetValues(XDocument document)
    {
        return document.Root?
            .Elements("data")
            .Select(element => new
            {
                Key = (string?)element.Attribute("name"),
                Value = element.Element("value")?.Value ?? string.Empty
            })
            .Where(item => !string.IsNullOrWhiteSpace(item.Key))
            .ToDictionary(item => item.Key!, item => item.Value, StringComparer.Ordinal)
            ?? new Dictionary<string, string>(StringComparer.Ordinal);
    }

    private static int ApplyChanges(
        IReadOnlyDictionary<string, XDocument> documents,
        IReadOnlyCollection<LocalizationResourceRow> rows)
    {
        var changedCount = 0;
        foreach (var row in rows)
        {
            changedCount += SetValue(documents["Default"], row.Key, row.Default);
            changedCount += SetValue(documents["ZhTw"], row.Key, row.ZhTw);
            changedCount += SetValue(documents["En"], row.Key, row.En);
            changedCount += SetValue(documents["Ja"], row.Key, row.Ja);
        }

        return changedCount;
    }

    private static int SetValue(XDocument document, string key, string? value)
    {
        var data = document.Root?
            .Elements("data")
            .FirstOrDefault(element => string.Equals((string?)element.Attribute("name"), key, StringComparison.Ordinal));

        if (data is null)
        {
            document.Root?.Add(new XElement("data",
                new XAttribute("name", key),
                new XAttribute(XNamespace.Xml + "space", "preserve"),
                new XElement("value", value ?? string.Empty)));
            return 1;
        }

        var valueElement = data.Element("value");
        if (valueElement is null)
        {
            data.Add(new XElement("value", value ?? string.Empty));
            return 1;
        }

        var normalizedValue = value ?? string.Empty;
        if (string.Equals(valueElement.Value, normalizedValue, StringComparison.Ordinal))
        {
            return 0;
        }

        valueElement.Value = normalizedValue;
        return 1;
    }

    private string CreateBackup()
    {
        var backupFolder = Path.Combine(_backupPath, DateTime.Now.ToString("yyyyMMddHHmmssfff"));
        Directory.CreateDirectory(backupFolder);

        foreach (var fileName in ResourceFiles.Values)
        {
            File.Copy(Path.Combine(_resourcesPath, fileName), Path.Combine(backupFolder, fileName), overwrite: false);
        }

        return backupFolder;
    }

    private void SaveDocuments(IReadOnlyDictionary<string, XDocument> documents)
    {
        foreach (var (language, fileName) in ResourceFiles)
        {
            var destination = Path.Combine(_resourcesPath, fileName);
            var temporary = destination + ".tmp";
            documents[language].Save(temporary, System.Xml.Linq.SaveOptions.DisableFormatting);
            File.Move(temporary, destination, overwrite: true);
        }
    }

    private void RestoreBackup(string backupFolder)
    {
        foreach (var fileName in ResourceFiles.Values)
        {
            var backupFile = Path.Combine(backupFolder, fileName);
            if (File.Exists(backupFile))
            {
                File.Copy(backupFile, Path.Combine(_resourcesPath, fileName), overwrite: true);
            }
        }
    }
}

public sealed record LocalizationExcelImportResult(int ImportedCount, int ChangedCount);

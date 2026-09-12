import fs from "node:fs/promises";
import { SpreadsheetFile, Workbook } from "@oai/artifact-tool";

const projectRoot = "C:/Disk F/Aga/EY/TEL/ICP/Projects";
const resourcesDir = `${projectRoot}/ICP/Resources`;
const outputPath = `${projectRoot}/outputs/ICP-Resources.xlsx`;

const resourceFiles = [
  { column: "Default", file: "SharedResource.resx" },
  { column: "zh-TW", file: "SharedResource.zh-TW.resx" },
  { column: "ja", file: "SharedResource.ja.resx" },
  { column: "en", file: "SharedResource.en.resx" },
];

function decodeXml(value) {
  return value
    .replace(/<!\[CDATA\[([\s\S]*?)\]\]>/g, "$1")
    .replace(/&#(x[0-9a-fA-F]+|\d+);/g, (_, code) => String.fromCodePoint(code[0].toLowerCase() === "x" ? parseInt(code.slice(1), 16) : parseInt(code, 10)))
    .replace(/&quot;/g, '"')
    .replace(/&apos;/g, "'")
    .replace(/&lt;/g, "<")
    .replace(/&gt;/g, ">")
    .replace(/&amp;/g, "&");
}

function parseResx(xml) {
  const records = new Map();
  const matcher = /<data\s+[^>]*\bname="([^"]+)"[^>]*>([\s\S]*?)<\/data>/g;
  let match;
  while ((match = matcher.exec(xml)) !== null) {
    const valueMatch = /<value>([\s\S]*?)<\/value>/.exec(match[2]);
    records.set(decodeXml(match[1]), valueMatch ? decodeXml(valueMatch[1]).replace(/\r?\n/g, "\n") : "");
  }
  return records;
}

const sources = await Promise.all(resourceFiles.map(async ({ column, file }) => ({
  column,
  values: parseResx(await fs.readFile(`${resourcesDir}/${file}`, "utf8")),
})));
const keys = [...new Set(sources.flatMap(source => [...source.values.keys()]))].sort((a, b) => a.localeCompare(b));
const rows = keys.map(key => {
  const values = sources.map(source => source.values.get(key) ?? "");
  const missing = sources.filter((source, index) => !source.values.has(key) || !values[index].trim()).map(source => source.column).join(", ");
  return [key, ...values, missing];
});

const workbook = Workbook.create();
const sheet = workbook.worksheets.add("Resources");
sheet.showGridLines = false;
sheet.tabColor = "#1F4E78";

sheet.getRange("A1:F1").merge();
sheet.getRange("A1").values = [["ICP Resources"]];
sheet.getRange("A1").format = { font: { name: "Arial", size: 16, bold: true, color: "#1F1F1F" }, verticalAlignment: "center" };
sheet.getRange("A1:F1").format.rowHeight = 26;
sheet.getRange("A2:F2").merge();
sheet.getRange("A2").values = [[`來源：SharedResource.resx、zh-TW、ja、en；共 ${keys.length} 個資源鍵`]];
sheet.getRange("A2").format = { font: { name: "Arial", size: 10, italic: true, color: "#666666" } };

const headers = ["Resource key", ...resourceFiles.map(item => item.column), "Missing locales"];
sheet.getRange("A4:F4").values = [headers];
sheet.getRange("A5").write(rows);
const lastRow = rows.length + 4;
const table = sheet.tables.add(`A4:F${lastRow}`, true, "ResourcesTable");
table.style = "TableStyleMedium2";
sheet.freezePanes.freezeRows(4);
sheet.getRange(`A5:F${lastRow}`).format = { font: { name: "Arial", size: 10 }, verticalAlignment: "top", wrapText: true };
sheet.getRange(`F5:F${lastRow}`).conditionalFormats.add("notContainsBlanks", { format: { fill: "#FCE4D6", font: { color: "#C00000" } } });

sheet.getRange("A:A").format.columnWidth = 42;
sheet.getRange("B:E").format.columnWidth = 38;
sheet.getRange("F:F").format.columnWidth = 20;
sheet.getRange(`A5:F${lastRow}`).format.autofitRows();

const missingCount = rows.filter(row => row[5]).length;
sheet.getRange("H1:I1").values = [["Summary", "Count"]];
sheet.getRange("H2:I4").values = [
  ["Resource keys", keys.length],
  ["Complete keys", keys.length - missingCount],
  ["Keys with missing values", missingCount],
];
sheet.getRange("H1:I1").format = { fill: "#1F4E78", font: { name: "Arial", size: 10, bold: true, color: "#FFFFFF" } };
sheet.getRange("H1:I4").format.borders = { preset: "all", style: "thin", color: "#D9E2F3" };
sheet.getRange("H1:I4").format.font = { name: "Arial", size: 10 };
sheet.getRange("H:H").format.columnWidth = 24;
sheet.getRange("I:I").format.columnWidth = 12;

const preview = await workbook.render({ sheetName: "Resources", range: "A1:I20", scale: 1.5, format: "png" });
await fs.writeFile(`${projectRoot}/outputs/ICP-Resources-preview.png`, new Uint8Array(await preview.arrayBuffer()));
const inspected = await workbook.inspect({ kind: "table", range: "Resources!A1:I12", include: "values,formulas", tableMaxRows: 12, tableMaxCols: 9 });
console.log(inspected.ndjson);
const errors = await workbook.inspect({ kind: "match", searchTerm: "#REF!|#DIV/0!|#VALUE!|#NAME\\?|#N/A|#NUM!|#NULL!|#SPILL!|#CALC!", options: { useRegex: true, maxResults: 50 }, summary: "formula error scan" });
console.log(errors.ndjson);

await fs.mkdir(`${projectRoot}/outputs`, { recursive: true });
const output = await SpreadsheetFile.exportXlsx(workbook);
await output.save(outputPath);
console.log(`Saved ${outputPath}`);

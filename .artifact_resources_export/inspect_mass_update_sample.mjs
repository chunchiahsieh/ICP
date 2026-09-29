import fs from "node:fs/promises";
import { FileBlob, SpreadsheetFile } from "@oai/artifact-tool";

const sourcePath = "C:/Disk F/Aga/EY/TEL/ICP/Projects/ICP/Files/Mass Update Sample File.xlsx";
const previewPath = "C:/Disk F/Aga/EY/TEL/ICP/Projects/.artifact_resources_export/mass-update-before.png";
const input = await FileBlob.load(sourcePath);
const workbook = await SpreadsheetFile.importXlsx(input);
const summary = await workbook.inspect({
  kind: "workbook,sheet,table",
  maxChars: 8000,
  tableMaxRows: 8,
  tableMaxCols: 24,
  tableMaxCellChars: 100,
});
process.stdout.write(summary.ndjson + "\n");
const sheet = workbook.worksheets.getItemAt(0);
const preview = await workbook.render({ sheetName: sheet.name, autoCrop: "all", scale: 1, format: "png" });
await fs.writeFile(previewPath, new Uint8Array(await preview.arrayBuffer()));
process.stdout.write(JSON.stringify({ sheetName: sheet.name, previewPath }) + "\n");

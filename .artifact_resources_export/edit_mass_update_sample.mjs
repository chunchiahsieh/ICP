import fs from "node:fs/promises";
import { execFileSync } from "node:child_process";
import { FileBlob, SpreadsheetFile } from "@oai/artifact-tool";

const sourcePath = "C:/Disk F/Aga/EY/TEL/ICP/Projects/ICP/Files/Mass Update Sample File.xlsx";
const outputPath = "C:/Disk F/Aga/EY/TEL/ICP/Projects/outputs/mass-update-sample/Mass Update Sample File.xlsx";
const previewPath = "C:/Disk F/Aga/EY/TEL/ICP/Projects/.artifact_resources_export/mass-update-after.png";

const gitPath = "C:/Users/User/.cache/codex-runtimes/codex-primary-runtime/dependencies/native/git/cmd/git.exe";
const originalBytes = execFileSync(gitPath, ["show", "HEAD:ICP/Files/Mass Update Sample File.xlsx"], {
  cwd: "C:/Disk F/Aga/EY/TEL/ICP/Projects",
  maxBuffer: 20 * 1024 * 1024,
});
const workbook = await SpreadsheetFile.importXlsx(new Uint8Array(originalBytes));
const sheet = workbook.worksheets.getItemAt(0);
const used = sheet.getUsedRange();
const originalValues = used.values;
const rowCount = used.values.length;
const sourceColumns = ["P", "O", "N", "M", "L", "K", "J", "I"];
const targetColumns = ["Q", "P", "O", "N", "M", "L", "K", "J"];
for (let index = 0; index < sourceColumns.length; index++) {
  sheet.getRange(`${sourceColumns[index]}1:${sourceColumns[index]}${rowCount}`)
    .copyTo(sheet.getRange(`${targetColumns[index]}1:${targetColumns[index]}${rowCount}`), "all");
}
sheet.getRange(`H1:H${rowCount}`).copyTo(sheet.getRange(`I1:I${rowCount}`), "all");
const updatedValues = originalValues.map((row, rowIndex) => [
  ...row.slice(0, 8),
  rowIndex === 0 ? "SHIPPER" : "TEL-JP",
  ...row.slice(8),
]);
sheet.getRange(`A1:Q${rowCount}`).values = updatedValues;
sheet.getRange(`J2:J${rowCount}`).format.numberFormat = "yyyy-mm-dd";
sheet.getRange(`N2:N${rowCount}`).format.numberFormat = "yyyy-mm-dd";
sheet.getRange(`Q2:Q${rowCount}`).format.numberFormat = "yyyy-mm-dd";

workbook.recalculate();
const check = await workbook.inspect({
  kind: "table",
  sheetId: sheet.name,
  range: `A1:Q${rowCount}`,
  include: "values,formulas",
  tableMaxRows: 12,
  tableMaxCols: 20,
  maxChars: 12000,
});
process.stdout.write(check.ndjson + "\n");
const errors = await workbook.inspect({
  kind: "match",
  searchTerm: "#REF!|#DIV/0!|#VALUE!|#NAME\\?|#N/A|#NUM!|#NULL!|#SPILL!|#CALC!",
  options: { useRegex: true, maxResults: 100 },
  summary: "final formula error scan",
});
process.stdout.write(errors.ndjson + "\n");
const preview = await workbook.render({ sheetName: sheet.name, range: `A1:Q${rowCount}`, scale: 1, format: "png" });
await fs.writeFile(previewPath, new Uint8Array(await preview.arrayBuffer()));
await fs.mkdir("C:/Disk F/Aga/EY/TEL/ICP/Projects/outputs/mass-update-sample", { recursive: true });
const output = await SpreadsheetFile.exportXlsx(workbook);
await output.save(outputPath);
await fs.copyFile(outputPath, sourcePath);
process.stdout.write(JSON.stringify({ outputPath, sourcePath, previewPath }) + "\n");

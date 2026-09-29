import { execFileSync } from "node:child_process";
import { SpreadsheetFile } from "@oai/artifact-tool";
const bytes = execFileSync("C:/Users/User/.cache/codex-runtimes/codex-primary-runtime/dependencies/native/git/cmd/git.exe", ["show", "HEAD:ICP/Files/Mass Update Sample File.xlsx"], { cwd: "C:/Disk F/Aga/EY/TEL/ICP/Projects", maxBuffer: 20 * 1024 * 1024 });
const workbook = await SpreadsheetFile.importXlsx(new Uint8Array(bytes));
const table = workbook.worksheets.getItemAt(0).tables.items[0];
process.stdout.write(JSON.stringify({ name: table.name, style: table.style, showBandedColumns: table.showBandedColumns, showTotals: table.showTotals, showFilterButton: table.showFilterButton }) + "\n");

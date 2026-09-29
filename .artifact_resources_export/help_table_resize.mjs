import { Workbook } from "@oai/artifact-tool";
const workbook = Workbook.create();
process.stdout.write(workbook.help("table.resize", { include: "index,examples,notes", maxChars: 4000 }).ndjson + "\n");

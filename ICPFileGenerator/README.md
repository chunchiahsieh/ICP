# ICPFileGenerator

Phase 1：ASP.NET Core Web API + Background Worker。模擬 TXT 產檔、查詢 Job、Hub 通知 Stub。不實作 PDF／Excel／RabbitMQ／真 Hub。

## 系統定位

| 系統 | 職責（完整願景） |
|------|------------------|
| ICP | Export Request；通知 Hub（不直連本 DB） |
| Integration Hub | 跨系統協調；建 Job；回寫 ExportRequest |
| ICPFileGenerator | 本專案：掃 Job、產檔、通知 Hub；提供查詢 API |

本階段僅 ICPFileGenerator 自管 Job；Job 需手動／SQL 插入後由 Worker Claim。

## Solution

掛在根目錄 [`ICP.sln`](../ICP.sln)，專案：`ICPFileGenerator/ICPFileGenerator.csproj`（net8.0）。

## Folder Structure

```text
ICPFileGenerator/
├── Controllers/          System + FileGenerationJobs（唯讀）
├── Workers/              FileGenerationWorker
├── Services/             FileGeneration + HubNotification Stub
├── Repositories/         JobRepository（ADO.NET）
├── Models/
├── Infrastructure/       Database / Logging
├── sql/                  建庫表 + Claim SP
├── Output/               TXT 輸出目錄（執行時建立）
├── Program.cs
└── appsettings*.json
```

## Build

```bash
dotnet restore ICPFileGenerator/ICPFileGenerator.csproj
dotnet build ICPFileGenerator/ICPFileGenerator.csproj
```

## Database / SQL Script

資料庫名稱：**`TEL-ICPFileGenerator`**

1. 執行 [`sql/001_CreateDatabase_And_JobTable.sql`](sql/001_CreateDatabase_And_JobTable.sql)
2. 執行 [`sql/002_ClaimNextFileGenerationJob.sql`](sql/002_ClaimNextFileGenerationJob.sql)

表：`ICPFileGeneratorJob`（`RequestId` UNIQUE）。Claim 使用 `UPDLOCK, READPAST, ROWLOCK`。

範例插入 Pending Job：見 [`sql/003_Sample_Pending_Job.sql`](sql/003_Sample_Pending_Job.sql)。

## appsettings（與 ICP 相同選擇邏輯）

| 環境 | 組態來源 |
|------|----------|
| AGA 電腦（`MachineName == AGA-PC`） | `appsettings.json`；Development 另加 `appsettings.Development.json` |
| TEL / 非 AGA | **僅** `appsettings.TEL.json`（見 `Program.cs`） |

連線字串鍵一律為 **`ConnectionStrings:ICPFileGenerator`**，資料庫為 `TEL-ICPFileGenerator`。

- `FileGenerator:PollingIntervalSeconds`（預設 10）
- `WorkerId`、`OutputDirectory`、`ProcessingTimeoutMinutes`、`MaxRetryCount`

範本：[`appsettings.example.json`](appsettings.example.json)。
## 執行方式

```bash
dotnet run --project ICPFileGenerator/ICPFileGenerator.csproj --launch-profile http
```

- Swagger：`http://localhost:5208/swagger`
- Worker 與 API 同進程啟動

## API（唯讀）

| Method | Path |
|--------|------|
| GET | `/api/system/status` |
| GET | `/api/file-generation-jobs` |
| GET | `/api/file-generation-jobs?status=Completed` |
| GET | `/api/file-generation-jobs/{id}` |
| GET | `/api/file-generation-jobs/request/{requestId}` |

## Export

見 [`docs/export.md`](docs/export.md)：ICP 上傳 → Hub → FileGen 產檔 → Hub 回寫 ICP。

# TEL Integration Hub

**定位**：GEM／ARUR／ICP 的中央整合中介平台（衛星系統只 Publish；Hub 統一 Consume／Routing／Transform／寫 DB／Log）。  
完整定義見 [`docs/TEL_Integration_Hub_Definition.md`](docs/TEL_Integration_Hub_Definition.md)。

單一 ASP.NET Core Web API 專案（**Phase 1**），與 ICP 共用根目錄 [`ICP.sln`](../ICP.sln)。

## 功能

- 消費 ICP 整合事件，依 payload 分流三種業務並寫入 `MESSAGE_LOG`
- Hub 處理成功後，以 `messageId` 回寫 ICP `INTEGRATION_EVENT_OUTBOX` 為 `Completed`（需 `ConnectionStrings:ICP_Connection`）
- Swagger 查詢 API
- `Integration:RabbitMq:Enabled` 可開關（false 時不連 RabbitMQ，API 仍可用）

## 三種業務消費者（一事件一 Consumer；ICP 發送端暫不動）

程式碼在 [`Consumers/`](Consumers/)：

| 業務 | 辨識方式 | Consumer |
|------|----------|-----------|
| 押金起案 | Envelope `eventType=icp.shipinfo.case.initiated` 且 `payload.caseType=Deposit` | `DepositCaseInitiatedConsumer` |
| ARUR 起案 | 同上且 `payload.caseType=ARUR` | `ArurCaseInitiatedConsumer` |
| 匯出檔案 | Envelope `eventType=icp.export.completed`（預留；Function/Export） | `ExportFileCompletedConsumer` |

事件契約為標準 **Event Envelope**（`messageId`／`sourceSystem`／`payload`…），見 [`docs/TEL_Integration_Hub_Definition.md`](docs/TEL_Integration_Hub_Definition.md) 與 `../ICP/docs/integration-events.md`。

- Queue：`tel.integration.hub.queue`（起案）、`tel.integration.hub.queue.export`（匯出預留）
- Exchange：`tel.integration`（topic）
- MessageLog.`TargetSystem` 會標 `Deposit` / `ARUR` / `Export` 方便查詢
- Export 契約見 `Models/ExportFileCompletedMessage.cs`；ICP Export 頁尚未發 Outbox，暫無實訊可測屬預期

## 組態檔（與 ICP 相同選擇邏輯）

| 環境 | 組態來源 |
|------|----------|
| AGA 電腦（`MachineName == AGA-PC`） | `appsettings.json`；Development 另加 `appsettings.Development.json` |
| TEL / 非 AGA | **僅** `appsettings.TEL.json`（見 `Program.cs`） |

範本：[`appsettings.example.json`](appsettings.example.json)。含密碼的檔案勿 commit。

## 指令

```powershell
dotnet build ICP.sln
dotnet run --project TELIntegrationHub\TEL.IntegrationHub.csproj
```

Swagger：`http://localhost:5261/swagger`  
Health：`GET /health`

## API

| Method | Path | 說明 |
|--------|------|------|
| GET | `/api/messages` | 查詢（可用 `targetSystem=Deposit` 等過濾） |
| GET | `/api/messages/{messageId}` | 單筆 |
| GET | `/api/messages/errors` | Failed / DeadLetter |
| POST | `/api/messages/{messageId}/retry` | 501（Phase 1 未實作） |

ICP 契約見 `../ICP/docs/integration-events.md`。  
Hub 願景與 Phase 對照見 [`docs/TEL_Integration_Hub_Definition.md`](docs/TEL_Integration_Hub_Definition.md)。

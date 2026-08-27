# ShipInfo 整合事件（Outbox + RabbitMQ）

ICP 在押金或 ARUR 起案成功後，會將**標準 Event Envelope**（含業務 `payload`）寫入 `INTEGRATION_EVENT_OUTBOX`，再由背景服務發送至 RabbitMQ，供 TEL Integration Hub 消費。

## 流程概覽

1. 使用者於 Ship Info 完成押金或 ARUR 起案。
2. `ShipInfoService.CreateCaseAsync` 於**同一資料庫交易**內更新表頭／明細、寫入 audit log、寫入 Outbox。
3. `IntegrationEventOutboxPublisherWorker` 定時撈取 `Pending` 事件。
4. 若 `Integration:RabbitMq:Enabled` 為 `true`，透過 RabbitMQ 發布 JSON；否則事件保留在 Outbox 中等待日後啟用。

## 設定（appsettings）

```json
"Integration": {
  "RabbitMq": {
    "Enabled": false,
    "HostName": "localhost",
    "Port": 5672,
    "VirtualHost": "/",
    "UserName": "",
    "Password": "",
    "Exchange": "tel.integration",
    "RoutingKey": "icp.shipinfo.case.initiated"
  },
  "Outbox": {
    "PollIntervalSeconds": 10,
    "MaxRetryCount": 5,
    "BatchSize": 20
  }
}
```

## 標準 Event Envelope

| 欄位 | 說明 |
|------|------|
| `messageId` | GUID，冪等鍵，同 Outbox `Id` |
| `eventType` | 固定 `icp.shipinfo.case.initiated` |
| `sourceSystem` | `ICP` |
| `targetSystems` | 預設 `["GEM","ARUR"]` |
| `occurredAt` | UTC 時間 |
| `correlationId` | 發票鍵（`InvoiceNo`） |
| `version` | `1.0` |
| `payload` | 業務資料（見下） |

### payload（起案）

| 欄位 | 說明 |
|------|------|
| `caseType` | `Deposit` 或 `ARUR` |
| `caseNo` | 押金單號或 RT 單號 |
| `headerKey` | 表頭列鍵 |
| `oldStatus` / `newStatus` | Ship Info 狀態 |
| `actor.userName` | 操作者 |
| `snapshot` | 起案當下 header、details 與 summary |

### JSON 範例（精簡）

```json
{
  "messageId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "eventType": "icp.shipinfo.case.initiated",
  "sourceSystem": "ICP",
  "targetSystems": ["GEM", "ARUR"],
  "occurredAt": "2026-06-06T12:00:00Z",
  "correlationId": "INV-001",
  "version": "1.0",
  "payload": {
    "caseType": "ARUR",
    "caseNo": "ARUR-INV-001-PO1",
    "headerKey": "INV-001\u001fPO1",
    "oldStatus": "Processing",
    "newStatus": "WarehouseReceived",
    "actor": { "userName": "user01" },
    "snapshot": {
      "header": { "invoiceNo": "INV-001", "rtNo": "ARUR-..." },
      "details": [{ "itemNo": "10", "qty": 1 }],
      "headerSummary": { "invoiceNo": "INV-001", "status": "..." },
      "detailSummary": { "detailCount": 1, "totalQty": 1 }
    }
  }
}
```

完整 Envelope 亦存於 `INTEGRATION_EVENT_OUTBOX.PayloadJson`。

## Outbox 狀態

| 狀態 | 說明 |
|------|------|
| `Pending` | 待發送或可重試 |
| `Published` | 已成功發送至 RabbitMQ |
| `Completed` | Hub 已成功消費並寫入 MessageLog（Hub 直接更新 ICP Outbox） |
| `Failed` | 超過 `MaxRetryCount` 仍失敗（發送端）；ARUR 由 Hub 寫入 ILC 失敗時也會標為 `Failed` |

流程：`Pending` → `Published` → `Completed`。押金 Hub 處理失敗時 Outbox 維持 `Published`，僅記 Hub `MESSAGE_LOG` Failed。ARUR Hub 處理失敗時 Outbox 改為 `Failed`，不自動重試；**若連 Outbox 都標不成 Failed，訊息會留在 Queue 直到標成功**。

### Failed 重送（ShipInfo）

當業務 case 已是 `Initiated`，但 Outbox 為 `Failed`（發送耗盡，或 ARUR 由 Hub 寫入 ILC 失敗）時：

1. ShipInfo 押金／ARUR 按鈕會依 `DepositOutboxFailed`／`ArurOutboxFailed` 再次啟用。
2. 再按同一按鈕會將該 `HeaderKey`+`CaseType` 最新 Failed 列重設為 `Pending`（`RetryCount=0`），**不重產 ICP 案號**、不改 case 狀態。ARUR 重送到 Hub 後會再取一個新的 ILC `PRT-yyyyMMdd-nnn`。
3. 既有 `IntegrationEventOutboxPublisherWorker` 會再次發布。

## Hub 消費（押金起案 → ILC）

`DepositCaseInitiatedConsumer`（`caseType=Deposit`）會：

1. 寫入 ILC `dbo.Deposit_Head`／`Deposit_Import`／`Deposit_Buyer`（同一交易；僅使用欄位）
2. MessageLog Success
3. 將 ICP Outbox 標為 `Completed`

連線：`ConnectionStrings:ILC_Connection`（例：`tetis87146` / `ILC`）。

同一 `InvNo` 若 Head 已存在則略過 INSERT（冪等），仍標 Completed。

## Hub 消費（ARUR 起案 → ILC）

`ArurCaseInitiatedConsumer`（`caseType=ARUR`）會：

1. 寫入 ILC `dbo.RT_ARUR_HEADER`（僅使用欄位；`CreateSys=ICP`、`Status=5`、`A1_Start=A8`、`ArrivalType=1`）
2. ILC `RT_NO` 由 Hub 呼叫 `dbo.SP_GEN_SEQNO`（`SYS_ID=ICP`、`GROUP_1=PRT`、`GROUP_2=yyyyMMdd`）組成 `PRT-yyyyMMdd-001`～`999`（與 ICP 畫面的 `ARUR-{發票號}` 不同，這是預期行為）。超過 999 會失敗並 rollback，因 `RT_NO` 為 nvarchar(16)
3. MessageLog Success
4. 將 ICP Outbox 標為 `Completed`

操作者：`actor.userName` 取 Windows 帳號最後一段，對 FIESTA `MailGroup.EmpID` 取得工號／信箱。

Hub 寫入失敗時將 Outbox 標為 `Failed` 並寫入 `LastError`，**不自動重試**。若連 Outbox 都標不成 Failed，訊息會留在 Queue 直到標成功。使用者於 ShipInfo 畫面重送時會再發布同一事件，Hub 會再取一個新的 PRT 號寫入 ILC。

## 相關程式

| 用途 | 路徑 |
|------|------|
| Envelope 基底 | `Models/Integration/IntegrationEventEnvelope.cs` |
| 起案事件 | `Models/Integration/ShipInfoCaseInitiatedEvent.cs` |
| 事件工廠 | `Services/Integration/ShipInfoCaseEventFactory.cs` |
| Outbox Repository | `Repositories/IntegrationEventOutboxRepository.cs` |
| 背景發布 | `Services/Integration/IntegrationEventOutboxPublisherWorker.cs` |

## 不在範圍

- Hub 回寫 GEM **業務表**（押金／ARUR 寫入 ILC 已實作）
- Export 頁 Outbox 發送（契約由 Hub 預留）

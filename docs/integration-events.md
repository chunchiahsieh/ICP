# ShipInfo 整合事件（Outbox + RabbitMQ）

ICP 在押金或 ARUR 起案成功後，會將含業務快照（Snapshot）的整合事件寫入 `INTEGRATION_EVENT_OUTBOX`，再由背景服務發送至 RabbitMQ，供未來的 TEL Integration Hub 消費。

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

| 設定 | 說明 |
|------|------|
| `RabbitMq:Enabled` | `false`（預設）時不發送，Outbox 仍累積；Hub 就緒後改為 `true` |
| `Exchange` / `RoutingKey` | 與 Hub 約定的交換器與路由鍵 |
| `Outbox:PollIntervalSeconds` | 背景輪詢間隔（秒） |
| `Outbox:MaxRetryCount` | 發送失敗最大重試次數，超過後狀態為 `Failed` |
| `Outbox:BatchSize` | 每次輪詢處理筆數上限 |

## 事件契約

| 欄位 | 說明 |
|------|------|
| `eventId` | GUID，冪等鍵，同 Outbox `Id` |
| `eventType` | 固定 `icp.shipinfo.case.initiated` |
| `eventVersion` | `1.0` |
| `occurredAt` | UTC 時間 |
| `correlationId` | 發票鍵（`InvoiceNo`） |
| `source` | `ICP` |
| `caseType` | `Deposit` 或 `ARUR` |
| `caseNo` | 押金單號或 RT 單號 |
| `headerKey` | 表頭列鍵（`InvoiceNo` + `TetPo`） |
| `oldStatus` / `newStatus` | Ship Info 狀態 |
| `actor.userName` | 操作者 |
| `snapshot` | 起案當下 header、details 與 summary |

### JSON 範例（精簡）

```json
{
  "eventId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "eventType": "icp.shipinfo.case.initiated",
  "eventVersion": "1.0",
  "occurredAt": "2026-06-06T12:00:00Z",
  "correlationId": "INV-001",
  "source": "ICP",
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
```

完整 payload 亦存於 `INTEGRATION_EVENT_OUTBOX.PayloadJson`。

## Outbox 狀態

| 狀態 | 說明 |
|------|------|
| `Pending` | 待發送或可重試 |
| `Published` | 已成功發送至 RabbitMQ |
| `Failed` | 超過 `MaxRetryCount` 仍失敗 |

發送失敗時 `RetryCount` 遞增並記錄 `LastError`；未達上限前維持 `Pending`。

## Hub 尚未就緒時

1. 維持 `Integration:RabbitMq:Enabled: false`。
2. 照常起案；查詢 `SELECT * FROM INTEGRATION_EVENT_OUTBOX ORDER BY CreateTime DESC` 確認快照。
3. RabbitMQ 與 Hub 就緒後設 `Enabled: true`，背景服務會發送既有 `Pending` 事件。

## 手動驗證

1. 起案一筆押金或 ARUR。
2. 確認 Outbox 有一筆 `Pending`，`PayloadJson` 含完整 `snapshot`。
3. （可選）啟用 RabbitMQ 後設 `Enabled: true`，確認狀態變為 `Published`。

## 相關程式

| 用途 | 路徑 |
|------|------|
| 起案掛鉤 | `Services/ShipInfoService.cs` |
| 事件工廠 / 快照 | `Services/Integration/ShipInfoCaseEventFactory.cs`、`ShipInfoCaseSnapshotBuilder.cs` |
| Outbox Repository | `Repositories/IntegrationEventOutboxRepository.cs` |
| 背景發布 | `Services/Integration/IntegrationEventOutboxPublisherWorker.cs` |
| RabbitMQ | `Services/Integration/RabbitMqPublisher.cs` |
| 資料表建立 | `Data/IntegrationSchemaInitializer.cs` |
| 事件 DTO | `Models/Integration/ShipInfoCaseInitiatedEvent.cs` |

## 不在範圍

- TEL Integration Hub 消費端
- Hub 回寫 ICP 的 API
- 起案以外的事件類型

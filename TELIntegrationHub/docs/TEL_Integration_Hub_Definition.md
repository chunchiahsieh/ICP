# TEL Integration Hub 定義

**平台名稱**：TEL Integration Hub  
**適用範圍**：GEM、ARUR、ICP 系統整合  
**原始設計文件**：[`0002-Hub/TEL_Integration_Hub_GEM_ARUR_ICP.md`](../../../0002-Hub/TEL_Integration_Hub_GEM_ARUR_ICP.md)（路徑相對 `Projects`：`../0002-Hub/…`）

---

## 1. 定位

TEL Integration Hub 是 GEM、ARUR、ICP 之間的**中央資料中介平台**。

```text
GEM  ─┐
ARUR ─┼── Publish Event ──> RabbitMQ ──> TEL Integration Hub
ICP  ─┘                                      │
                                            ├── 更新目標 DB（GEM / ARUR / ICP）
                                            ├── Publish 給其他 Queue
                                            ├── Message Log
                                            └── Swagger API 查詢
```

| 角色 | 名稱 | 責任 |
|------|------|------|
| 衛星系統 | GEM / ARUR / ICP | 有資料異動時 **Publish** 至 RabbitMQ；初期不需各自實作 Consumer |
| 中央平台 | TEL Integration Hub | Consume、Routing、Transform、寫 DB／Publish、Log、Retry／DLQ、查詢 API |
| 訊息通道 | RabbitMQ | Exchange / Queue / Routing |

---

## 2. Hub 負責什麼

衛星系統只做：

```text
有資料異動 → Publish Event 到 RabbitMQ
```

Hub 統一做：

```text
Consume → Routing → Transform → Write DB / Publish → MessageLog → Retry / DLQ → Swagger 查詢
```

具體包括：

- RabbitMQ 訊息接收
- 訊息格式轉換（Transform）
- 一對一／一對多分發（依 Routing Rule）
- 寫入目標系統資料庫（Upsert）
- 訊息處理紀錄（MessageLog）
- Retry／Dead Letter Queue
- Swagger 查詢 API
- 後續擴充 MES、WMS、BI、ESG、AI 等系統時，主要改 Hub Routing

---

## 3. Hub 不負責什麼（原則）

- 不讓 GEM／ARUR／ICP **彼此直接呼叫**或互寫對方 Consumer
- 衛星系統**不需要知道**其他系統的 DB 結構、Queue 名稱與重送邏輯
- 初期衛星系統**不必**為了整合改成自有 RabbitMQ Consumer（可維持查 DB）

---

## 4. 漸進式階段

| 階段 | 設計 | 說明 |
|------|------|------|
| Phase 1 | Hub Consume + Hub 寫目標 DB + MessageLog + Swagger | 最容易落地 |
| Phase 2 | Hub 寫 DB + 同時 Publish Queue | 新舊並行 |
| Phase 3 | 各系統自己 Consumer；Hub 做 Routing／Governance／Log | 完整事件驅動 |

---

## 5. 標準 Event Envelope（已採用）

ICP Outbox 與 Hub Consumer 已統一使用下列 Envelope（業務資料在 `payload`）：

```json
{
  "messageId": "uuid",
  "eventType": "icp.shipinfo.case.initiated",
  "sourceSystem": "ICP",
  "targetSystems": ["GEM", "ARUR"],
  "occurredAt": "2026-06-06T12:00:00Z",
  "correlationId": "INV-001",
  "version": "1.0",
  "payload": { }
}
```

詳見 [`ICP/docs/integration-events.md`](../../ICP/docs/integration-events.md)。Hub 對舊版 flat schema（`eventId`／`source`）仍有過渡正規化，Outbox Pending 排空後可移除。

---

## 6. 現況 vs 定義（本專案 Phase 1）

| 定義（設計文件） | 目前 `TELIntegrationHub` 實作 |
|------------------|-------------------------------|
| Clean Architecture 多分層（Api／Application／Domain／Infrastructure／Worker） | **單一** Web API 專案 |
| Runtime .NET 8 | **.NET 9**（與 ICP 一致） |
| Exchange `tel.integration.exchange` | **`tel.integration`**（對齊 ICP Outbox） |
| Hub 寫入 GEM／ARUR／ICP 業務 DB | **尚未**；NoOp `ITargetWriter` |
| Hub 回寫 ICP Outbox 狀態 | **已有**：Success → `INTEGRATION_EVENT_OUTBOX.Status = Completed`（`ICP_Connection`） |
| Routing／Transform／Retry 完整服務 | Routing stub；Retry API **501** |
| 一對多分發至多目標 DB | **尚未** |
| ICP 業務 Consume | **押金／ARUR** 已依 `payload.caseType` 分流；**Export** Envelope 契約預留 |
| MessageLog + Swagger 查詢 | **已有**（`GET /api/messages` 等） |
| 標準 Event Envelope | **已採用**（ICP 發送 + Hub 消費；舊 flat 過渡正規化） |

詳細操作與消費者說明見 [README](../README.md)、[phase1-notes](phase1-notes.md)。

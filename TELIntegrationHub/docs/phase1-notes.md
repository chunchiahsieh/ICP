# TEL Integration Hub — Phase 1 摘要

- **Hub 定義**：見 [`TEL_Integration_Hub_Definition.md`](TEL_Integration_Hub_Definition.md)。
- **事件契約**：標準 Event Envelope（ICP + Hub 已對齊）；舊 flat schema 由 Hub 過渡正規化。
- 三種業務消費者（[`Consumers/`](../Consumers/)，一事件一 Consumer）：
  - **押金起案**：`DepositCaseInitiatedConsumer`（`payload.caseType=Deposit`）
  - **ARUR 起案**：`ArurCaseInitiatedConsumer`（`payload.caseType=ARUR`）
  - **匯出檔案**：`ExportFileCompletedConsumer`（預留 Envelope `icp.export.completed`）
- 寫入 Hub `MESSAGE_LOG`；成功後回寫 ICP Outbox `Published` → `Completed`（業務表尚未寫入）。
- ICP 契約說明：`../ICP/docs/integration-events.md`。

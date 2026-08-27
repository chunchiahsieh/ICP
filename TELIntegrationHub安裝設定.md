# TEL Integration Hub 安裝設定

## ILC 資料庫設定

使用具有 ILC DDL 權限的帳號，依序執行：

1. `TELIntegrationHub/sql/001_CreateSerialNumbers.sql`
2. `TELIntegrationHub/sql/002_AlterRtArurHeader_CreateSys.sql`

`SerialNumbers` 與 `RT_ARUR_HEADER` 必須位於同一個 `ILC` 資料庫；Hub 會在同一個交易內取得每日 `PRT-yyyyMMdd-001` 至 `999` 的流水號並寫入 ARUR Header。

## 連線字串

Hub 的設定檔需提供下列連線字串：

- `HubDatabase`：Hub 的 MessageLog 資料庫。
- `ICP_Connection`：ICP 資料庫，用於 Outbox 與 Delivery To 對照查詢。
- `FIESTA_Connection`：FIESTA 資料庫，用於 `MailGroup` 的操作者工號／信箱查詢。
- `ILC_Connection`：ILC 資料庫，用於流水號與 `RT_ARUR_HEADER` 寫入。

## ARUR 失敗處理

Hub 會將欄位驗證、對照資料查無、流水號超限與 SQL 寫入失敗原因寫入 Hub MessageLog，並將 ICP `INTEGRATION_EVENT_OUTBOX` 設為 `Failed`、寫入 `LastError`。**不會自動重試**。若連 Outbox 都標不成 Failed，訊息會留在 Queue 直到標成功。ICP 既有畫面會依此重新開放 ARUR 起案重送；重送會再取一個新的 `PRT-yyyyMMdd-nnn` 寫入 ILC。

ICP 畫面單號為 `ARUR-{發票號}`，ILC `RT_NO` 為 `PRT-yyyyMMdd-nnn`，兩者並存、不對齊。`CreateSys` 寫入 `ICP`。

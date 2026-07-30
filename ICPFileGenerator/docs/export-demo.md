# Export Demo（三系統 HTTP）

簡單端到端：ICP 上傳 → Hub 建 Job → FileGenerator Worker 略過產檔 → Hub 回寫 ICP。

## 啟動順序

1. 執行 FileGen SQL：`sql/001`、`sql/002`（DB：`TEL-ICPFileGenerator`）
2. 啟動 Hub（埠 `5261`）
3. 啟動 ICPFileGenerator（埠 `5208`）
4. 啟動 ICP

## 設定

| 專案 | 關鍵組態 |
|------|----------|
| ICP | `Integration:Hub:BaseUrl` = `http://localhost:5261` |
| Hub | `ICP_Connection`、`ICPFileGenerator`（`TEL-ICPFileGenerator`） |
| FileGen | `FileGenerator:Hub:BaseUrl` = `http://localhost:5261` |

### 重要：ICP 與 Hub 必須同一 ICP 資料庫

兩邊的 **`ConnectionStrings:ICP_Connection`** 必須指向**同一個** Server／Database（本機 AGA 預設皆為 `TEL-ICP`）。

若不一致，Hub 更新 `EXPORT_REQUEST` 會找不到列，Demo API 回傳 **HTTP 404**，訊息會帶出 Hub 實際連到的 `Data Source`／`Initial Catalog`，方便對照 ICP 組態。

## 操作

1. 開啟 ICP **Function → Export**
2. 上傳任意檔案 → Notify Hub
3. 預期：
   - ICP `EXPORT_REQUEST`：Pending → Processing → Completed
   - FileGen Job：Pending → Processing → Completed（`OutputFilePath=SKIPPED`）
   - Hub MessageLog 有 `demo.export.*` 紀錄

## Demo API（Hub）

- `POST /api/demo/export-requests`
- `POST /api/demo/file-jobs/completed`
- `POST /api/demo/file-jobs/failed`

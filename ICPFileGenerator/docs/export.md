# Export（Shipping Advice → Pickup Notice Excel + Case Mark PDF）

端到端：ICP 上傳 Shipping advice Excel → Hub 建 Job → FileGenerator 產檔 → Hub 回寫 → ICP Modal 下載。

## 啟動順序

1. 執行 FileGen SQL：`sql/001`、`sql/002`（DB：`TEL-ICPFileGenerator`）
2. 啟動 Hub（埠 `5261`）
3. 啟動 ICPFileGenerator（埠 `5208`）
4. 啟動 ICP（IIS Express SSL `44388`）

## 設定（同機共用輸出路徑）

| 專案 | 關鍵組態 |
|------|----------|
| ICP | `Integration:Hub:BaseUrl` = `http://localhost:5261` |
| ICP | `Integration:Export:OutputDirectory` = `ICPFileGenerator`（ICP 專案下資料夾） |
| Hub | `ICP_Connection`、`ICPFileGenerator`（`TEL-ICPFileGenerator`） |
| FileGen | `FileGenerator:Hub:BaseUrl` = `http://localhost:5261` |
| FileGen | `FileGenerator:OutputDirectory` = `../ICP/ICPFileGenerator`（與 ICP 同一資料夾） |

兩系統同機；預設路徑解析後為：

```text
{ICP ContentRoot}/ICPFileGenerator/{RequestId}/
```

### 重要：ICP 與 Hub 必須同一 ICP 資料庫

兩邊的 **`ConnectionStrings:ICP_Connection`** 必須指向**同一個** Server／Database。

## 產檔內容

來源 sheet：`to BE Shipping advice Report`（自第 4 列）

輸出資料夾：

```text
{OutputDirectory}/{RequestId}/
  ├── PickupNotice_{yyyyMMdd}.xlsx
  ├── NoCharge_{Invoice}_{yyyyMMdd}.pdf
  └── Charge_{Invoice}_{yyyyMMdd}.pdf
```

- Excel sheet：`to BE New Pick up notice`（依 Invoice No. → Carton No. 排序）
- AH=`X` → NoCharge Case Mark PDF；否則 Charge
- 不同 Invoice 各一份 PDF；每個 Carton No. 一頁

## 操作

1. 開啟 ICP **Function → Export**
2. 上傳 Shipping advice `.xlsx`
3. 狀態：Pending → Processing → Completed
4. 點 **View files** → Modal 可單檔下載 Excel／PDF，或 **Download all (zip)**

## Export API（Hub）

- `POST /api/export/export-requests`
- `POST /api/export/file-jobs/completed`（body 含 `outputFilePath`）
- `POST /api/export/file-jobs/failed`

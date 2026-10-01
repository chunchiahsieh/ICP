# Export（Shipping Advice → Pickup Notice Excel + Case Mark PDF）

端到端：ICP 上傳 Shipping advice Excel → Hub 建 Job → FileGenerator 產檔 → Hub 回寫 → ICP Modal 下載。

## 啟動順序

1. 執行 FileGen SQL：`sql/001`、`sql/002`（DB：本機 `TEL-ICP`／TEL `ICP`）
2. 啟動 Hub（埠 `5261`）
3. 啟動 ICPFileGenerator（埠 `5208`）
4. 啟動 ICP（IIS Express SSL `44388`）

## 設定（同機共用輸出路徑）

| 專案 | 關鍵組態 |
|------|----------|
| ICP | `Integration:Hub:BaseUrl` = `http://localhost:5261` |
| ICP | `Integration:Export:OutputDirectory` = `ICPFileGenerator`（ICP 專案下資料夾） |
| Hub | `ICP_Connection`、`ICPFileGenerator`（與 ICP 同庫：本機 `TEL-ICP`／TEL `ICP`） |
| FileGen | `FileGenerator:Hub:BaseUrl` = `http://localhost:5261` |
| FileGen | `FileGenerator:OutputDirectory` = `../ICP/ICPFileGenerator`（與 ICP 同一資料夾） |

兩系統同機；預設路徑解析後為：

```text
{ICP ContentRoot}/ICPFileGenerator/{RequestId}/
```

### 重要：ICP 與 Hub 必須同一 ICP 資料庫

兩邊的 **`ConnectionStrings:ICP_Connection`** 必須指向**同一個** Server／Database。

## 產檔內容

來源 sheet：上傳 Excel 的第一個工作表（依分頁順序，名稱不限）。第 1 列為欄位標題，資料自第 2 列開始；Invoice No. 與 Carton No. 都空白的列略過。
請將要匯出的資料放在第一個工作表；若第一個工作表缺少必要欄位或沒有資料，該次 Export 失敗，不會改讀其他工作表。

輸出資料夾：

```text
{OutputDirectory}/{RequestId}/
  ├── PickupNotice_{yyyyMMdd}.xlsx
  ├── NoCharge_{Invoice}_{yyyyMMdd}.pdf
  └── Charge_{Invoice}_{yyyyMMdd}.pdf
```

- Excel sheet：`to BE New Pick up notice`（依 Invoice No. → Carton No. 排序）
- Pickup Notice 每張 Invoice 的每個 Carton No. 只保留來源檔第一筆；不同 Invoice 的相同箱號分別保留。
- Ship to address 直接使用來源的 `Ship-to Party Address`，保留完整地址。
- 提貨地點／Contact Person／Phone No.：以 Shipping Advice **欄位 C（SLOC）** 對照 ICP `SystemConfigs`（`Category=PickUpLocation`，`Key1`=SLOC → `Value1`／`Value2`／`Value3`）；找不到則空白
- AH=`X` → NoCharge Case Mark PDF；否則 Charge
- 不同 Invoice 各一份 PDF；每個 Carton No. 一頁
- Charge Case Mark：公司名稱取 `Sold-to Party`；`PORT OF DISCHARGE` 取 `Port of Entry`；`PO NO.` 取 `PO#`（不再顯示 TEA 前綴）。

## 操作

1. 開啟 ICP **Function → Export**
2. 上傳 Shipping advice `.xlsx`
3. 狀態：Pending → Processing → Completed
4. 點 **View files** → Modal 可單檔下載 Excel／PDF，或 **Download all (zip)**

## Export API（Hub）

- `POST /api/export/export-requests`
- `POST /api/export/file-jobs/completed`（body 含 `outputFilePath`）
- `POST /api/export/file-jobs/failed`

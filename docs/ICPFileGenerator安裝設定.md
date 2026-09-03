# ICPFileGenerator 安裝設定

Export 產檔（Pickup Notice Excel／Case Mark PDF）依賴 ICP 同庫的 Job 表與預存程序。本機與客戶平台都需執行下列腳本，否則 Worker 會失敗。

常見錯誤：

```text
找不到預存程序 'dbo.ClaimNextFileGenerationJob'
```

原因：資料庫只有表、沒有建 SP，或腳本執行在錯誤的資料庫。

---

## 1. 執行位置（與 ICP 同庫）

| 環境 | SQL Server | 資料庫 |
|------|------------|--------|
| 本機（AGA） | `AGA-PC\SQLEXPRESS01` | `TEL-ICP` |
| 客戶／TEL | `tetis87146` | `ICP` |

說明：

- Job 表與 SP 建在 **ICP 應用同一資料庫**，不是獨立的 FileGen 庫。
- Hub、ICPFileGenerator 的 `ConnectionStrings:ICPFileGenerator` 必須指向上表同一 Server／Database。

---

## 2. 腳本清單

路徑：`ICPFileGenerator/sql/`

| 順序 | 檔案 | 作用 |
|------|------|------|
| 1 | `001_CreateDatabase_And_JobTable.sql` | 建立表 `dbo.ICPFileGeneratorJob`（若尚不存在） |
| 2 | `002_ClaimNextFileGenerationJob.sql` | 建立／更新預存程序 `dbo.ClaimNextFileGenerationJob` |

可選：`003_Sample_Pending_Job.sql`（本機測試用範例 Pending Job，正式環境不必執行）。

---

## 3. 安裝步驟

### 3.1 用 SSMS 連線

連到目標環境的 SQL Server，並確認選到正確資料庫（主機／TEL：`ICP`；本機 AGA 可能為 `TEL-ICP`）。

### 3.2 調整 USE（本機 AGA 若庫名不同再改）

腳本預設為主機／TEL：

```sql
USE [ICP];
```

本機 AGA 若資料庫名稱為 `TEL-ICP`，執行前請改為：

```sql
USE [TEL-ICP];
```

`001`、`002`（以及若使用 `003`）都要一致。

### 3.3 依序執行

1. 開啟並執行 `001_CreateDatabase_And_JobTable.sql`
2. 開啟並執行 `002_ClaimNextFileGenerationJob.sql`

若表已手動建過，仍請執行 **002**（缺 SP 就會出現本文開頭的錯誤）。

---

## 4. 驗證

在目標資料庫執行：

```sql
-- 表
SELECT OBJECT_ID(N'dbo.ICPFileGeneratorJob', N'U') AS JobTableId;

-- 預存程序
SELECT OBJECT_ID(N'dbo.ClaimNextFileGenerationJob', N'P') AS ClaimProcId;
```

兩欄皆不應為 `NULL`。

物件總覽亦可在 SSMS：

```text
資料庫 → Tables → dbo.ICPFileGeneratorJob
資料庫 → Programmability → Stored Procedures → dbo.ClaimNextFileGenerationJob
```

---

## 5. 啟動順序（產檔驗證）

物件就緒後建議啟動順序：

```text
TEL Integration Hub
  ↓
ICPFileGenerator（埠 5208）
  ↓
ICP
```

ICPFileGenerator 日誌不應再出現 `找不到預存程序 'dbo.ClaimNextFileGenerationJob'`。

詳細產檔說明見：`ICPFileGenerator/docs/export.md`。

---

## 6. 常見問題

| 現象 | 處理 |
|------|------|
| 找不到預存程序 `ClaimNextFileGenerationJob` | 在正確 ICP 庫執行 `002`；確認 `USE` 資料庫名稱 |
| 找不到物件／表不存在 | 先執行 `001`，再執行 `002` |
| 本機正常、客戶平台失敗 | 客戶庫未跑腳本，或本機改了 `USE` 後未在客戶庫執行 |
| Hub／FileGen 連線的 Database 名稱不一致 | 對齊 `appsettings`：本機可為 `TEL-ICP`、TEL／主機為 `ICP` |

# ICP

ASP.NET Core MVC (.NET 9) 專案骨架。

> Solution 檔位於上一層：`../ICP.sln`（含 ICP 與 TEL.IntegrationHub）。建置請在 `Projects` 執行 `dotnet build ICP.sln`。  
> Hub 專案：[`../TELIntegrationHub`](../TELIntegrationHub/)。

## 環境需求

- .NET SDK 9.0+
- SQL Server / LocalDB（執行時資料庫；預設連線使用 `(localdb)\MSSQLLocalDB`）

## 專案結構

```
ICP/
├─ Controllers/         # MVC Controllers
│  └─ HomeController.cs
├─ Data/                # EF Core DbContext
│  └─ ApplicationDbContext.cs
├─ Models/              # View Model / Entity
├─ Views/               # Razor Views（含 Views/Permission/ 權限管理後台）
├─ Services/            # 含 UserResourcePermissionService、PermissionScannerService 等
├─ wwwroot/             # 靜態資源（CSS / JS / 第三方 lib）
├─ Program.cs           # 入口 + DI 設定
├─ appsettings.json     # 組態（含連線字串）
├─ Prompt.md            # 權限管理 RBAC 需求分析 Prompt（可複製給 AI）
└─ ICP.csproj
```

## 設定連線字串

於 `appsettings.json` 修改 `ConnectionStrings:DefaultConnection`：

```json
"ConnectionStrings": {
  "DefaultConnection": "Server=...;Database=...;User Id=...;Password=...;TrustServerCertificate=True"
}
```

> 本機開發若沒裝 SQL Server，可不更動預設值；只要不實際呼叫 `DbContext`，App 仍可正常啟動。

## 常用指令

```powershell
# 還原套件
dotnet restore

# 編譯
dotnet build

# 開發模式執行（含熱重載）
dotnet watch run

# 一般執行
dotnet run

# EF Core Migration（未來新增 Entity 後）
dotnet ef migrations add InitialCreate
dotnet ef database update
```

## 部署注意事項

上線至 TEL 環境前，請逐項確認下列設定與流程。

### 組態檔（必查）

| 項目 | 本機開發 | TEL 正式 / UAT |
|------|----------|----------------|
| 組態來源 | `appsettings.json`（AGA 電腦） | **`appsettings.TEL.json`**（非 AGA 電腦僅載入此檔，見 `Program.cs`） |
| `App.SuperUser` | 可設 `"On"` 供除錯 | **必須 `"Off"`** — 若為 On 將跳過後端 Route 權限中間層，等同無 RBAC |
| `App.Mode` | 依需求 | **建議 `"PRD"`** — 使用 Windows 整合登入與 ILC 使用者主檔 |
| `App.SimulatedWindowsIdentity` | 僅本機 AGA 模擬用 | **正式環境勿設定或留空** |
| 連線字串 | 本機 SQL Server | 指向 TEL 的 ICP / ILC / FIESTA 資料庫；**勿將密碼 commit 至版控** |
| `AllowedHosts` | 可為 `*` | 建議改為實際網域，降低 Host Header 風險 |

> 部署前以文字搜尋確認：`"SuperUser": "On"` 不得出現在 `appsettings.TEL.json` 或正式環境組態中。

### IIS / 執行環境

- 啟用 **Windows 驗證**（Anonymous 關閉），與 `launchSettings.json` / IIS 設定一致
- 應用程式集區身分需能存取 ICP、ILC、FIESTA 三個資料庫
- 部署後**回收或重啟應用程式集區**，使組態與 Route 索引快取生效
- 確認 `Logs/` 目錄可寫入（Serilog 檔案日誌）

### 權限模組上線流程

1. 發佈前執行 `dotnet build`，確認編譯成功
2. 若 Views 有新增或變更 `data-permissions`，上線後執行 **`POST /Admin/PermissionScan`** 同步 Resources 表
3. 調整角色、RolePermissions 或角色指派後，**使用者需重新登入** Session 才會更新
4. 上線驗收：以受限角色登入，確認側欄、頁面、直接 URL、POST API 皆受權限控管

### 禁止事項

- 正式環境 **`App.SuperUser: "On"`**
- 將含真實密碼的 `appsettings.json` / `appsettings.TEL.json` 提交至 Git
- 僅改 DB 角色權限卻未通知使用者重新登入（Session 仍為舊權限）
- 略過 PermissionScan 即開放新功能頁（DB 無 Route / ResourceCode，後端可能未保護該端點）

### 參考檔案

- 組態範本：[`appsettings.example.json`](appsettings.example.json)
- 部署後驗收：[`Prompt.md`](Prompt.md) 第 10 節驗收清單

---

## 加入新 Entity 的流程

1. 在 `Models/` 建立實體類別。
2. 在 `Data/ApplicationDbContext.cs` 增加 `DbSet<TEntity>`。
3. 執行 `dotnet ef migrations add <Name>` → `dotnet ef database update`。
4. 建立對應 Controller 與 View（可用 `dotnet aspnet-codegenerator` 或手刻）。

---

## 權限管理 — 需求分析 Prompt

完整 Prompt 已移至 **[`Prompt.md`](Prompt.md)**（可整份複製貼給 AI 作為 RBAC 實作規格）。

**使用方式**

1. 開啟 [`Prompt.md`](Prompt.md)，複製全文貼到 AI 對話。
2. 實作時以本 repo 的 `Views/Permission/`、`Services/` 為 UI 與邏輯參考範本。
3. Views 新增或變更 `data-permissions` 後，需執行 `POST /Admin/PermissionScan` 同步 Resources 至 DB。
4. **ResourceName** 為後台操作者閱讀用，固定繁中寫入 DB；掃描器不讀按鈕 `@Localizer` 文字。新增 ResourceCode 時請在 `SharedResource.resx` 補同名 key。詳見 [`Prompt.md`](Prompt.md) 第 3 節。
4. 正式環境 `App.SuperUser` 必須為 `"Off"`（詳見上方「部署注意事項」）；角色或權限變更後，使用者需**重新登入**以更新 Session 權限。
5. 多國語言 key 命名請遵循 `.cursor/rules/views-localization.mdc`。

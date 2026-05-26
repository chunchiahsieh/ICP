# ICP

ASP.NET Core MVC (.NET 9) 專案骨架。

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
├─ Views/               # Razor Views
├─ wwwroot/             # 靜態資源（CSS / JS / 第三方 lib）
├─ Program.cs           # 入口 + DI 設定
├─ appsettings.json     # 組態（含連線字串）
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

## 加入新 Entity 的流程

1. 在 `Models/` 建立實體類別。
2. 在 `Data/ApplicationDbContext.cs` 增加 `DbSet<TEntity>`。
3. 執行 `dotnet ef migrations add <Name>` → `dotnet ef database update`。
4. 建立對應 Controller 與 View（可用 `dotnet aspnet-codegenerator` 或手刻）。

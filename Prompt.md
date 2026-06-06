> 本檔為 ICP 權限管理（RBAC）完整需求規格，可整份複製貼給 AI 使用。部署限制請見 [`README.md`](README.md)「部署注意事項」。

# 需求：ASP.NET Core MVC 權限管理（RBAC）全套功能

請依下列規格，在 ASP.NET Core MVC (.NET 9) 專案中實作完整權限管理。UI 採 Bootstrap + jQuery DataTables（ProDataTables 模式）；所有使用者可見文字須多國語言（四份 SharedResource*.resx：zh-TW / en / ja / fallback）。

---

## 1. 專案前提

- 框架：ASP.NET Core MVC .NET 9、EF Core、Session、Serilog
- 登入：Windows 整合登入；使用者主檔來自 ILC `UserInfoAd`（TelId、DepId 等）
- 資料庫：
  - **ICP**：Resources、Roles、RolePermissions、RolesTelId、RolesDepId、RolesMailGroup
  - **ILC**：UserInfoAd（使用者列表唯讀）
  - **FIESTA**：MailGroup（依 TelId 查 Address，供 RolesMailGroup 指派）
- Permission 模組 Controller 加 `[PermissionModule]`；View 解析至 `Views/Permission/{Controller}/`（Resources Controller 對應 `Views/Permission/RoleResources/`）
- 連線字串：`ICP_Connection`、`ILC_Connection`、`FIESTA_Connection`

---

## 2. 資料模型

### Resources（系統資源）
- 欄位：Id、ParentId、SystemCode、ModuleCode、ResourceCode、ResourceName、ResourceType、Route、Icon、Sort、IsVisible、IsEnabled、Description、稽核欄位
- ResourceCode 唯一識別；ResourceType 例：Page、Button、Menu、Menu Category、Field、API

### Roles（角色）
- RoleCode、RoleName、IsEnabled、Description、稽核欄位

### RolePermissions（角色 × 資源）
- RoleId、ResourceId、ActionCode、IsAllowed、DataScope、Description、稽核欄位
- 批次建立時 ActionCode 不可寫入完整 ResourceCode（max 50），須依 ResourceType 解析（見第 4 節）

### 角色指派（三維度）
| 表 | 鍵值 | 說明 |
|----|------|------|
| RolesTelId | TelId + RoleId | 依員工編號指派角色 |
| RolesDepId | DepId + RoleId | 依部門編號指派角色 |
| RolesMailGroup | Address + RoleId | 依 FIESTA MailGroup Address 指派角色 |

---

## 3. Resource 命名與 ResourceType（掃描規則）

Views 中所有需控管項目加 `data-permissions` 與 `data-i18n-key`（**兩者值相同**）。掃描 `Views/**/*.cshtml` 寫入 Resources 表。

| 位置 | ResourceCode 範例 | HTML | ResourceType |
|------|-------------------|------|--------------|
| 側欄區塊標題 | `Views.Shared._SidebarNav.Setting` | `<div class="sb-sidenav-menu-heading">` | Menu Category |
| 側欄選單連結 | `Views.Shared._SidebarNav.Systems.Permissions.Users` | `<a class="nav-link">` | Menu |
| 頂部品牌連結 | `Views.Shared._Layout.Home` | `<a>` | Menu |
| 功能頁外層 | `Views.Permission.RoleDepIds.View` | `<div>`（非 button） | Page |
| 按鈕 | `Views.Permission.RoleDepIds.Create` | `<button>` | Button |

- Menu Category 判定：`div` + ResourceCode 為 `Views.Shared._SidebarNav.{區塊}`（四段）
- ModuleCode：SidebarNav 取第 4 段（如 Setting、Systems）；Permission 頁取第 3 段（如 RoleDepIds）
- 掃描 API：`POST /Admin/PermissionScan` → PermissionScannerService + PermissionResourceSyncService Upsert

---

## 4. ActionCode 規則（RolePermissions 批次建立）

共用 Helper `RolePermissionActionCodes.Resolve(Resource)`：

| ResourceType | ActionCode |
|--------------|------------|
| Menu Category | Allow |
| Menu | Allow |
| Page / Button | ResourceCode **末段**（View、Create、Delete、Edit、Scan、Disable、Export、Approve 等） |
| 其他 | Allow |

---

## 5. 後台管理頁面（Views/Permission/）

均需：`[PermissionModule]` Controller、ProDataTables 篩選列表、四語系 resx、對應 `Views.Permission.*` 的 data-permissions。

| 模組 | 功能 |
|------|------|
| **Users** | ILC 使用者列表；「權限」按鈕開 Modal 顯示 JSON；Modal 標題列 fa-copy 複製 JSON 至剪貼簿 |
| **RoleResources**（Resources Controller） | Resources 列表 + Scan 按鈕；含 ResourceType 篩選 |
| **Roles** | 角色列表；建立、編輯、停用 |
| **RolePermissions** | 已建立清單（含 ResourceType 欄）；批次建立精靈（選 Role + 選 Resource）；批次刪除 |
| **RoleTelIds** | 精靈：選 Role + 選 User(TelId)；結果列表；批次刪除 |
| **RoleDepIds** | 精靈：選 Role + 選 DepId；結果列表；批次刪除 |
| **RoleMailGroups** | 精靈：選 Role + 選 MailGroup(Address)；結果列表；批次刪除 |

側欄入口（`_SidebarNav.cshtml`）放在 Systems → Permissions 子選單。

---

## 6. 使用者權限 JSON API

`GET /Users/GetPermissions?keyId={int}` 回傳：

```json
{
  "keyId": 0,
  "telId": "...",
  "roleAssignments": {
    "roleTelIds": [],
    "roleDepIds": [],
    "roleMailGroups": []
  },
  "resources": [
    {
      "resourceId": "...",
      "resourceCode": "Views.Permission.Users.View",
      "resourceName": "...",
      "resourceType": "Page",
      "systemCode": "Views",
      "moduleCode": "Users",
      "route": "/Users",
      "actionCode": "View",
      "isAllowed": true
    }
  ]
}
```

**Resources 合併邏輯**（UserResourcePermissionService）：
1. 依使用者 TelId / DepId / FIESTA MailGroup 查出所有啟用中的 Role 指派
2. 合併 Role 的 RolePermissions；順序 **RoleMailGroups → RoleDepIds → RoleTelIds**（同 ResourceId 後蓋前）
3. 僅 `IsAllowed=true` 且 Resource.IsEnabled=true
4. 去重後依 ResourceCode 排序

---

## 7. Session 與 Runtime 權限管控

### UserResourcePermissionService
- Session key：`UserResources`（JSON 序列化 Resources 列表）
- 登入成功後 `RefreshSessionResourcesAsync`；`RequireLoginFilter` 若 Session 無則 `EnsureSessionResourcesAsync`
- 登出清除 Session 權限
- 方法：
  - `HasPermission(resourceCode)`：ResourceCode 相符且 IsAllowed；Menu/Menu Category 另需 ActionCode=Allow
  - `HasMenuCategoryPermission(resourceCode)`：ResourceType=Menu Category 且 ActionCode=Allow
  - `HasAllow` 委派 `HasPermission`（供側欄 Menu 使用）
  - `GetAllowedResourceCodes()`：供前端注入

### 側欄（server-side）
- `_SidebarNav.cshtml`：`ShowMenuCategory(code)` 僅在有 Menu Category 權限時顯示區塊標題；各 `<a>` 以 `HasPerm(code)` 控制

### Views（client-side）
- `_IcpScriptI18n.cshtml` 注入 `window.IcpPermissions = { superUser, allowedCodes }`
- `wwwroot/js/icp-permissions.js`：DOMContentLoaded 掃描 `[data-permissions]`
  - 無權限 → `hidden=true`
  - ResourceType=Page 無權限 → 隱藏元素 + 顯示 `#icp-page-access-denied` alert banner（`Permission.AccessDenied` 多語系）
  - Menu Category 標題無權限 → 隱藏
- SuperUser 時前端 `superUser:true` 全放行

### 後端 Route 權限中間層（RequireResourcePermissionFilter）

防止直接輸入 URL 或 curl 繞過前端 `icp-permissions.js`。

| 元件 | 路徑 |
|------|------|
| Route 索引快取 | `Services/ResourceRouteRegistryService.cs` |
| 請求 → ResourceCode | `Helpers/PermissionRequestResolver.cs` |
| Route 正規化 | `Helpers/PermissionRouteNormalizer.cs` |
| 全域 Filter | `Filters/RequireResourcePermissionFilter.cs` |

**流程**（在 `RequireLoginFilter` 之後）：

1. **`App.SuperUser: On` 時整層跳過**（不解析 Route、不讀 Session 權限 JSON，所有請求直接放行）
2. `[AllowAnonymous]`、`[SkipResourcePermission]` → 跳過
3. `PermissionRequestResolver` 依 Controller / Action / HTTP Method 解析所需 ResourceCode
4. GET `Index`：以 `/Controller` 查 DB 索引中 `ResourceType=Page` 的 Route
5. POST API：`Views.Permission.{Module}.{View|Create|Delete|Edit|Disable|Scan}`（`Resources` controller → module `RoleResources`）
6. 推導出的 ResourceCode 必須已於 DB 註冊（PermissionScan）才受保護
7. `UserResourcePermissionService.HasPermission(code)` 比對 Session 使用者權限 JSON
8. 無權：AJAX/JSON → 403 `{ success: false, message }`；一般 GET → 導向 `ErrorPage/Index`

**Action 對照**（新增 Controller action 時需同步更新 `PermissionRequestResolver`）：

| Action 模式 | 所需 ResourceCode 末段 |
|-------------|------------------------|
| `Index`（GET） | Page（由 Route 索引） |
| `Query*`、`GetFilterOptions*`、`Lookup`、`Get` | `View` |
| `GetPermissions` | `Views.Permission.Users.View` |
| `BatchCreate` | `Create` |
| `BatchDelete` | `Delete` |
| `Save`（Roles） | 新建 `Create`；編輯 `Edit` |
| `Disable` / `BatchDisable` | `Disable` |
| `PermissionScan`（Admin） | `Views.Permission.RoleResources.Scan` |

**驗證**：

- 角色僅授權 `Views.Permission.Users.View` → GET `/Users` 200；GET `/RoleDepIds` 403；POST `/RoleDepIds/BatchCreate` 403
- 僅授權 Menu（無 Page View）→ GET 對應頁面仍 403
- `PermissionScan` 後 `ResourceRouteRegistryService.RefreshAsync` 更新索引

---

## 8. SuperUser（開發 bypass）

`appsettings.json` → `App.SuperUser: "On" | "Off"`（預設 Off）

- **On**：`RequireResourcePermissionFilter` 整層跳過；`HasPermission` / 側欄 / 前端 `superUser:true` 一律放行；不載入 Session Resources；僅供本機除錯
- **Off**：正常權限檢查；TEL 正式環境必須 Off

---

## 9. 多國語言

- 四份 `Resources/SharedResource*.resx`
- Key 慣例：
  - 頁面：`{Feature}.Title`
  - 側欄：`Views.Shared._SidebarNav.{區塊}.{項目}`
  - 權限掃描：`Views.Permission.{Feature}.View` / `.Create` / `.Delete` 等
  - JS：`Permission.AccessDenied`、`Users.CopyPermissionsJson` 等經 IcpScriptI18nBuilder 輸出
- 詳細規則見專案 `.cursor/rules/views-localization.mdc`

---

## 10. 驗收清單

- [ ] PermissionScan 後 DB Resources 與 Views 的 data-permissions 一致
- [ ] RolePermissions 批次建立 ActionCode 正確（Menu→Allow，Page.View→View）
- [ ] 角色指派後 GetPermissions JSON 正確；Resources 合併順序正確
- [ ] 重新登入後 Session 更新；側欄僅顯示有 Allow 的 Menu Category / Menu
- [ ] 僅授權單一 Menu Category 時，只顯示該區塊標題（其他區塊標題不顯示）
- [ ] Page 無 View 權限：內容隱藏 + main 區警告 banner；Button 無權限僅隱藏
- [ ] SuperUser Off 時上述規則生效；SuperUser On 時全部可見
- [ ] Users 權限 JSON Modal 可一鍵複製
- [ ] RolePermissions 列表顯示 ResourceType 欄位
- [ ] 後端 Route 權限：無 View 權限時 GET 頁面 403；無 Create/Delete 時 POST API 403
- [ ] PermissionScan 後 Route 索引立即更新

---

## 11. 實作參考（本 repo 路徑，供對照勿硬編碼複製）

| 用途 | 路徑 |
|------|------|
| DbContext / Entity | Data/ApplicationDbContext.cs、Models/Icp/* |
| 掃描 | Services/PermissionScannerService.cs、Services/PermissionResourceSyncService.cs |
| ActionCode | Helpers/RolePermissionActionCodes.cs |
| ResourceType | Helpers/PermissionResourceTypes.cs |
| 使用者權限 | Services/UserResourcePermissionService.cs、Models/UserPermissionsResponse.cs |
| 登入 Session | Services/UserAuthService.cs、Filters/RequireLoginFilter.cs |
| 後端 Route 權限 | Filters/RequireResourcePermissionFilter.cs、Services/ResourceRouteRegistryService.cs、Helpers/PermissionRequestResolver.cs |
| 側欄 | Views/Shared/_SidebarNav.cshtml |
| Runtime JS | wwwroot/js/icp-permissions.js、Views/Shared/_IcpScriptI18n.cshtml |
| SuperUser | Models/Auth/AppAuthOptions.cs |
| 後台 UI | Views/Permission/{Users,RoleResources,Roles,RolePermissions,RoleTelIds,RoleDepIds,RoleMailGroups}/ |
| 掃描 API | Controllers/AdminController.cs → PermissionScan |

請依以上規格實作，並確保 dotnet build 通過。

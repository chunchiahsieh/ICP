# Ship Info Form Metadata JSON 設定參數說明

本文件定義 Ship Info Header 的 JSON 表單設定。它只控制前端 UI；資料更新仍一律經既有 `SaveHeaderAsync`。

## 整體結構

```json
{
  "formId": "shipinfo-header",
  "metadataVersion": "1.0",
  "fields": {},
  "modes": {
    "view": { "groups": [] },
    "edit": { "groups": [] },
    "create": { "groups": [] }
  }
}
```

| 參數 | 型別 | 必填 | 用途 |
|---|---|---:|---|
| `formId` | string | 是 | 固定為 `shipinfo-header`。 |
| `metadataVersion` | string | 是 | Metadata 版本。 |
| `fields` | object | 是 | 以欄位名稱為 key 的共用定義。 |
| `modes` | object | 是 | `view`、`edit`、`create` 的獨立畫面定義。 |

`view`、`edit` 必須存在；`create` 為 PoC 預覽模式。某模式未列出的欄位不顯示，也不會自動加入欄位。

## 共用欄位：`fields`

| 參數 | 型別 | 必填 | 預設值 | 用途 |
|---|---|---:|---|---|
| `labelKey` | string | 否 | 既有 DisplayName／欄位名稱 | 多國語言資源鍵。 |
| `type` | string | 是 | 無 | `text`、`date`、`select`、`checkbox`。 |
| `maxLength` | number | 否 | 既有資料規則 | 最大輸入長度；必須大於 0。 |
| `placeholderKey` | string | 否 | 無 | Placeholder 資源鍵。 |
| `helpTextKey` | string | 否 | 無 | 說明文字資源鍵。 |
| `options` | array | Select 擇一 | 無 | 固定選項。 |
| `optionsSource` | string | Select 擇一 | 無 | 已註冊的動態選項來源。 |
| `checkedValue` | string | Checkbox 是 | 無 | 勾選時回傳值。 |
| `uncheckedValue` | string | Checkbox 是 | 無 | 未勾選時回傳值。 |

第一版參數組合：

| `type` | 可用參數 |
|---|---|
| `text` | `labelKey`、`maxLength`、`placeholderKey`、`helpTextKey` |
| `date` | `labelKey`、`placeholderKey`、`helpTextKey`；值契約為 `yyyy-MM-dd` |
| `select` | `labelKey`、`options` 或 `optionsSource`、`helpTextKey` |
| `checkbox` | `labelKey`、`checkedValue`、`uncheckedValue`、`helpTextKey` |

欄位 key 必須是既有 Ship Info Header ViewModel 的欄位名稱。

## 模式與群組：`modes.{mode}.groups`

| 模式 | 用途 |
|---|---|
| `view` | 查看資料。Renderer 無條件強制所有欄位唯讀。 |
| `edit` | 修改既有 Header；能否儲存仍由後端判定。 |
| `create` | PoC-A 僅空資料渲染、預設值與前端驗證；頁面不允許送出。 |

群組參數：

| 參數 | 型別 | 必填 | 預設值 | 用途 |
|---|---|---:|---|---|
| `id` | string | 是 | 無 | 同一模式中唯一的群組識別。 |
| `labelKey` | string | 否 | 不顯示標題 | 群組標題資源鍵。 |
| `order` | number | 否 | JSON 原始順序 | 群組排序。 |
| `columns` | number | 否 | `1` | 每列欄數，允許 `1`～`4`。 |
| `component` | string | 否 | 無 | 受控的非欄位元件。目前只支援 `fileUploader`。 |
| `adapter` | string | component 是 | 無 | 受控元件的 Adapter 名稱。目前 `fileUploader` 只允許 `shipInfoHeaderAttachments`。 |
| `fields` | array | 是 | 無 | 一般欄位群組的欄位與模式覆寫；元件群組必須是空陣列。 |

空 `groups` 允許，代表該模式不顯示任何欄位。群組 `id` 不可重複；同一模式的欄位不可跨群組重複引用。

模式欄位覆寫參數：

| 參數 | 型別 | 必填 | 預設值 | 用途 |
|---|---|---:|---|---|
| `name` | string | 是 | 無 | 引用 `fields` 的欄位 key。 |
| `order` | number | 否 | JSON 原始順序 | 群組內排序。 |
| `readOnly` | boolean | 否 | `true` | 禁止該模式編輯。View 一律會強制為 `true`。 |
| `required` | boolean | 否 | `false` | 前端必填驗證。 |
| `columnSpan` | number | 否 | `1` | 橫跨欄數，不可大於群組 `columns`。 |

合併採淺層覆寫：

```javascript
const effectiveField = {
  ...baseField,
  ...modeField,
  name: modeField.name
};
```

物件與陣列不深層合併。模式覆寫僅控制 UI，不能改變後端可更新欄位、權限或資料型別。

## 附件群組：`component: "fileUploader"`

附件不是 `ICP_HEADER.ATTACHED_FILE` 的文字輸入欄位，而是由既有附件 API 與 Adapter 管理的獨立 UI 區塊。將它宣告在 `groups` 可控制附件出現在表單的哪個位置，以及在哪些模式顯示。

```json
{
  "id": "attachments",
  "labelKey": "ShipInfo.Group.Attachments",
  "order": 20,
  "columns": 1,
  "component": "fileUploader",
  "adapter": "shipInfoHeaderAttachments",
  "fields": []
}
```

規則：

- `component` 為 `fileUploader` 時，`adapter` 必須為 `shipInfoHeaderAttachments`，且 `fields` 必須為空陣列。
- `view` 模式僅列出與下載附件；`edit` 模式才會顯示上傳與刪除。
- JSON 不可指定附件 API URL、`AttachmentType`、Owner Id、儲存路徑、副檔名或檔案大小；這些均由 Adapter、Controller 與 Service 的既有權限及驗證規則決定。
- 未知的 `component`／`adapter` 一律視為 Metadata 錯誤，採 Fail Closed。

## Select 與 Checkbox

固定 Select：

```json
"ImportExport": {
  "labelKey": "ShipInfo.Field.ImportExport",
  "type": "select",
  "options": [
    { "value": "Import", "labelKey": "Common.Import" },
    { "value": "Export", "labelKey": "Common.Export" }
  ]
}
```

動態 Select：

```json
"Broker": {
  "labelKey": "ShipInfo.Field.Broker",
  "type": "select",
  "optionsSource": "broker"
}
```

`options` 與 `optionsSource` 必須擇一。第一版已註冊來源只有 `broker`；JSON 不可指定 URL、SQL 或 JavaScript。

Checkbox：

```json
"DriverDetails": {
  "labelKey": "ShipInfo.Field.DriverDetails",
  "type": "checkbox",
  "checkedValue": "Y",
  "uncheckedValue": "N"
}
```

Renderer 內部使用 Boolean；讀取與送出 Payload 使用 `Y/N`。兩個 mapping 值不可相同。

## 多國語言

沿用既有：`SharedResource.resx`、`SharedResource.zh-TW.resx`、`SharedResource.en.resx`、`SharedResource.ja.resx`。

1. `labelKey` 有資源：顯示目前 Culture 的文字。
2. `labelKey` 找不到：欄位回退既有 DisplayName／欄位名稱；群組回退 `id`；僅開發環境記錄警告。
3. 未設定 `labelKey`：欄位回退既有 DisplayName／欄位名稱；群組不顯示標題；不記錄警告。

## 驗證與失敗行為

Metadata 載入時驗證：必要屬性、欄位是否存在於 Header ViewModel、支援欄位／群組元件、模式名稱、重複群組／欄位、`columns`、`columnSpan`、Select 設定、Checkbox mapping，以及未文件化的 JSON 屬性。

任何未知模式、欄位、元件或設定錯誤都採 **Fail Closed**：停止表單渲染、顯示受控錯誤，且不回退成一般 Text input。

## 安全邊界

> Form Metadata 只控制前端 UI，不代表使用者具有資料修改權限。

實際更新仍必須通過：

```text
SaveHeaderAsync
→ 功能權限
→ Header 狀態
→ 並行控制
→ 後端 Editable Field 白名單
→ Audit / Transaction / Outbox
```

實際可更新欄位為：前端模式允許編輯 ∩ 後端 Editable Field 白名單 ∩ 使用者權限 ∩ Header 狀態。

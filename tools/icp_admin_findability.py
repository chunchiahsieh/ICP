"""Quick navigation and maintenance records for the administrator guide."""

def quick_index(table,p,h):
    h('1.3 一頁快速索引').paragraph_format.page_break_before=True
    p('先依症狀跳至指定章節；完整流程只在主題章節維護。章節連結可點擊，頁碼隨文件更新。此索引不要求重新啟動已運行的 Hub 或 ICPFileGenerator。',kind='維運建議')
    table(['遇到的問題','先看與接著看','帶著哪些資料查'],[
        ('畫面沒有更新或欄位不對','[[15]] → [[14.1]] → [[26.2]]','網址、站台實體路徑、版本、Config、Network 載入檔案。'),
        ('Excel 上傳失敗','[[8.1]] → [[15.2]]','第一分頁、欄位、列號、原始錯誤與 Settings 比對值。'),
        ('產檔不動或只在呼叫 API 後執行','[[13.2]] → [[21.2]] → [[26.4]]','程序／Worker 啟動時間、RequestId、Job 狀態、輸入輸出路徑。'),
        ('起案未完成或想重送','[[21.1]] → [[24.1]] → [[26.4]]','messageId、Outbox、Hub Log、下游寫入結果。'),
        ('通知未收到或想更改名單','[[21.3]] → [[12.1]] → [[20.4]]','通知種類、排程、EventKey、有效設定及收件者來源。'),
        ('檔案無法下載或找不到','[[15.2]] → [[11.3]]','附件／原檔／產檔類別、實體路徑與檔案存在狀態。'),
        ('權限不符或 Settings 無法上傳','[[9.2]] → [[26.7]]','使用者、角色、頁面 Resource、Session、資料範圍。'),
        ('要修改欄位或新增功能','[[17.2]] → [[23]] → [[18.3]]','需求編號、修改層次、測試專案與回復影響。')
    ],[1.75,2.5,2.75])
    p('管理者從 [[5]]、[[12]]、[[15]] 開始；開發者從 [[2]]、[[4]]、[[25.2]] 開始。搜尋用 [[1.4]]；管理畫面見 [[26.8]]。',kind='維運建議')

def setting_impact(table,p,h):
    h('20.4 設定修改影響與本地核對').paragraph_format.page_break_before=True
    p('下表的消費方式以程式為依據；「重啟建議」是維運操作，不表示所有值都只能重啟才會生效。修改前先依 [[5.1]] 確認來源。環境變數及命令列可能覆寫 JSON；不以檔案已存檔判定有效值已變更。',kind='維運建議')
    table(['修改位置與內容','生效與既有資料影響','本地核對方式'],[
        ('ICP NotificationMail：Smtp／各類 MailTo、CcTo、Enabled、SendTimes','通知流程多處讀 IOptionsMonitor；新一次讀取可取新值。既有排程快照及寄送紀錄不保證隨設定回寫；實例見 [[12.1]]。','NotificationSmtpChecks 核對 SMTP 與 fallback；ArrivalNoticeScheduleChecks 核對排程。不要用真實名單驗證。'),
        ('ICP Integration：RabbitMq、Outbox','Publisher／Outbox Worker 讀 IOptionsMonitor；實際 Broker 連線與既有事件需分開核對。不因設定更新重設全部 Outbox。','核對 Options 與迴圈取值；使用隔離事件與既有測試。回排界線見 [[26.4]]。'),
        ('Hub Integration：RabbitMq Enabled／Queue／綁定','Enabled 控制啟動時的 MassTransit 註冊，連線與綁定隨 Bus 建立；此類變更需重新啟動 Hub。不代表歷史訊息會自動重播。','檢查註冊來源、啟動 Log 與測試組態；本版不實際變更運行中的 Broker。'),
        ('ICPFileGenerator FileGenerator：輪詢、WorkerId、OutputDirectory、Hub URL','Worker 與 HTTP Client 使用 IOptions；建議重啟後核對生效。新路徑不會搬移既有輸出檔，也不會改寫舊 Job。','FileGenerationChecks 核對本地產出；Worker 啟動與 Job 排查見 [[13.2]]、[[21.2]]。'),
        ('各服務 ConnectionStrings／身分與權限設定','依 DI、啟動讀取及 Session 使用處理；統一安排重啟／重新登入核對。切換連線不會搬資料，舊 Session 不代表新授權結果。','用隔離設定確認選檔與映射，不輸出密碼；權限與 Session 見 [[9.2]]。'),
        ('Config/*table-fields*.json、Views、wwwroot/js／css','不同 Provider 的監聽與載入方式不同；更新後重新取 metadata／重整頁面，必要時重啟。可見欄位變更不會新增 DB 欄。','相關 Report／Filter checks；核對實際 JSON、欄位順序與空值，必要時對照 Network 與快取。'),
        ('Sidebar.CustomLinks、wwwroot/docs、Files 範本','連結與實體檔要同時更新；以版本參數避開舊快取，依選單載入方式重整／重新登入。舊檔不會自動重命名。','確認 URL 及版本；下載檔比對 SHA-256、範本表頭、目錄連結及頁碼。')
    ],[2.0,2.7,2.3])
    p('變更完成判斷採本地設定與隔離測試證據；資料庫還原、SMTP 投遞、運行中服務回收等維運動作不因文件範例自動執行。每次異動的記錄格式見 [[18.3]]。',kind='維運建議')

def change_matrix(table,p,h):
    h('17.2 修改範圍對照').paragraph_format.page_break_before=True
    p('以下列出相依入口與本地測試候選，不表示每次都要修改全部檔案。新增欄位先判斷 Header／Detail 與資料來源；實際交付仍逐檔列清單，格式見 [[26.2]]。',kind='維運建議')
    table(['改修類型','同步檔案或層次','本地測試與文件'],[
        ('新增 Detail 欄位','Models/Icp/IcpDetail.cs、ShipInfoDetailRowDto、ShipInfoFieldCatalog、ShipInfoRowDtoMapper；需要時含 sql、匯入 Map／Rules／Service、Config、Resources、Files。','依資料鏈測試匯入、保存、再查與輸出；WBS 現有實例見 [[23]]。'),
        ('變更欄位 Filter／數字顯示','Config 實際與 example、MetadataProvider、QueryModel、Repository 查詢、pro-datatable／pro-table-filters；必要時含 View 與資源。','ShippingReportChecks、MassDataReportChecks、TariffFilterChecks 或 ForwarderHistoryFilterChecks；查詢與匯出一致。'),
        ('變更 Excel 上傳規則','對應 Controller、ColumnMap、ImportRules、ImportService；需要時含 DTO、範本、csproj 發布規則與錯誤顯示。','AddDiSaImportChecks、ForwarderUploadChecks、TariffUploadChecks；Settings 用 SettingsExcelChecks，覆蓋失敗保留案例。'),
        ('新增通知或修改排程','NotificationMailOptions、Sender／內容組裝、觸發 Service／Worker、DI、Schedule／Log 模型與 SQL、設定範例及 Resources。','NotificationSmtpChecks、ArrivalNoticeScheduleChecks、DeliveryDelayMailChecks；分開核對名單、時區、取消與重複風險。'),
        ('調整現有通知名單','該環境實際載入的 NotificationMail 對應種類；Arrival To 另查 Ship Info 資料。','不改不相干種類；依 [[12.1]] 與 [[20.4]] 核對生效與快照，保留名單差異紀錄。'),
        ('新增整合事件或改產檔','Contracts／Payload、Publisher／Consumer 註冊、專用下游寫入、Outbox／Log、完成回寫；產檔含 Reader／Generator／Worker／Job。','依入口核對識別、成功／失敗與重複保護；FileGenerationChecks 需範例路徑及暫存輸出參數，見 [[16]]。')
    ],[1.45,3.15,2.4])

def maintenance_record(table,p,h):
    h('18.3 維護紀錄固定格式').paragraph_format.page_break_before=True
    p('每次修改建立一份紀錄；所有項目用「已完成／不適用／未執行及原因」填寫，不留空白造成誤判。以下為維運建議格式，不是新增系統資料表或自動追蹤功能。',kind='維運建議')
    table(['紀錄欄位','必須記載的內容'],[
        ('識別與目的','需求／BUG 編號、日期與時區、修改者或負責角色、問題與修改後行為。'),
        ('版本與檔案','基準及修改後 commit／版本；依服務列資料夾與檔案；原始碼、publish、Config、SQL、範本及文件分開。'),
        ('資料與設定影響','新增或修改 schema、回填與相容性；有效設定來源、生效方式、既有排程／檔案影響。秘密值不貼進紀錄。'),
        ('本地測試證據','專案、完整指令、組態、輸入／暫存輸出、Exit Code、項數與 Log 路徑；初次失敗及後續修正均保留。'),
        ('文件與下載檔','手冊版本、修改章節、實際下載檔、系統連結與版本參數；雜湊、目錄及頁碼核對。'),
        ('回復方式','前版程式與設定位置、schema 相容性、需保留的新資料／檔案、未完成工作與避免重複的處理。'),
        ('完成範圍','明列本地已完成項目、維運參考程序及既有尚未實作功能。部署是否執行另記，不設正式環境驗收表。')
    ],[1.6,5.4])
    p('本次填寫示例：手冊維護 v2.4；僅修改文件來源、下載副本與連結版本；業務程式、SQL、SMTP 與 MailTo／CcTo 不變。沿用 2026-10-27 的 9 組本地測試紀錄；本次另執行文件內容、內部連結、頁碼、版面及下載副本雜湊核對。這些文件檢查不計入 683 項業務檢查。',kind='維運建議')
    p('完整流程的維護位置：通知名單在 [[12.1]]、IIS 喚醒在 [[13.2]]、產檔卡住在 [[21.2]]、設定修改影響在 [[20.4]]、重送界線在 [[26.4]]。其他章節只保留摘要與連結；修改主題時同步核對快速索引。',kind='維運建議')

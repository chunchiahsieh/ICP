"""Illustrative uploads, exact error references, and advice separated from checks."""
import xml.etree.ElementTree as ET


def append_examples(doc, table, bullet, root):
    doc.add_page_break()
    doc.add_heading('13 上傳案例與錯誤修正',level=1)
    doc.add_paragraph('以下成功／失敗案例是教學情境，用來對照卡控與預期結果；除註明既有畫面外，不代表本次已對系統實際上傳或寫入。正式操作前，請先用該頁下載的範本，並確認既有資料與 Settings。')
    cases=[
        ('13.1 Add DI/SA','新版範本第一頁 Upload Data；一個尚未存在的 Invoice，有兩列 Seq 1、2，33 個必填、格式與 Settings 都通過，只有允許的明細欄位不同。','預覽兩列可上傳，按 Save 後整批建立表頭與明細。','同一 Invoice 兩列 Seq 都是 1，或非白名單 TET_PO 不同。','整檔拒絕；依訊息改正 Seq 或讓 TET_PO 一致後重新上傳。'),
        ('13.2 Mass Update 非 NCPI','用本頁範本；Invoice No 存在且檔內不重複；13 個更新欄位先填妥要保留的新值。','預覽比對到 Header；Save 後更新該 Invoice 對應的表頭。','Invoice No 在 ICP_HEADER 不存在。','儲存被拒絕；核對完整 Invoice、前導零及是否匯入過，不能用 Mass Update 新增 Invoice。'),
        ('13.3 Mass Update NCPI','使用 NCPI 頁下載的範本；每個 Invoice 只一列，且所有 Invoice 可比對到既有 Header。','核對預覽與影響筆數後 Save，完成批次更新。','相同 Invoice 在 Excel 出現兩列。','系統拒絕重複 Invoice；整併成一列後重傳。'),
        ('13.4 Forwarder Data Upload','第一頁 Upload Data，18 個必填都有值；Quantity=5，日期=2026/10/26，C/B 使用目前 Broker Key，同 Invoice 的運送欄位一致。','Upload Check 通過並顯示日期 2026-10-26；Save 後列入 Upload History。','C/B 填成 Broker 的顯示名稱，而非該筆 Key。','驗證失敗；至 Broker 查看 Key 後修正，重新上傳，不修改其他 Settings 來遷就錯值。'),
        ('13.5 Customs Data','使用新版範本；Invoice Number 檔內唯一，Shipper 與 Air/Sea 使用 Settings Value，Declaration Amount(TWD)=12345；CIF 可留空。','驗證通過後新增；若 Invoice 已存在，先取得覆蓋確認。','Declaration Amount(TWD)=ABC，或同檔相同 Invoice Number 只有 Line No 不同。','數字錯誤或重複 Invoice 阻擋整檔；修正值或重複資料後重傳。'),
        ('13.6 Declaration PDF','假設可見 HAWB 為 HAWB001，檔名 HAWB001.pdf 或 HAWB001.tif，且大小在上限內。','上傳後同 HAWB 對應資料可從 Declaration PDF 欄開啟。','上傳 HAWB001.xlsx 或將檔名寫成報單號。','檔案類型不符或找不到 HAWB；改用允許類型並以實際 HAWB 命名。'),
        ('13.7 Import Doc','可見 HAWB=HAWB001，使用 HAWB001.pdf／.xls／.xlsx。','從 Import Doc 欄開啟已上傳提單。','使用 HAWB001.tif。','Import Doc 不允許 tif；另備妥允許格式，不把副檔名改掉冒充轉檔。'),
        ('13.8 Cost','使用現有可見 HAWB 命名的 .xls 或 .xlsx，大小在設定上限。','附件以 HAWB 關聯，可於 Cost 檔案入口檢視或下載。','使用 HAWB001.pdf。','Cost 只接受 Excel；選擇正確檔案與上傳類型。'),
        ('13.9 Settings Excel','從目前頁下載；保留十欄順序與該頁 Category，Key1 非空、不重複，其他格式與長度通過。','確認後用本檔全部資料取代該頁有效資料；原檔漏列的舊資料不再有效。','Key1 空白、公式、錯誤 Category 或同組鍵重複。','整檔失敗且原資料保留；依錯誤列修正後重傳。'),
        ('13.10 Ship Info 附件','在可編輯且有權限的 Header 上，選擇允許副檔名、非空且未超過附件大小設定的檔案。','上傳完成後附件列表可見該檔，核對名稱與下載。','零位元檔案，或大小超過 ArurAttachment 設定。','顯示 Uploaded file is empty. 或 File exceeds …MB.；重新選擇有效檔案。'),
        ('13.11 Languages Excel','從 Languages 匯出目前資源 Excel，保留五欄標題及既有 Resource Key，只修改翻譯文字。','匯入合法更新後，重新載入畫面核對翻譯。','新增系統不存在的 Resource Key 或同一鍵出現兩列。','Unknown resource key 或 Duplicate resource key；修正鍵值與重複列，不把匯入當作任意新增資源功能。'),
        ('13.12 Export','上傳符合 Shipping advice 規格的 Excel，Hub 與 FileGenerator 可用。既有畫面已有 Completed 案例。','Pending／Processing 後為 Completed；按「檢視檔案」，逐檔下載 PickupNotice Excel、Case Mark PDF，或全部下載 ZIP。','Hub 連線失敗；既有畫面顯示 Hub notify failed: 無法連線，因為目標電腦拒絕連線。','狀態 Failed；保留 Request ID 與錯誤，請管理者檢查 Hub 服務及 URL，勿連續重複上傳。')
    ]
    for heading,success,expected,failure,handling in cases:
        doc.add_heading(heading,level=2)
        table(['情境','輸入與結果'], [('成功案例',success),('成功結果',expected),('失敗案例',failure),('修正方式',handling)], [1.1,5.9])
    doc.add_heading('13.13 常見實際訊息對照',level=2)
    doc.add_paragraph('英文或中文會隨訊息來源與語言設定不同。{列}、{欄位}、{值} 代表訊息中的變動內容；本表保留實際訊息格式，不把教學範例寫成實測紀錄。')
    errors=[
        ('Receiver is required for Ship Plan creation.','ARUR 收貨人空白；本次預覽實際出現。','取消起案預覽，在 Header 編輯補收貨人，儲存後重開預覽。'),
        ('第 {列} 列缺少必填欄位 {欄位}','Add DI/SA 必填缺值。','回 Excel 該列補值；不是只補標題。'),
        ('第 {列} 列 {欄位} 須為有效數字','Add DI/SA 數值格式錯誤。','移除文字單位、不可解析符號，保留有效數字。'),
        ('INVOICE_NO＋INVOICE_SEQ 重複：{值}','同 Invoice 的 Seq 重複。','01 與 1 視為相同數字；使用正確且不重複的 Seq。'),
        ('第 {列} 列 DELIVERY_TO「{值}」不存在於 Settings → DeliveryToList 的 Customer Code','DI/SA 交貨對象比對錯誤。','使用 Customer Code，不填 Key 或 Name。'),
        ('第 {列} 列 C/B「{值}」不存在於 Settings → Broker 的 Key。','Forwarder 找不到有效 Broker Key。','核對 Broker Key 及是否已刪除。'),
        ('Invoice {值} 多筆明細的 MAWB/HAWB/FLT1#/FLT2#/ETA 須一致，請修正後重新上傳','Forwarder 同 Invoice 運送資訊不一致。','檢查包含選填 FLIGHT 2# 在內的五個欄位，統一後重傳。'),
        ('第一個分頁名稱必須為 Upload Data，請使用新版 Template','Customs 首頁工作表名稱錯誤。','不只改其中一個分頁，還要把 Upload Data 放第一頁。'),
        ('檔案內 Invoice Number 重複：{值}','Customs 以整個 Invoice Number 檢查重複。','Line No 不同仍不能當成不同 Invoice。'),
        ('Excel 內 Invoice No 重複：{值}','Mass Update 重複更新目標。','一個 Invoice 保留一列，核對所有更新欄後重傳。'),
        ('ICP_HEADER 查無 Invoice No：{值}','Mass Update 找不到原資料。','檢查前導零與 Invoice 是否存在；不會自動建立。'),
        ('Excel 欄位或順序不符，請先下載此 Settings 頁面的 Excel 作為範本。','Settings 標題或順序變動。','重下載該頁 Excel，把資料貼回原十欄結構。'),
        ('Excel 沒有資料，原設定不會被清空。','Settings 空檔。','不得以空檔清空；填寫有效資料後再上傳。'),
        ('第 {列} 列 {欄位} 不可使用公式，請貼上純值。','Settings 含公式。','先計算，複製後貼上值。'),
        ('重複鍵（Category / FunctionCode / Key1 / Key2），Excel 列：{值}','Settings 組合鍵重複。','依列號比較四個欄位，修正重複；大小寫差異不算不同。'),
        ('The Excel file does not use the ICP localization export format.','Languages 格式不符。','使用 Languages 原匯出檔，保留五欄標題。'),
        ('Unknown resource key: {值}','Languages 未知資源鍵。','改用現有鍵；如確需新鍵，交管理者依程式流程新增。'),
        ('File not found／檔案不存在','歷史紀錄的實體原檔遺失；本次歷史頁實際顯示。','交管理者檢查保存路徑；不代表該批當初未匯入。'),
        ('Hub notify failed: …','Export 通知 Hub 失敗；本次畫面有既有失敗紀錄。','保留 Request ID 交管理者檢查連線；不要自行改收件者或 Settings。')
    ]
    translations={e.attrib.get('name'):e.findtext('value','') for e in ET.parse(root/'ICP/Resources/SharedResource.zh-TW.resx').getroot().findall('data')}
    for key,why,fix in [('Broker.TariffData.InvalidFileType','Customs 附件類型不符。','報單允許 pdf/tif，Import Doc 允許 pdf/xls/xlsx，Cost 允許 xls/xlsx。'),('Broker.TariffData.HawbNotFound','附件檔名對應不到可見 HAWB。','檢查檔名主體與 HAWB 完全對應，以及資料可見權限。')]:
        if translations.get(key): errors.append((translations[key],why,fix))
    table(['訊息','原因','修正方式'],errors,[2.7,1.5,2.8])
    doc.add_heading('Mass Update 更新範圍',level=3)
    doc.add_paragraph('兩個 Mass Update 頁使用共同更新服務。Save 依 Invoice 對應 Header；同一 Invoice 若有多筆 Header，可能全部被更新。可更新 13 欄：ArrivalNotice、SaDate、Forwarder、Broker、Shipper、Eta、Mawb、Hawb、Flt、DeliveryDate、MdpFlag、ReasonForDeliveryDelay、DelayNotificationDate。空值也可能覆蓋原值，不是「空白就略過」。NcdrNo、Owner、EndUser 雖可供預覽對照，不屬於目前這 13 個寫入欄位。提交前務必核對整份資料，不假設按 NCPI 頁名稱就自動縮小 Invoice 範圍。')
    doc.add_paragraph('此流程目前只要求 Invoice No 必填並對應原資料，不可把 Add DI/SA 的工作表名稱、33 欄必填或其他 Settings 檢查一律套用到 Mass Update。')

    doc.add_page_break()
    doc.add_heading('14 填寫建議與系統卡控的差別',level=1)
    doc.add_paragraph('「系統卡控」是目前程式會檢查或拒絕的條件；「填寫建議」是為維持業務資料品質的建議。未被系統擋下不代表業務內容正確。')
    table(['功能／欄位','系統會檢查','填寫建議'],[
        ('Forwarder Type','必填、長度；未限制只能 EMG／NML。','依業務使用 EMG／NML，不新增自行命名類型。'),
        ('Forwarder MDP','選填、長度；此匯入未限制只能 Y／空白。','依範本約定填 Y 或空白；別與 DI/SA 的 MDP_FLAG 嚴格限制混用。'),
        ('Forwarder Freight charge','文字必填，非強制數字。','依貨代內容填寫；不要自行把運費條件文字轉成數字。'),
        ('Customs 重量與稅率等文字欄','必填／長度依卡控表，非所有欄都有數字解析。','重量與金額保持合法數值及單位規格，避免文字混入。'),
        ('Customs 顯示小數','Exchange Rate 4 位、Net Weight 3 位、Unit Value 2 位只是顯示。','保留原始精度，不因畫面顯示位數自行截斷 Excel。'),
        ('同 Invoice 的資料','只有指定欄位能不同，其他須一致。','同批資料先按 Invoice 排序檢查；確認不是把兩張發票混用一個號碼。'),
        ('編號與長數字','依各流程型別及長度檢查，不能修復 Excel 已遺失的前導零。','Invoice、HAWB、料號等先設為文字，避免科學記號及數字精度損失。'),
        ('Settings 上傳','整批取代而非合併；缺列的舊資料將失效。','先下載保留備份，核對完整清單及影響範圍，再上傳。'),
        ('日期','不同操作格式規則不同，依第 12 章。','統一填 yyyy-MM-dd；不要假設所有頁面都能接受 20260912。'),
        ('附件','類型、大小、名稱、HAWB 及權限檢查依功能而異。','檢查檔案內容與檔名一致；改副檔名不等於轉換格式。'),
        ('失敗後重試','預覽成功、排程成功、檔案 Completed 含義不同。','先確認狀態及 Request ID，再重試；避免重複上傳或起案。')
    ],[1.6,2.7,2.7])

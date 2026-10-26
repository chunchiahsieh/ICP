"""Generate explicit user-facing validation tables from checked source rules."""
import re


def read(root, relative):
    return (root / relative).read_text(encoding='utf-8-sig')


def quoted_block(source, name):
    return re.findall(r'"([^"]+)"', re.search(re.escape(name) + r'[^=]*=\s*(?:new[^\n]*\s*)?[\[{]([\s\S]*?)[\]}];', source).group(1))


def entity_properties(source):
    constants = dict(re.findall(r'const int (\w+)\s*=\s*(\d+)', source))
    result = {}
    for match in re.finditer(r'((?:\s*\[[^\]]+\])*)\s*public\s+([\w?]+)\s+(\w+)\s*\{', source):
        attrs, kind, name = match.groups()
        maximum = re.search(r'MaxLength\((\w+)\)', attrs)
        maximum = (constants.get(maximum.group(1), maximum.group(1)) if maximum else '')
        result[name.lower()] = (name, kind.rstrip('?'), maximum)
    return result


def append_rules(doc, table, bullet, root):
    doc.add_page_break()
    doc.add_heading('12 卡控條件與處理邏輯', level=1)
    doc.add_paragraph('本章列出目前系統實際執行的規則。上傳、清單顯示與畫面編輯可能使用不同檢查，不可將畫面顯示小數位視為上傳限制。表中「選填」代表可留空，有填值仍須符合格式及長度。')
    doc.add_heading('12.1 Add DI/SA 檔案與儲存邏輯', level=2)
    table(['檢查項目', '通過條件與失敗處理'], [
        ('檔案與工作表', '使用 Download Sample 的 .xlsx／.xls，單檔最大 50 MB；第一個分頁必須為 Upload Data。第一列標題、第二列起資料；空白資料列跳過，整份沒有資料時拒絕。'),
        ('標題', '必填欄位的標題必須存在；重複、無法識別或系統管理欄位會拒絕。依新版範本保留全部欄位，不自行換名稱。'),
        ('日期', 'SA_DATE、ETD、ETA、INVOICE_DATE 必填；DELIVERY_DATE 選填。文字只接受 yyyy-MM-dd／yyyy/MM/dd，必須為有效日期；日期型 Excel 儲存格可辨識。20260912 文字不接受。'),
        ('數字', '下表列為數字的欄位須可解析；整數欄位不可輸入小數。選填數字有填值仍檢查。此匯入流程沒有對所有數字統一限制正數或相同小數位，請勿混用 Ship Info 編輯規則。'),
        ('同檔重複', 'INVOICE_NO + INVOICE_SEQ 不可重複；Seq 以數字比較，01 與 1 視為同一值。任一重複即拒絕，不跳過重複列。'),
        ('系統已存在 Invoice', 'ICP 已存在相同 INVOICE_NO 時不接受新匯入；即使改成不同 TET_PO 仍不可藉此重複新增。'),
        ('同 Invoice 一致性', '同一 Invoice 的非明細差異白名單欄位必須一致；日期與數字先正規化後比對，文字比較有區分大小寫。只要有不一致，整檔失敗。'),
        ('Settings', '只接受未刪除的設定值；Add DI/SA 比對 Key，只有 DELIVERY_TO 比對 Customer Code。值前後空白會修整，比對不區分大小寫。'),
        ('驗證與 Save', '先解析、格式與一致性檢查，再檢查設定與既有 Invoice。預覽通過不代表已寫入。按 Save 時再次驗證，在同一交易儲存；錯誤時不允許部分資料成功。'),
        ('預設值', '選填 Forklift／WasteDisposal／DriverDetails 留空時，匯入後預設 N；MovingLabor 留空預設 0人。這些欄位不是 Excel 必填。'),
        ('系統管理欄位', 'CREATE_DATE、RT_NO、ARRIVAL_NOTICE_FLAG、ARRIVAL_NOTICE、REASON_FOR_DELIVERY_DELAY、DELAY_NOTIFICATION_DATE、DEPOSIT、NOTES、CANCELLATION、REASON_FOR_CANCELLATION、ATTACHED_FILE 不可放回上傳範本。')
    ], [1.5, 5.5])
    rules = read(root, 'ICP/Helpers/AddDiSaImportRules.cs')
    required = set(quoted_block(rules, 'RequiredProperties'))
    variable = set(quoted_block(rules, 'VariableInvoiceFields'))
    mapping = read(root, 'ICP/Helpers/AddDiSaExcelColumnMap.cs')
    columns = ''.join(re.findall(r'"([^"]+)"', re.search(r'Array.AsReadOnly\(\s*\(([\s\S]*?)\)\.Split', mapping).group(1))).split()
    detail = set(quoted_block(mapping, 'DetailProperties'))
    props_header = entity_properties(read(root, 'ICP/Models/Icp/IcpHeader.cs'))
    props_detail = entity_properties(read(root, 'ICP/Models/Icp/IcpDetail.cs'))
    dates = {'SaDate','Etd','Eta','InvoiceDate','DeliveryDate'}
    special = {'Broker':'Broker Key', 'Shipper':'Shipper Key','Freight':'Air/Sea Key','Warehouse':'Warehouse Key','InvoiceType':'Invoice Type Key','OrderType':'Order Type Key','DeliveryTo':'Customer Code','Bu':'BU Key','MdpFlag':'僅 Y／空白'}
    rows = []
    for col in columns:
        normalized = col.replace('_','').lower()
        candidates = props_detail if any(p.lower() == normalized for p in detail) else props_header
        name, kind, maximum = candidates[normalized]
        rule = ('日期' if name in dates else '整數' if kind == 'int' else '數字' if kind in {'double','decimal'} else '文字')
        if maximum:
            rule += '；最多 ' + maximum + ' 字'
        if name in special:
            rule += '；' + special[name]
        rows.append((col, '必填' if name in required else '選填', rule, '可不同' if name in variable else '須一致'))
    assert len(rows) == 73
    doc.add_heading('Add DI/SA 全部 73 欄規格', level=3)
    table(['上傳欄位','必填','型別 長度與比對','同 Invoice'], rows, [2.0,.55,3.65,.8])
    bullet('例如：同 Invoice 的兩列 TET_PO 不同會拒絕；ITEM_NO 不同則允許。TET_PO_LINE 是選填，但不在差異白名單，仍須一致。WBS_ELEMENT 選填、最多 30 字，可逐明細不同。')

    doc.add_heading('12.2 Forwarder Data Upload 卡控與覆蓋', level=2)
    table(['檢查項目','通過條件與處理'], [
        ('Excel 工作表','第一個分頁 Upload Data，第一列標題，第二列起資料；不會改讀填寫範例。保留既有 CSV 支援，但 CSV 沒有分頁設定。'),
        ('必填與標題','18 個必填欄位的標題及內容都要存在。只有 Customer Reference、FLIGHT 2#、Action、MDP 選填。重複識別標題會拒絕。'),
        ('日期','Confirmed Custom Date／ETD／ETA 文字接受 yyyy/MM/dd、yyyy-MM-dd。有效 Excel 日期型儲存格及日期欄 Excel 序號可辨識；一般文字數字不當作日期。畫面顯示 yyyy-MM-dd。'),
        ('同 Invoice','同檔同一 Invoice 的 MAWB、HAWB、FLIGHT 1#、FLIGHT 2#、ETA 必須一致。文字前後空白修整、不分大小寫；日期比對日期部分。'),
        ('Broker','C/B 對應 Settings → Broker 的 Key，且設定不能已刪除；非 Value 或顯示名稱。'),
        ('既有 Invoice 覆蓋','儲存前檢查相同 Invoice；未確認時不寫入。確認後，把該 Invoice 原有各列封存，再以本次該 Invoice 各列取代，不是只覆蓋相同 Material 的單列。'),
        ('整批交易','Save 會重新解析並驗證。封存、刪除舊列及寫入新列在同一交易，失敗回復。預覽、失敗或未 Save 的檔案不列成功歷史。'),
        ('紀錄與下載','歷史由目前與封存資料組成；被覆蓋的批次仍可查。原檔存在且對應已儲存紀錄才可下載；File not found 不等於匯入失敗。'),
        ('範例不是列舉卡控','Type 範例 EMG／NML、MDP 範例 Y／空白不代表此上傳已限制只能這些值；Freight charge 目前是文字必填，不限制只能數字。')
    ], [1.5,5.5])
    fsource = read(root,'ICP/Helpers/ForwarderImportRules.cs')
    required_f = dict(re.findall(r'\["(\w+)"\]\s*=\s*"([^"]+)"', fsource.split('public static void')[0]))
    fp = entity_properties(read(root,'ICP/Models/Icp/ForwarderDataUpload.cs'))
    ordered = [('Type','Type'),('InvoiceNo','Invoice No.'),('CustomerReference','Customer Reference'),('MaterialCode','Material Code'),('OrderMaterialName','Order Material Name'),('Quantity','Quantity'),('PortOfLoading','Port of Loading'),('ShipToName','Ship to Name'),('ShipToAddress','Ship to Address'),('ShipToPartyCountryCode','Ship to Party Country Code'),('ShipToPortCode','Ship to Port Code'),('FreightCharge','Freight charge'),('ConfirmedCustomDate','Confirmed Custom Date'),('Hawb','HAWB'),('Mawb','MAWB'),('Etd','ETD'),('Eta','ETA'),('Flight1','FLIGHT 1#'),('Flight2','FLIGHT 2#'),('Cb','C/B'),('Action','Action'),('Mdp','MDP')]
    frows=[]
    for prop,label in ordered:
        _,kind,maximum=fp[prop.lower()]
        rule = '日期' if kind=='DateTime' else '數字' if kind=='decimal' else '文字'
        if maximum: rule+='；最多 '+maximum+' 字'
        if prop=='Cb': rule+='；Broker Key'
        frows.append((label,'必填' if prop in required_f else '選填',rule))
    table(['上傳欄位','必填','格式與長度'], frows,[2.7,.7,3.6])

    doc.add_heading('12.3 Customs Clearance Data 卡控與金額',level=2)
    table(['檢查項目','通過條件與處理'],[
        ('檔案','支援 xlsx／xls；第一個分頁限定 Upload Data，第二列起資料。檔內沒有可匯入資料時拒絕。'),
        ('標題與內容','Invoice Number、MAWB、HAWB、Broker、Air/Sea、Declaration Amount(TWD) 六個標題必須存在；其他每列必填欄位仍會檢查空值。重複識別標題拒絕；請直接用新版完整範本。'),
        ('HAWB','HAWB 標題要保留，內容可空；空白時以 MAWB 代入 HAWB。這與 Forwarder 的 HAWB 必填不同。'),
        ('設定比對','Shipper 對應 Settings Shipper 的 Value；Air/Sea 對應 AirSea 的 Value，兩者皆修整前後空白且區分大小寫。此流程不能沿用 Add DI/SA 的 Key 規則。'),
        ('COO 與 Broker','COO 是必填；Country of Origin 欄位篩選取自 Settings。Broker 必填，但本匯入流程不做 Forwarder C/B 那種 Broker Key 存在檢查。'),
        ('日期','Import Date／Declaration Date／Release Date 必填並驗證有效日期。使用 yyyy-MM-dd 或 yyyy/MM/dd；程式亦辨識 yyyy/M/d 及 Excel 日期序號，顯示 yyyy-MM-dd。'),
        ('數字檢查','Quantity、Unit Value、Exchange Rate、Declaration Amount(TWD) 必填且解析為數字。其他文字儲存的重量、稅率等，不可推論都有相同數字解析檢查。'),
        ('同檔 Invoice','整個 Invoice Number 必須唯一，大小寫不區分；不是以 Invoice + Line No 為重複鍵。同檔相同 Invoice 不會因 Line No 不同而放行。'),
        ('既有 Invoice','出現覆蓋確認；取消不寫入。確認後更新相同 Invoice 的資料，其他 Invoice 不受影響；LOG Remarks 維持既有人工維護內容。'),
        ('重新確認','確認依使用者、上傳檔內容與當時資料狀態驗證，15 分鐘到期或資料／檔案變動需重新確認，不能直接沿用舊確認。'),
        ('計算金額','Total Amount(Foreign Currency) = Unit Value × Quantity；Total Amount(TWD) = 前者 × Exchange Rate。Declaration Amount(TWD) 為另行上傳的報單金額，不用計算總額自動代替。'),
        ('整批寫入','全部驗證及覆蓋確認通過後同一交易新增／更新，任一失敗回復；附件依 HAWB 重新關聯。'),
        ('顯示精度','Exchange Rate 顯示 4 位、Net Weight 3 位、Unit Value 2 位；超過顯示小數位並非因此拒絕上傳。')
    ],[1.5,5.5])
    ts=read(root,'ICP/Helpers/TariffCustomsImportRules.cs')
    tr=set(re.findall(r'TariffExcelColumnMap\.(\w+)',re.search(r'RequiredCellColumns[^=]*=[^{]*\{([\s\S]*?)\};',ts).group(1)))
    tm=read(root,'ICP/Helpers/TariffExcelColumnMap.cs')
    aliases={}
    for label,prop in re.findall(r'\["([^"]+)"\]\s*=\s*(\w+)',tm): aliases.setdefault(prop,label)
    lengths=re.findall(r'\[TariffExcelColumnMap\.(\w+)\]\s*=\s*(\w+(?:\.\w+)?)',re.search(r'MaxLengths[^=]*=[^{]*\{([\s\S]*?)\};',ts).group(1))
    descmax=re.search(r'DescriptionOfGoodsMaxLength\s*=\s*(\d+)',read(root,'ICP/Models/Icp/TariffData.cs')).group(1)
    trows=[]
    for prop,maximum in lengths:
        maximum=descmax if maximum=='TariffData.DescriptionOfGoodsMaxLength' else maximum
        mode='必填' if prop in tr else '選填'
        rule='文字；最多 '+maximum+' 字'
        if prop in {'Quantity','UnitValue','ExchangeRate','DeclarationAmountTWD'}: rule='數字；最多 '+maximum+' 字'
        if prop=='HAWB':rule+='；空白使用 MAWB'
        if prop in {'AirSea','Shipper'}:rule+='；Settings Value'
        trows.append((aliases.get(prop,prop),mode,rule))
    for prop in ['ImportDate','DeclarationDate','ReleaseDate']:
        trows.append((aliases.get(prop,prop),'必填','有效日期'))
    table(['上傳欄位','必填','目前檢查'],trows,[2.5,.7,3.8])
    bullet('CIF Value 選填，沒有要求必填；目前選填的 CIF／Freight Charge／Total Pieces／Gross Weight 以文字與長度檢查，勿寫成已全面限制數字。填寫資料仍應依業務規格保持正確。')

    doc.add_heading('12.4 Settings Excel 取代與權限',level=2)
    table(['檢查項目','通過條件與失敗處理'],[
        ('下載與上傳權限','可查看該頁即能下載；具有該頁 Create 權限才可上傳。下載該頁全部有效設定，不受清單目前 Filter 限制。'),
        ('檔案與列數','只能上傳非空的 .xlsx，最大 50 MB；取第一個分頁；資料區最多 50000 列。'),
        ('標題順序','Category、FunctionCode、Key1、Key2、Value1、Value2、Value3、Value4、Value5、Value6，十欄名稱與順序須完全符合。'),
        ('內容','Key1 必填，最多 100 字；Key2 最多 100；Category／FunctionCode 最多 50；Value1 至 Value6 各最多 1000。不得有公式或超出十欄的資料。'),
        ('各頁邊界','Category 必須屬於目前頁面；Customized 也受其範圍限制。不能用本頁上傳替換另一設定分類。'),
        ('重複鍵','Category + FunctionCode + Key1 + Key2 組合不可重複；比對不區分大小寫。'),
        ('分類檢查','一般設定頁的 Category 必須完全符合該頁分類，含大小寫；Country of Origin 的 Category 為 Country of Origin，含空格。Customized 的 Category 必填且不可使用一般設定頁分類。'),
        ('整批取代','全檔通過才將該頁舊有效資料停用並新增本檔全部資料，交易一併完成；不是合併更新、不是追加，檔案漏列的舊資料不再有效。'),
        ('空檔及失敗','無資料不允許用來清空整頁；任一列或資料庫儲存失敗，原有效資料保留。錯誤訊息最多列出前 30 項，修正後重傳可能再顯示其他錯誤。')
    ],[1.5,5.5])
    bullet('例：原設定有 A、B、C，本次檔案只有 A、B；成功取代後只有 A、B 有效。若 B 有錯，A、B、C 全部維持原狀。')
    table(['Settings 頁面','Excel Category'], [('BU Codes','BuCode'),('Warehouse Codes','WhCode'),('Delivery To List','DeliveryToList'),('Pick Up Location','PickUpLocation'),('ETA Delivery Date Table','EtaDelDateTable'),('Default Delivery Warehouse','DefaultDeliveryWh'),('Order Type','OrderType'),('Air/Sea','AirSea'),('Broker','Broker'),('Invoice Type','InvoiceType'),('Order Priority','OrderPriority'),('Shipper','Shipper'),('Country of Origin','Country of Origin'),('Customized','該頁允許的自訂分類；不可使用上列分類')],[3.0,4.0])

    doc.add_heading('12.5 Ship Info 編輯與起案',level=2)
    table(['操作','卡控與處理'],[
        ('表頭編輯','Reason For Delivery Delay 與 Delay Notification Date 必須同時填或同時空白。Total Cartons 有填時須為非負整數。欄位另依畫面設定檢查必填、長度及可編輯權限。'),
        ('明細編輯','Invoice Seq 有填須為大於 0 的整數；Qty 必填非負且最多 3 位小數、Price／Amount 必填非負最多 2 位、Rate 有填非負最多 4 位、Net Weight 有填非負最多 3 位。Carton No／Length／Width／Hight／Gross Weight 依目前編輯規則必填且須為非負整數。此處不是 Excel 匯入規則。'),
        ('起案共通','需具有對應 Deposit／ARUR 權限且資料在可操作範圍。取消資料不得起案；案件處理中不可重複起案；Invoice No 不可空白。整合 RabbitMQ 未啟用時提交被拒絕。'),
        ('Deposit','另需 MAWB、HAWB、FLT；不使用 ARUR 的收件人／搬運欄位必填規則。'),
        ('ARUR 必填','Receiver、Forklift、MovingLabor、WasteDisposal、DriverDetails 不可空白；Warehouse 最多 4 字。'),
        ('ARUR 長度','預覽前端另限制 TET PO 最多 30 字；主旨「AR + 空格 + TET PO + 空格 + Invoice No」最多 50 字，超過不可提交。匯入 TET_PO 可接受的長度不代表一定可起案。'),
        ('ARUR 附件','沒有附件不因此阻擋；已有附件須實際存在且路徑有效，組合 UNC 路徑字串最多 1000 字。遺失附件先修正，再重新預覽。'),
        ('狀態','按確認送出後先為 Processing，需 Hub 在目的系統完成並回覆後才成為 Initiated；不能把 Processing 當作已完成。錯誤時依狀態與訊息處理，不連續點擊建立重複案件。'),
        ('黃底','只代表預覽重點欄位，不作為必填、可編輯或已送出的判斷。')
    ],[1.5,5.5])

    doc.add_heading('12.6 篩選與結果判斷',level=2)
    table(['情境','實際邏輯與例子'],[
        ('多行精確文字','指定支援欄位每行一個值，修整空白與去除重複後作精確 OR；輸入 1、換行、2 不應匹配 10 或 21。未標示多行的 Text 仍用一般文字條件。'),
        ('不同欄條件','同時設定 Invoice 與 Broker 時，必須同時符合兩欄；不是任一欄符合就列出。'),
        ('Checkbox 空白','「空白」是空值篩選，不是在資料中搜尋中文「空白」二字。勾選多個選項為同欄 OR。'),
        ('報表範圍','Shipping 保留 Header／Detail；Mass 只有 Header。兩者均受角色資料範圍限制，Filter 不會擴大可查看範圍。'),
        ('日期與格式','日期區間與數字顯示格式分開理解；顯示 2 位不等於資料只存 2 位，是否阻擋要看該操作的卡控表。')
    ],[1.5,5.5])

    doc.add_heading('12.7 通知信卡控與排程邏輯',level=2)
    table(['通知／檢查','實際邏輯與處理'],[
        ('共通權限','排程、取消及預覽需有 Ship Info Header 編輯權限，且 Header 在可見範圍內；取消狀態或不可編輯資料不能新增排程。'),
        ('到貨通知 Flag','Arrival Notice Flag 必須為 N 才可排程；已有同 Invoice Pending 排程需先取消，不能再建立一筆。'),
        ('到貨通知收件者','Arrival Notice 至少一個有效 email，總文字最多 299 字；多個地址以分號 ; 分隔，不接受逗號或換行。可使用 mail: 前綴；開頭 * 的舊標記要先移除。'),
        ('到貨通知時間','不是任意輸入寄送時間；系統取管理者 SendTimes 中下一個台北時間時段，今日時段已過則取次日。沒有有效 HH:mm 時段會拒絕排程。'),
        ('排程取消','只有 Pending 可以取消；已進入 Sending 時不可再取消或修改，先重新整理查狀態。Sent 表示寄送完成，Failed 表示需處理失敗原因。'),
        ('排程預覽','排程保留當時收件者內容；查看既有排程的預覽，不可只用目前 Header 新值推定已排程內容。'),
        ('管制品通知','Controlled Goods Notice Flag 必須為 N；同 Invoice + TET PO 至少一筆 Detail 的 EL_FLAG 為 2；寄送時段必須有設定。已有 Pending 或 Sending 排程不得重複新增。'),
        ('管制品日期','操作區日期用於查詢當日排程，使用 yyyy-MM-dd。新增排程取下一個台北時間時段，今日已過則次日，不是等到 ETA 日期才寄。送出前仍核對 Flag 與 EL_FLAG。'),
        ('儲存同步排程','Ship Info 儲存會同步符合條件的通知排程；既有 Pending 依目前資料更新或取消，不反覆新增。管制品重複儲存會保留原 Pending 時段；Sending 時會阻擋衝突修改，請先查狀態。'),
        ('延遲通知資料','選擇 yyyy-MM-dd 通知日期，清單只列該 Delay Notification Date 且延遲原因非空白的資料，並套用可見範圍；資料原為 yyyy/MM/dd 的同日期亦能比對。'),
        ('延遲信內日期','Delay Notification Date 是通知日期；郵件表格的 Delivery Date 取 Delivery Information 的交貨日期，不使用通知日期替代。'),
        ('寄送與預覽','能顯示預覽不代表已寄信。各類通知仍受 Enabled、排程、SMTP 及收件者設定影響；以狀態及錯誤為準，不因等待而反覆建立。')
    ],[1.5,5.5])

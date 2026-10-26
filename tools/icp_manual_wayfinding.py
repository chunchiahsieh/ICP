"""Task navigation, annotated crops and recovery index for user guide 1.5."""
from PIL import Image, ImageDraw, ImageFont
from pathlib import Path

def scenario_guide(doc, table):
    doc.add_heading('1.0 依工作情境找操作',level=2)
    doc.add_paragraph('點選下表的步驟、規則或案例直接前往。尚未開始時先看步驟；看到錯誤時先查錯誤索引，避免直接重複上傳或提交。')
    table(['我要做什麼','操作入口','規則與示範'],[
        ('新增進口資料','[[3.2|Add DI/SA 步驟]]','[[12.1|上傳卡控]]／[[13.1|成功與失敗案例]]'),
        ('批次更新交貨資料','[[3.3|非 NCPI]]／[[3.4|NCPI]]','[[15.2|空白與覆蓋影響]]／[[13.2|非 NCPI 案例]]／[[13.3|NCPI 案例]]'),
        ('上傳貨代資料或找原檔','[[5.4|Forwarder 步驟與歷史]]','[[12.2|卡控]]／[[13.4|案例]]'),
        ('上傳報關資料','[[5.6|Customs Data 步驟]]','[[12.3|卡控]]／[[13.5|案例]]'),
        ('上傳報單、提單或費用','[[5.6|三類附件入口]]','[[13.6|報單]]／[[13.7|提單]]／[[13.8|Cost 案例]]'),
        ('查詢或下載報表','[[5.1|Shipping Report]]／[[5.3|Mass Data Report]]','[[12.6|過濾邏輯]]／[[11.9|放大操作圖]]'),
        ('產生提貨通知與 Case Mark','[[3.5|Export 步驟]]','[[13.12|案例]]／[[15.1|完成判斷]]'),
        ('Deposit／ARUR 起案','[[3.1|Ship Info 起案]]','[[12.5|卡控]]／[[11.9|錯誤位置放大圖]]'),
        ('查通知或調整收件者','[[3.6|通知信與申請方式]]','[[12.7|排程規則]]／[[18|管理者申請總表]]'),
        ('更新 Settings 代碼','[[4|各頁 Excel 步驟]]','[[12.4|整批取代規則]]／[[13.9|案例]]'),
        ('已看到錯誤或結果不明','[[17|錯誤快速索引]]','[[15.3|失敗重試]]／[[15.4|求助資訊]]')
    ],[2.2,2.3,2.5])
    doc.add_paragraph('一般文字格式、日期及數值依各功能規則；填寫建議另見 [[14|填寫建議與系統卡控的差別]]。')


def annotated_details(doc, picture, root):
    doc.add_heading('11.9 重要操作局部放大',level=2).paragraph_format.page_break_before=True
    doc.add_paragraph('以下使用 2026-10-26 已拍攝的畫面裁切，加上紅框與步驟編號；標記為文件說明，不是系統按鈕。未製作假的成功或覆蓋確認視窗。')
    source=root/'outputs/manual/screenshots-20261026'
    output=root/'outputs/manual/screenshots-20261026-details';output.mkdir(exist_ok=True)
    configs=[
        ('07-add-disa.jpg','01-disa-save.jpg',(275,65,665,240),[
            (1,(410,152,500,194)),(2,(282,152,402,194)),(3,(506,152,566,194))],
         'Add DI/SA 三個操作位置',
         '①先下載範例；②選檔上傳並核對預覽；③驗證通過後按儲存。本圖尚未上傳，儲存按鈕停用屬正常。[[13.1|查看案例]]／[[12.1|查看規則]]'),
        ('23-shipping-multiline.jpg','02-multiline-filter.jpg',(370,195,590,435),[
            (1,(386,243,558,380)),(2,(386,388,514,420))],
         '多行 Text 篩選與確定',
         '①每行一個完整 Invoice；②按確定套用，多值為 OR。輸入完成但尚未確定時，不可把畫面當成已套用。[[12.6|查看過濾規則]]'),
        ('02-arur-preview.jpg','03-arur-error.jpg',(470,65,1050,155),[
            (1,(479,110,950,140))],
         'ARUR 實際錯誤訊息',
         '①Receiver is required for Ship Plan creation. 表示收貨人缺值。先取消預覽、修正 Header 並儲存，再重新預覽；不要只看黃底欄位猜必填。[[12.5|查看起案卡控]]／[[17|錯誤索引]]'),
        ('31-export.jpg','04-export-result.jpg',(1050,340,2295,468),[
            (1,(1058,361,1140,432)),(2,(1602,361,2180,393))],
         'Export 狀態與既有錯誤',
         '①先辨識 Failed 或 Completed；②Failed 時讀取錯誤並保留 Request ID，交管理者確認。Completed 才檢視輸出並核對內容。[[13.12|查看案例]]／[[15.3|重試規則]]')]
    for index,(original,name,crop,markers,title,note) in enumerate(configs):
        image=Image.open(source/original).convert('RGB');image=image.crop(crop)
        top=76;canvas=Image.new('RGB',(image.width,image.height+top+12),'white');canvas.paste(image,(0,top));draw=ImageDraw.Draw(canvas)
        font=ImageFont.truetype('C:/Windows/Fonts/arialbd.ttf',22)
        # Position each number over its own target; keep source pixels readable.
        positions=[]
        for number,box in markers:
            l,t,r,b=box;l-=crop[0];r-=crop[0];t=t-crop[1]+top;b=b-crop[1]+top
            x=(l+r)//2
            if len(markers)==3: x={1:190,2:52,3:310}[number]
            if len(markers)==2 and image.width<300:x=45 if number==1 else image.width-45
            x=max(22,min(image.width-22,x));draw.rectangle((l,t,r,b),outline='#dc2d37',width=3)
            draw.line((x,50,(l+r)//2,t),fill='#dc2d37',width=3)
            draw.ellipse((x-20,8,x+20,48),fill='#dc2d37');draw.text((x-7,14),str(number),font=font,fill='white')
        path=output/name;canvas.save(path,quality=95)
        heading=doc.add_heading(title,level=3)
        if index==2:heading.paragraph_format.page_break_before=True
        picture(path,'實拍局部放大與文件編號　2026-10-26',max_height=2.2)
        doc.add_paragraph(note)


def append_lookup(doc,table):
    doc.add_heading('17 錯誤快速索引',level=1).paragraph_format.page_break_before=True
    doc.add_paragraph('依畫面訊息尋找關鍵字。{列}、{欄位}、{值} 是訊息中的變動內容；語言不同時可用 Invoice、Customer Code、Broker 等關鍵字定位。下表的「可重試」指修正後依正常流程再操作，不代表直接按原按鈕重送。')
    groups=[('17.1 上傳資料驗證',[
        ('第 {列} 列缺少必填欄位 {欄位}／須為有效數字','DI/SA 欄位空白或數字無法解析；依列號修正。','修正原檔後可重傳；[[13.1|案例]]／[[12.1|卡控]]'),
        ('INVOICE_NO＋INVOICE_SEQ 重複','同 Invoice 的 Seq 數值相同；01 與 1 不算不同。','修正正確 Seq 後重傳；[[13.1|案例]]'),
        ('DELIVERY_TO…Customer Code','填入的值不是有效交貨對象 Customer Code。','先核對代碼；缺代碼請資料管理者確認。[[12.1|規則]]／[[18|申請方式]]'),
        ('C/B…Broker 的 Key','Forwarder 的 Broker Key 不存在或無效。','修正 Key 後可重傳；需建檔先找資料管理者。[[13.4|案例]]'),
        ('MAWB/HAWB/FLT1#/FLT2#/ETA 須一致','同 Invoice 運送欄位不一致，包含選填 FLIGHT 2#。','核對並統一後可重傳；[[12.2|規則]]／[[13.4|案例]]'),
        ('第一個分頁名稱必須為 Upload Data','Customs 檔案的第一分頁名稱或順序不符。','使用新版範本並把 Upload Data 放第一頁；[[13.5|案例]]'),
        ('檔案內 Invoice Number 重複','Customs 以整個 Invoice Number 檢查；Line No 不同仍重複。','核對業務資料後再傳，不可任意改號避開；[[12.3|規則]]'),
        ('Excel 內 Invoice No 重複／ICP_HEADER 查無 Invoice No','Mass Update 重複目標或找不到既有 Invoice。','修正後可重傳；不會自動新增。[[13.2|非 NCPI]]／[[13.3|NCPI]]')]),
        ('17.2 Settings 與附件',[
        ('Excel 欄位或順序不符','Settings 表頭結構被改動。','重新下載該頁範本，貼回原欄位；[[13.9|案例]]'),
        ('Excel 沒有資料，原設定不會被清空。','空檔不能清空 Settings。','補完整資料再傳；整頁清理需求先找管理者。[[12.4|規則]]'),
        ('不可使用公式／重複鍵','Settings 含公式或分類與代碼組合重複。','貼上純值並修正重複列後重傳；[[13.9|案例]]'),
        ('Unknown resource key／Duplicate resource key','Languages 未知鍵或重複鍵。','用既有鍵並去除重複；新增資源找管理者。[[13.11|案例]]'),
        ('不支援的檔案格式／HAWB 不存在','附件類型或檔名對應不符；完整訊息見詳細對照。','先核對格式、HAWB 與可見資料；[[13.13|實際訊息]]／[[5.6|附件步驟]]'),
        ('File not found／檔案不存在','原檔或輸出檔目前不可存取，不等於資料未匯入。','找管理者查路徑，不要為補下載重匯資料；[[15.3|處理方式]]')]),
        ('17.3 起案與背景處理',[
        ('Receiver is required for Ship Plan creation.','ARUR 缺收貨人；取消預覽後修正並保存。','保存後可重開預覽；[[11.9|放大圖]]／[[12.5|卡控]]'),
        ('Hub notify failed: …','Export 通知 Hub 失敗。','先找維運管理者，保留 Request ID；確認原因及結果後再重送。[[13.12|案例]]'),
        ('Save 中斷／起案 Processing／結果不明','不是一律代表未成功，需先查回資料或案件狀態。','結果未確認前不要重送；[[15.3|重試流程]]／[[15.4|求助資訊]]'),
        ('通知 Failed／沒有收到信','查寄送狀態、收件者、排程及垃圾郵件。','請管理者確認紀錄；不要反覆建立通知。[[3.6|通知須知]]／[[12.7|規則]]')])]
    for heading,rows in groups:
        doc.add_heading(heading,level=2)
        table(['看到的訊息或關鍵字','原因與修正','是否可重試與求助'],rows,[2.3,2.3,2.4])
    doc.add_heading('18 哪些變更需通知管理者',level=1).paragraph_format.page_break_before=True
    doc.add_paragraph('以下是申請與協作方式；聯絡實際窗口依公司公告。未取得核准聯絡名單，本手冊只列角色，不臆填姓名或信箱。')
    table(['要變更的項目','請找誰與提供什麼','使用者完成後核對'],[
        ('共用 MailTo／CcTo','通知信設定管理者；環境、通知種類、原名單、增刪後名單、生效時間與原因。','重新查看設定與預覽；不應假設既有排程全部同步改寫。[[3.6|詳細流程]]'),
        ('寄件者、通知啟用、寄送時段','通知信設定管理者；欲變更項目、適用通知種類及時段。','請管理者確認生效環境；預覽不是實際寄送。[[12.7|規則]]'),
        ('個別到貨通知 To','有 Ship Info 編輯權限者；Invoice 與正確地址，多筆用分號分隔。','保存後重查待寄排程及預覽；無編輯權限再申請。共用 Cc 仍由管理者處理。[[3.6|說明]]'),
        ('Settings 新增或修改代碼','該分類資料維護者；分類、Key／Customer Code／Value、預期用途。','確認已在正確頁建檔，再依上傳規則填正確欄位；不用另造近似代碼。[[4|步驟]]'),
        ('看不到頁面、按鈕或資料範圍','系統權限管理者；帳號、環境網址、功能、所需動作及授權範圍。','權限調整後重新載入或登入，再核對可見資料。[[15.4|求助資訊]]'),
        ('原檔遺失、產檔或案件卡住','系統維運管理者；Invoice、批次／Request ID、時間、錯誤、已操作步驟。','等結果確認後再決定是否重試，不自行改狀態或反覆提交。[[15.3|重試流程]]')
    ],[1.6,2.8,2.6])

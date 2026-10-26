"""Fresh screenshot guide; originals remain intact and crops are labelled."""
from pathlib import Path
from PIL import Image


def add_fresh_screenshot_guide(doc, table, picture, root):
    shots=root/'outputs/manual/screenshots-20261026'
    crops=root/'outputs/manual/screenshots-20261026-crops'; crops.mkdir(exist_ok=True)
    doc.add_heading('11 新版畫面操作對照',level=1)
    doc.add_paragraph('本章為 2026-10-26 已登入系統的實際畫面。畫面資料為拍攝時既有資料；拍攝未按 Save、確認起案或實際寄信。部分圖片裁切為局部以提高閱讀性，原圖另保留。寬表格以水平捲軸查看其餘欄位。')
    groups=[
        ('11.1 Ship Info 與起案','3.1',[
            ('01-ship-info.jpg','Ship Info 清單','先選 Header，載入 Detail；再按 Deposit 或 ARUR。',None),
            ('02-arur-preview.jpg','ARUR 預覽與卡控','黃色是欄位重點；畫面實際顯示 Receiver is required for Ship Plan creation.，送出停用。先取消、補收貨人並儲存，再預覽。',(450,0,2326,680)),
            ('03-deposit-preview.jpg','Deposit 預覽及底部郵件','核對表頭、明細、下方郵件內容；按送出才進入後續確認，不把開啟預覽當成已起案。',(450,0,2326,1155)),
            ('36-shipinfo-notification-toolbar.jpg','編輯模式通知工具列','先開啟表頭，再切換編輯，可見到貨延遲、Arrival Notice、管制品到貨通知。',(760,0,1580,520)),
            ('37-arrival-schedule.jpg','Arrival Notice 排程清單','查看預定時間、收件者及狀態；目前沒有排程不代表系統故障。',(760,0,1580,600)),
            ('38-controlled-schedule.jpg','管制品通知清單','選擇日期是查詢排程；是否排程仍依 Flag、EL_FLAG 與寄送時段。',(760,0,1580,600)),
            ('39-delay-date-preview.jpg','延遲日期與郵件預覽','日期選 2026-10-07 可看到既有通知資料；信內 Delivery Date 是實際交貨日期。',(760,0,1580,800))]),
        ('11.2 Add DI/SA 與 Mass Update','3.2',[
            ('07-add-disa.jpg','Add DI/SA','下載範例 → 上傳 → 檢查預覽 → Save；上傳前先看 73 欄卡控。',(250,40,1450,360)),
            ('33-massupdate-non-ncpi.jpg','Mass Update 非 NCPI','使用本頁範本，核對既有 Invoice 與全部更新欄位，預覽後儲存。',(250,40,1450,360)),
            ('34-massupdate-ncpi.jpg','Mass Update NCPI','不要把批次更新當作新增；同 Invoice 不可在本檔重複。',(250,40,1450,360))]),
        ('11.3 Settings 各頁','4',[
            ('08-setting-bu.jpg','BU Codes／BU 代碼','下載本頁 Excel；上傳會整批取代本頁，不會追加。',None),
            ('09-setting-warehouse.jpg','Warehouse Codes／倉庫代碼','維護倉庫代碼，核對 Category 與 Key。',None),
            ('10-setting-deliveryto.jpg','Delivery To List／交貨對象清單','區分 Key、Customer Code、Name；DI/SA 上傳用 Customer Code。',None),
            ('11-setting-pickup.jpg','Pick Up Location／提貨地點','核對儲位、提貨地點、聯絡人及電話。',None),
            ('12-setting-eta.jpg','ETA Delivery Date Table','核對目的港、ETA 行事曆、倉庫、航班與交貨日欄位。',None),
            ('13-setting-defaultwarehouse.jpg','Default Delivery Warehouse','核對預設交貨倉庫對應，Excel 僅操作此頁範圍。',None),
            ('14-setting-ordertype.jpg','Order Type','維護訂單類型，勿用另一分類的 Excel 取代。',None),
            ('15-setting-airsea.jpg','Air/Sea','DI/SA 對應 Key，Customs Data 對應 Value。',None),
            ('16-setting-broker.jpg','Broker','Forwarder C/B 對應 Key，並非 Value。',None),
            ('17-setting-invoicetype.jpg','Invoice Type','核對發票類型代碼與顯示值。',None),
            ('18-setting-priority.jpg','Order Priority','維護優先序；不要把範例選項視為所有上傳共用限制。',None),
            ('19-setting-coo.jpg','Country of Origin／原產國','維護原產國代碼；報關資料 COO 篩選對應此設定。',None),
            ('20-setting-shipper.jpg','Shipper／出貨人','DI/SA 使用 Key，Customs Data 使用 Value；兩種匯入不可混填。',None),
            ('21-setting-customized.jpg','Customized／自訂設定','只維護本頁允許的自訂分類；其他設定分類由各頁分開管理。',None)]),
        ('11.4 Shipping Report 與 Mass Data Report','5.1',[
            ('23-shipping-multiline.jpg','Shipping Report 多行輸入','每行一個 Invoice，使用完整值精確篩選；按確定後套用。',None),
            ('24-shipping-result.jpg','Shipping Report 多筆結果','本次用兩個既有 Invoice 示範，Header 篩成兩筆；選一筆查看 Detail。',None),
            ('25-mass-report.jpg','Mass Data Report','僅保留 Header，沒有下方 Detail；下載格式與 Header 相同。',None)]),
        ('11.5 Forwarder Data Upload','5.4',[
            ('04-forwarder-check.jpg','Upload Check／上傳檢核','此分頁做上傳與預覽；按 Save 才算正式儲存。',(250,40,1450,360)),
            ('05-forwarder-history.jpg','Upload History／上傳紀錄','成功匯入批次、上傳人與筆數；畫面檔案不存在代表原檔目前遺失，不是當初匯入失敗。',None),
            ('06-history-date-filter.jpg','歷史紀錄日期區間','由匯入時間的篩選設定起訖日期；可再用檔名、狀態等欄位縮小結果。',None)]),
        ('11.6 Customs Clearance Data','5.6',[
            ('26-customs-clearance.jpg','報關資料及附件欄','上傳報關資料、Declaration PDF、Import Doc、Cost 各用對應按鈕。',None),
            ('27-importdoc-upload.jpg','Import Doc 上傳視窗','允許 pdf、xls、xlsx；檔名主體需為可存取資料的 HAWB。',(860,320,1500,820))]),
        ('11.7 Export 產檔與下載','3.5',[
            ('31-export.jpg','Shipping advice 上傳與工作狀態','Completed 才可檢視輸出；畫面也保留既有 Failed 訊息。',None),
            ('32-export-results.jpg','成功工作輸出檔案','逐檔下載或全部下載 ZIP；此畫面是既有成功工作，本次未建立新產檔工作。',(740,0,1580,470))]),
        ('11.8 通知信獨立頁面','3.6',[
            ('28-notification-overview.jpg','通知信設定與清單','此頁只供檢視，SMTP 與各通知設定由管理者維護 appsettings；不可在本頁修改收件者。',None),
            ('29-notification-arrival.jpg','Arrival notice 分頁','查看預定時間、Invoice、狀態與收件者；空清單文字有明確提示。',None),
            ('30-delay-mail-preview.jpg','延遲通知郵件預覽','核對通知日期與 Delivery Date，畫面是既有資料預覽，不會因按預覽而寄信。',(0,0,2326,650))])
    ]
    count=0
    for title, chapter, images in groups:
        doc.add_page_break(); doc.add_heading(title,level=2)
        doc.add_paragraph('詳細步驟見第 '+chapter+' 節；卡控與案例可透過章節連結跳轉。')
        for filename,label,note,box in images:
            source=shots/filename
            if not source.exists(): raise FileNotFoundError(source)
            if box is None:
                # Retain main toolbar and working area, omitting only unused lower whitespace.
                with Image.open(source) as im:
                    box=(max(0,250),0,im.width,min(im.height,850))
            with Image.open(source) as im:
                crop=im.crop((box[0],box[1],min(im.width,box[2]),min(im.height,box[3])))
                crop_path=crops/filename; crop.save(crop_path,quality=95)
            doc.add_heading(label,level=3)
            picture(crop_path,'實拍局部 2026-10-26 '+label,max_height=4.1)
            doc.add_paragraph(note)
            count+=1
    return count

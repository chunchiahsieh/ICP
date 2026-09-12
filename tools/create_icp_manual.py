from pathlib import Path
from docx import Document
from docx.shared import Inches, Pt, RGBColor
from docx.enum.text import WD_ALIGN_PARAGRAPH
from docx.enum.table import WD_CELL_VERTICAL_ALIGNMENT
from docx.enum.section import WD_SECTION
from docx.oxml import OxmlElement
from docx.oxml.ns import qn
from PIL import Image, ImageDraw, ImageFont

ROOT = Path(r"C:\Disk F\Aga\EY\TEL\ICP\Projects")
OUT = ROOT / "outputs" / "manual"
OUT.mkdir(parents=True, exist_ok=True)
DOCX = OUT / "ICP系統操作手冊.docx"
SHIP = Path(r"C:\Users\User\AppData\Local\Temp\codex-clipboard-73676402-59d7-4f96-9ec8-fb91495920d9.png")
SCOPE = Path(r"C:\Users\User\AppData\Local\Temp\codex-clipboard-4b7f1bf3-9b33-4f1e-bf72-abecdb662680.png")
SCREENSHOTS = OUT / "screenshots"
ANNOTATED = OUT / "screenshots-annotated"
ANNOTATED.mkdir(parents=True, exist_ok=True)

doc = Document()
sec = doc.sections[0]
sec.top_margin = Inches(0.65)
sec.bottom_margin = Inches(0.65)
sec.left_margin = Inches(0.7)
sec.right_margin = Inches(0.7)

styles = doc.styles
for name in ["Normal", "Title", "Subtitle", "Heading 1", "Heading 2", "Heading 3"]:
    s = styles[name]
    s.font.name = "Microsoft JhengHei"
    s._element.rPr.rFonts.set(qn("w:eastAsia"), "Microsoft JhengHei")
    s.font.color.rgb = RGBColor(0, 0, 0)
styles["Normal"].font.size = Pt(10.5)
styles["Normal"].paragraph_format.space_after = Pt(5)
styles["Normal"].paragraph_format.line_spacing = 1.15
styles["Title"].font.size = Pt(28)
styles["Title"].font.bold = True
title_style_ppr = styles["Title"]._element.get_or_add_pPr()
title_style_border = title_style_ppr.find(qn("w:pBdr"))
if title_style_border is not None:
    title_style_ppr.remove(title_style_border)
styles["Heading 1"].font.size = Pt(18)
styles["Heading 1"].font.bold = True
styles["Heading 1"].paragraph_format.space_before = Pt(12)
styles["Heading 1"].paragraph_format.space_after = Pt(6)
styles["Heading 2"].font.size = Pt(14)
styles["Heading 2"].font.bold = True
styles["Heading 3"].font.size = Pt(11.5)
styles["Heading 3"].font.bold = True

def shade(cell, fill):
    tcPr = cell._tc.get_or_add_tcPr()
    shd = tcPr.find(qn("w:shd"))
    if shd is None:
        shd = OxmlElement("w:shd")
        tcPr.append(shd)
    shd.set(qn("w:fill"), fill)

def borders(table):
    tblPr = table._tbl.tblPr
    tb = tblPr.find(qn("w:tblBorders"))
    if tb is None:
        tb = OxmlElement("w:tblBorders")
        tblPr.append(tb)
    for edge in ("top", "left", "bottom", "right", "insideH", "insideV"):
        el = OxmlElement(f"w:{edge}")
        el.set(qn("w:val"), "single")
        el.set(qn("w:sz"), "4")
        el.set(qn("w:color"), "D9D9D9")
        tb.append(el)

def add_table(headers, rows, widths=None):
    t = doc.add_table(rows=1, cols=len(headers))
    t.autofit = False
    borders(t)
    for i, h in enumerate(headers):
        c = t.rows[0].cells[i]
        c.text = h
        shade(c, "1F4E78")
        c.vertical_alignment = WD_CELL_VERTICAL_ALIGNMENT.CENTER
        for r in c.paragraphs[0].runs:
            r.font.color.rgb = RGBColor(255,255,255)
            r.font.bold = True
    for ri, row in enumerate(rows):
        cells = t.add_row().cells
        for i, value in enumerate(row):
            cells[i].text = str(value)
            cells[i].vertical_alignment = WD_CELL_VERTICAL_ALIGNMENT.CENTER
            if ri % 2:
                shade(cells[i], "F3F6F9")
    if widths:
        for row in t.rows:
            for i, width in enumerate(widths):
                row.cells[i].width = Inches(width)
    doc.add_paragraph().paragraph_format.space_after = Pt(2)
    return t

def step(text):
    p = doc.add_paragraph(style="List Number")
    p.add_run(text)
    return p

def bullet(text):
    p = doc.add_paragraph(style="List Bullet")
    p.add_run(text)
    return p

def picture(path, caption, max_height=None):
    if not path.exists():
        return
    picture_width = 7.0
    if max_height is not None:
        with Image.open(path) as source_image:
            image_width, image_height = source_image.size
        picture_width = min(picture_width, max_height * image_width / image_height)
    p = doc.add_paragraph()
    p.alignment = WD_ALIGN_PARAGRAPH.CENTER
    p.add_run().add_picture(str(path), width=Inches(picture_width))
    cap = doc.add_paragraph(caption)
    cap.alignment = WD_ALIGN_PARAGRAPH.CENTER
    for r in cap.runs:
        r.italic = True
        r.font.size = Pt(9)

def annotation_font(size, bold=False):
    candidates = [
        Path(r"C:\Windows\Fonts\msjhbd.ttc" if bold else r"C:\Windows\Fonts\msjh.ttc"),
        Path(r"C:\Windows\Fonts\arialbd.ttf" if bold else r"C:\Windows\Fonts\arial.ttf"),
    ]
    for candidate in candidates:
        if candidate.exists():
            return ImageFont.truetype(str(candidate), size)
    return ImageFont.load_default()

def annotate_screenshot(source, output, markers, redact_identity=False, crop_height=None):
    image = Image.open(source).convert("RGBA")
    draw = ImageDraw.Draw(image, "RGBA")
    width, height = image.size
    if redact_identity:
        draw.rounded_rectangle((8, height - 122, min(220, width - 8), height - 38), radius=8, fill=(31, 36, 42, 245))
        draw.text((20, height - 92), "Signed-in user", font=annotation_font(16), fill=(230, 233, 236, 255))
        draw.text((20, height - 66), "Identity hidden in guide", font=annotation_font(13), fill=(180, 186, 192, 255))
    for number, x, y, target_x, target_y in markers:
        radius = max(18, int(width * 0.018))
        draw.line((x, y, target_x, target_y), fill=(220, 45, 55, 255), width=max(4, int(width * 0.004)))
        draw.ellipse((x-radius, y-radius, x+radius, y+radius), fill=(220, 45, 55, 245), outline=(255,255,255,255), width=3)
        label = str(number)
        box = draw.textbbox((0, 0), label, font=annotation_font(radius, bold=True))
        draw.text((x-(box[2]-box[0])/2, y-(box[3]-box[1])/2-2), label, font=annotation_font(radius, bold=True), fill=(255,255,255,255))
    if crop_height is not None and crop_height < height:
        image = image.crop((0, 0, width, crop_height))
    image.convert("RGB").save(output, quality=92)
    return output

def active_menu_center_y(source):
    """Locate the blue highlighted menu row in the left navigation."""
    image = Image.open(source).convert("RGB")
    width, height = image.size
    pixels = image.load()
    scan_width = min(int(width * 0.20), 250)
    scores = []
    for y in range(70, height):
        score = sum(
            1 for x in range(scan_width)
            if 15 <= pixels[x, y][0] <= 45
            and 40 <= pixels[x, y][1] <= 75
            and 65 <= pixels[x, y][2] <= 115
        )
        scores.append((y, score))
    peak_y, peak_score = max(scores, key=lambda item: item[1])
    threshold = max(20, int(peak_score * 0.45))
    matching_rows = {y for y, score in scores if score >= threshold}
    top = peak_y
    bottom = peak_y
    while top - 1 in matching_rows:
        top -= 1
    while bottom + 1 in matching_rows:
        bottom += 1
    return (top + bottom) // 2

title = doc.add_paragraph(style="Title")
title.add_run("ICP 系統操作手冊")
title_ppr = title._p.get_or_add_pPr()
title_border = title_ppr.find(qn("w:pBdr"))
if title_border is not None:
    title_ppr.remove(title_border)
sub = doc.add_paragraph("功能操作與權限管理指南", style="Subtitle")
sub.alignment = WD_ALIGN_PARAGRAPH.LEFT
doc.add_paragraph("版本 1.1　更新日期 2026 年 9 月 12 日")
doc.add_paragraph(
    "本手冊說明 ICP 系統的導覽、查詢、維護、上傳、報表與權限管理方式。"
    "畫面及可用按鈕會依登入者的角色、資源權限與 DataScope 資料範圍而不同。"
)
doc.add_paragraph("本版已逐頁開啟並核對目前帳號可見的 31 個功能頁；操作名稱與頁面說明以英文介面為主，中文作必要補充。")

doc.add_heading("目錄", level=1)
for item in [
    "1 Quick Start", "2 Common Operations", "3 Functions", "4 Settings", "5 Reports and Data Exchange",
    "6 Languages", "7 Administration", "8 Role Permissions and DataScope", "9 常見問題", "10 Menu Page Index",
    "11 Visual Guide for Every Page"
]:
    doc.add_paragraph(item)

doc.add_page_break()
doc.add_heading("1 Quick Start", level=1)
doc.add_heading("1.1 登入與首頁", level=2)
step("開啟 ICP 網址並完成公司帳號驗證。")
step("登入後確認左下角 Logged in as 顯示正確人員。")
step("從左側選單進入功能；最近使用會列出近期開啟的頁面。")
bullet("若頁面或按鈕未顯示，先確認角色與資源權限。")
bullet("管理者修改權限後，使用者必須重新載入權限或重新登入。")

doc.add_heading("1.2 左側導覽", level=2)
add_table(["區域", "用途"], [
    ("Search menu", "輸入英文頁面名稱快速定位功能。"),
    ("Recent", "快速返回近期使用頁面；可清除紀錄。"),
    ("Pinned", "將常用頁面固定在選單中；按頁面右側的圖釘可釘選，再按一次可取消釘選。"),
    ("Links", "開啟管理者設定的外部或內部連結。"),
    ("Functions", "Ship Info、Add DI/SA、Mass Update 及 Export。"),
    ("Settings", "維護 BU、Warehouse、Delivery To 與各項代碼。"),
    ("Reports", "Shipping Report、Compare ICP vs AR/UR 及 Mass Data Report。"),
    ("Forwarder / Broker", "Forwarder Data Upload、Customs Data Download 及 Tariff Data。"),
    ("Administration", "Users、Resources、Roles 及角色指派。"),
], [1.5, 5.5])
step("在左側選單找到常用頁面，按頁面右側的 Pin 圖示加入 Pinned。")
step("從 Pinned 可直接開啟已固定頁面；若不再需要，按 Unpin／圖釘圖示取消固定。")
bullet("Pinned 只調整個人導覽捷徑，不會新增頁面權限，也不會改變 DataScope。")

pinned_recent_source = SCREENSHOTS / "00-pinned-recent.png"
if pinned_recent_source.exists():
    with Image.open(pinned_recent_source) as nav_image:
        nav_w, nav_h = nav_image.size
    pinned_recent_markers = [
        (1, int(nav_w*0.15), int(nav_h*0.10), int(nav_w*0.03), int(nav_h*0.10)),
        (2, int(nav_w*0.15), int(nav_h*0.13), int(nav_w*0.08), int(nav_h*0.13)),
        (3, int(nav_w*0.15), int(nav_h*0.17), int(nav_w*0.03), int(nav_h*0.17)),
        (4, int(nav_w*0.19), int(nav_h*0.17), int(nav_w*0.145), int(nav_h*0.17)),
        (5, int(nav_w*0.15), int(nav_h*0.23), int(nav_w*0.04), int(nav_h*0.20)),
        (6, int(nav_w*0.15), int(nav_h*0.56), int(nav_w*0.14), int(nav_h*0.56)),
    ]
    pinned_recent_annotated = annotate_screenshot(
        pinned_recent_source,
        ANNOTATED / "00-pinned-recent.png",
        pinned_recent_markers,
        crop_height=min(nav_h, 780),
    )
    picture(pinned_recent_annotated, "Pinned and Recent navigation", max_height=5.0)
    add_table(["No.", "Operation"], [
        ("1", "Pinned 顯示已固定的常用頁面。"),
        ("2", "按 Pinned 內的頁面名稱可直接開啟；按右側 Unpin 圖示可取消固定。"),
        ("3", "Recent 自動列出最近開啟的頁面。"),
        ("4", "按 Recent 右側的垃圾桶圖示可清除近期紀錄，不會刪除系統資料。"),
        ("5", "按 Recent 內的頁面名稱可快速返回該頁。"),
        ("6", "在分類選單中按頁面右側的 Pin 圖示，即可把頁面加入 Pinned。"),
    ], [0.65, 6.35])

doc.add_heading("1.3 Language", level=2)
if pinned_recent_source.exists():
    language_markers = [
        (1, int(nav_w*0.79), int(nav_h*0.065), int(nav_w*0.88), int(nav_h*0.025)),
    ]
    language_annotated = annotate_screenshot(
        pinned_recent_source,
        ANNOTATED / "00-language-switch.png",
        language_markers,
        crop_height=min(nav_h, 460),
    )
    picture(language_annotated, "Language selector", max_height=3.5)
    add_table(["No.", "Operation"], [
        ("1", "按右上角 Language，選擇要使用的介面語言。"),
        ("2", "切換後確認 Menu、欄位及按鈕文字；頁面資料、角色權限與 DataScope 不會改變。"),
    ], [0.65, 6.35])

doc.add_heading("2 Common Operations", level=1)
doc.add_heading("2.1 清單查詢與篩選", level=2)
step("進入清單頁後，先確認頁面標題與資料筆數。")
step("按欄位標題可切換排序；按欄位下方的「篩選」開啟條件。")
step("選擇或輸入條件後套用；按目前篩選數量可再次調整。")
step("使用右下角頁次及每頁筆數切換資料。")
bullet("畫面查無資料時，可能是篩選條件、DataScope 或角色權限造成。")
bullet("匯出內容應與目前權限及資料範圍一致。")

doc.add_heading("2.2 新增、編輯與刪除", level=2)
bullet("新增：按新增按鈕，填寫必填欄位後儲存。")
bullet("編輯：在清單的操作欄按編輯，確認資料後儲存。")
bullet("刪除：僅在確認資料不再使用時執行；若系統要求再次確認，核對目標後再提交。")
bullet("上傳：先核對檔案格式、工作表名稱與欄位，再執行上傳。")

doc.add_heading("3 Functions", level=1)
doc.add_heading("3.1 Ship Info", level=2)
doc.add_paragraph("Ship Info 頁分為 Ship Header 與 Ship Detail。先選取 Header 列，系統才會載入對應 Detail，並啟用 Deposit 或 ARUR Case 等可用操作。")
picture(SHIP, "圖 1　Ship Info 頁面與 Ship Header 清單")
step("在 Ship Header 欄位使用 Filter 或排序找到目標 Invoice No。")
step("點選資料列左側單選按鈕，載入該筆 Ship Detail。")
step("需要修改時，按 Action 欄的 Edit 圖示。")
step("核對表頭、明細與附件後儲存。")
bullet("若直接網址或 API 指向無權限的 Header，系統仍應拒絕存取。")
bullet("BROKER、BU、FORWARDER、MDP FLAG 等欄位可能受 DataScope 限制。")

for heading, purpose, steps in [
    ("3.2 Add DI/SA", "建立新的 DI/SA 資料。", ["進入 Add DI/SA。", "依畫面填寫或上傳必要資料。", "檢查驗證訊息後提交。"]),
    ("3.3 Mass Update Non NCPI", "以批次檔案更新非 NCPI 資料。", ["下載或準備規定格式。", "選擇檔案並執行驗證。", "確認預覽與錯誤列後提交更新。"]),
    ("3.4 Mass Update NCPI", "以批次檔案更新 NCPI 資料。", ["準備 NCPI 專用格式。", "上傳並檢查驗證結果。", "確認影響範圍後提交。"]),
    ("3.5 Export", "依條件匯出可見的船務資料。", ["設定匯出條件。", "確認資料範圍與筆數。", "執行下載並核對檔案內容。"]),
]:
    doc.add_heading(heading, level=2)
    doc.add_paragraph(purpose)
    for s in steps: step(s)

doc.add_heading("4 Settings", level=1)
doc.add_paragraph("設定頁供管理者維護系統選項。異動會影響其他頁面的下拉選單、預設值或計算邏輯，儲存前應先確認代碼是否已被使用。")
add_table(["頁面", "管理內容", "基本操作"], [
    ("BU Codes", "BU 代碼與說明", "Search, Add, Edit"),
    ("Warehouse Codes", "倉庫代碼", "Search, Add, Edit"),
    ("Delivery To List", "交貨對象資料", "Search, Add, Edit"),
    ("Pick Up Location", "提貨地點資料", "Search, Add, Edit"),
    ("ETA Delivery Date Table", "ETA 與交貨日規則", "Search, Add, Edit"),
    ("Default Delivery Warehouse", "預設倉庫對應", "Search, Add, Edit"),
    ("Order Type", "訂單類型選項", "Search, Add, Edit"),
    ("Air Sea", "Air／Sea 選項", "Search, Add, Edit"),
    ("Broker", "Broker 選項", "Search, Add, Edit"),
    ("Invoice Type", "Invoice Type 選項", "Search, Add, Edit"),
    ("Order Priority", "Order Priority 選項", "Search, Add, Edit"),
    ("Customized", "系統自訂參數", "Search and Maintain"),
], [1.6, 3.0, 2.4])

doc.add_heading("5 Reports and Data Exchange", level=1)
for heading, body, notes in [
    ("5.1 Shipping Report", "依條件查詢及匯出船務表頭與相關明細。", "清單、篩選選項與 Excel 匯出均套用 DataScope。"),
    ("5.2 Compare ICP vs AR/UR", "比對 ICP 與 AR/UR 資料差異。", "本頁目前不套用 DataScope。"),
    ("5.3 Mass Data Report", "查詢及匯出大量船務資料。", "清單、篩選與匯出均套用 DataScope。"),
    ("5.4 Forwarder Data Upload", "上傳貨代提供的資料檔案。", "本頁目前不套用 DataScope；上傳前先核對模板。"),
    ("5.5 Customs Data Download", "依條件下載報關所需資料。", "下載內容依登入者的 DataScope 限制。"),
    ("5.6 Tariff Data", "查詢、維護及匯出關稅資料與附件。", "清單、篩選、附件與匯出均套用 DataScope。"),
]:
    doc.add_heading(heading, level=2)
    doc.add_paragraph(body)
    step("設定查詢或檔案條件。")
    step("執行查詢、上傳或下載。")
    step("核對筆數、錯誤訊息與輸出內容。")
    doc.add_paragraph("注意：" + notes)

doc.add_heading("6 Languages", level=1)
doc.add_paragraph("Languages 用於維護繁體中文、英文與日文顯示文字。")
step("進入 Languages 並搜尋資源鍵。")
step("編輯各語言文字，保持相同資源鍵的語意一致。")
step("儲存後重新載入頁面，確認畫面顯示。")
bullet("不要直接更改資源鍵；程式會以資源鍵尋找翻譯。")

doc.add_heading("7 Administration", level=1)
add_table(["頁面", "用途"], [
    ("Users", "查詢使用者並查看 Permissions。"),
    ("Resources", "管理頁面、Action 與 ResourceCode。"),
    ("Roles", "建立及維護角色。"),
    ("Role Permissions", "將 Roles、Resources 與 DataScope 組合。"),
    ("User Roles", "將角色指派給特定 TELID。"),
    ("Department Roles", "依部門指派角色。"),
    ("Mail Group Roles", "依郵件群組指派角色。"),
], [1.7, 5.3])
doc.add_heading("7.1 建議設定順序", level=2)
step("先確認資源已存在且 ResourceCode、Route 與 Action 正確。")
step("建立角色。")
step("在 Role Permissions 選取角色與資源，必要時設定 DataScope。")
step("透過人員、部門或郵件群組指派角色。")
step("讓目標使用者重新載入權限或重新登入，然後測試功能。")

doc.add_heading("8 Role Permissions and DataScope", level=1)
doc.add_paragraph("DataScope 用來限制同一功能中可見的資料。未勾選 Restrict visible data 時不限制；勾選後依資料表、欄位、運算子與值建立條件。")
picture(SCOPE, "圖 2　Role Permissions 建立流程中的 Data scope 設定")
doc.add_heading("8.1 設定步驟", level=2)
step("在 Role Permissions 精靈選擇角色。")
step("選擇要授權的資源。")
step("進入 Set data scope，勾選 Restrict visible data。")
step("選擇資料表、欄位及運算子，再輸入值。")
step("需要多個欄位時按 Add condition；同一規則中的條件必須全部符合。")
step("確認摘要並建立權限，最後重新載入使用者權限。")
add_table(["運算子", "Value 範例", "結果"], [
    ("=", "LOG YUANFAN", "只允許完全等於此值。"),
    ("in", "LOG YUANFAN, LOG KWE", "允許符合其中任一值；不需加單引號。"),
    ("contains", "YUANFAN", "允許欄位中包含此文字。"),
], [1.0, 2.5, 3.5])
doc.add_heading("8.2 套用頁面", level=2)
add_table(["頁面", "常用資料表", "常用欄位"], [
    ("Ship Info", "ICP_HEADER", "BROKER、BU、FORWARDER、MDP_FLAG"),
    ("Shipping Report", "ICP_HEADER", "BROKER、BU、FORWARDER"),
    ("Mass Data Report", "ICP_HEADER", "BROKER、BU、FORWARDER"),
    ("Tariff Data", "TariffData", "Broker、AirSea、Shipper"),
    ("Customs Data Download", "實際下載資料來源", "依下載資料欄位設定"),
], [1.8, 2.0, 3.2])
doc.add_paragraph("目前系統規則：DataScope JSON、資料表、欄位、型別或運算子無法解析時，該規則視為未設定，因此不限制資料。管理者應在正式授權前使用目標帳號驗證。")

doc.add_heading("9 常見問題", level=1)
add_table(["現象", "檢查方式"], [
    ("看不到頁面", "確認資源權限、角色指派與 Route；重新登入。"),
    ("修改權限後沒有變化", "重新載入 Session 權限或登出再登入。"),
    ("查不到資料", "清除欄位篩選，確認 DataScope 的資料表、欄位與值。"),
    ("DataScope 顯示全部資料", "檢查 JSON、資料表、欄位、運算子與值是否有效；錯誤規則目前視為未限制。"),
    ("Ship Info 明細未出現", "先選取一筆 Ship Header；確認該 Header 在可見範圍。"),
    ("匯出筆數不同", "確認匯出條件、目前篩選與 DataScope 是否一致。"),
    ("上傳失敗", "檢查檔案格式、必填欄位、重複資料及畫面錯誤訊息。"),
], [2.3, 4.7])

doc.add_heading("10 Menu Page Index", level=1)
pages = [
    ("Functions", "Ship Info", "/ShipInfo"), ("Functions", "Add DI/SA", "/AddDiSa"),
    ("Functions", "Mass Update Non NCPI", "/MassUpdateNonNcpi"), ("Functions", "Mass Update NCPI", "/MassUpdateNcpi"),
    ("Functions", "Export", "/Export"), ("Settings", "BU Codes", "/BuCode"), ("Settings", "Warehouse Codes", "/WhCode"),
    ("Settings", "Delivery To List", "/DeliveryToList"), ("Settings", "Pick Up Location", "/PickUpLocation"),
    ("Settings", "ETA Delivery Date Table", "/EtaDelDateTable"), ("Settings", "Default Delivery Warehouse", "/DefaultDeliveryWh"),
    ("Settings", "Order Type", "/OrderType"), ("Settings", "Air Sea", "/AirSea"), ("Settings", "Broker", "/Broker"),
    ("Settings", "Invoice Type", "/InvoiceType"), ("Settings", "Order Priority", "/OrderPriority"), ("Settings", "Customized", "/Customized"),
    ("Reports", "Shipping Report", "/ShippingReport"), ("Reports", "Compare ICP vs AR/UR", "/CompareIcpVsArUr"),
    ("Reports", "Mass Data Report", "/MassDataReport"), ("Forwarder", "Forwarder Data Upload", "/ForwarderDataUpload"),
    ("Broker", "Customs Data Download", "/CustomsDataDownload"), ("Broker", "Tariff Data", "/TariffData"),
    ("Languages", "Languages", "/LocalizationManagement"), ("Administration", "Users", "/Users"),
    ("Administration", "Resources", "/Resources"), ("Administration", "Roles", "/Roles"), ("Administration", "Role Permissions", "/RolePermissions"),
    ("Administration", "User Roles", "/RoleTelIds"), ("Administration", "Department Roles", "/RoleDepIds"),
    ("Administration", "Mail Group Roles", "/RoleMailGroups"),
]
add_table(["Category", "Page", "Route"], pages, [1.5, 3.3, 2.2])

doc.add_heading("11 Visual Guide for Every Page", level=1)
doc.add_paragraph("本章使用目前英文介面的實際畫面。圖中的紅色編號對應下方操作說明；畫面資料會隨環境、角色、DataScope 與當下資料而不同。")

login_source = SCREENSHOTS / "00-login.png"
if login_source.exists():
    with Image.open(login_source) as login_image:
        w, h = login_image.size
    login_markers = [
        (1, int(w*0.27), int(h*0.15), int(w*0.36), int(h*0.15)),
        (2, int(w*0.27), int(h*0.21), int(w*0.36), int(h*0.21)),
        (3, int(w*0.27), int(h*0.255), int(w*0.36), int(h*0.255)),
        (4, int(w*0.73), int(h*0.30), int(w*0.65), int(h*0.30)),
    ]
    login_annotated = annotate_screenshot(login_source, ANNOTATED / "00-login.png", login_markers, crop_height=min(h, 520))
    doc.add_page_break()
    doc.add_heading("Guide 01 — Login", level=2)
    picture(login_annotated, "Login page")
    add_table(["No.", "Operation"], [
        ("1", "在 Staff ID 輸入 620256。"),
        ("2", "Password 依部署環境要求輸入；目前測試環境可依現行登入規則操作。"),
        ("3", "需要時勾選 Remember Password；共用電腦不建議勾選。"),
        ("4", "按 Login。成功後確認頁面進入 Ship Info，且左下角顯示登入狀態。"),
    ], [0.65, 6.35])

screen_pages = [
    ("11.2", "Ship Info", "01-ship-info.png", "用 Filter 找到 Header；選取資料列後查看 Detail，並依權限使用 Deposit 或 ARUR。"),
    ("11.3", "Add DI/SA", "02-add-di-sa.png", "輸入或上傳 DI/SA 資料，檢查必填欄位與驗證訊息後提交。"),
    ("11.4", "Mass Update Non NCPI", "03-mass-update-non-ncpi.png", "選擇非 NCPI 模板檔，先驗證預覽，再執行批次更新。"),
    ("11.5", "Mass Update NCPI", "04-mass-update-ncpi.png", "選擇 NCPI 模板檔，確認影響筆數與錯誤列後更新。"),
    ("11.6", "Export", "05-export.png", "設定匯出條件，確認範圍後產生並下載檔案。"),
    ("11.7", "BU Codes", "06-bu-codes.png", "搜尋 BU Code；使用新增或編輯維護代碼。"),
    ("11.8", "Warehouse Codes", "07-warehouse-codes.png", "搜尋 Warehouse Code；使用新增或編輯維護資料。"),
    ("11.9", "Delivery To List", "08-delivery-to-list.png", "搜尋 Delivery To；新增或編輯交貨對象。"),
    ("11.10", "Pick Up Location", "09-pick-up-location.png", "搜尋提貨地點；新增或編輯可用地點。"),
    ("11.11", "ETA Delivery Date Table", "10-eta-delivery-date-table.png", "搜尋並維護 ETA 與 Delivery Date 對應規則。"),
    ("11.12", "Default Delivery Warehouse", "11-default-delivery-warehouse.png", "維護預設交貨倉庫對應，儲存前確認條件沒有衝突。"),
    ("11.13", "Order Type", "12-order-type.png", "搜尋並維護 Order Type 選項。"),
    ("11.14", "Air Sea", "13-air-sea.png", "搜尋並維護 Air 或 Sea 選項。"),
    ("11.15", "Broker", "14-broker.png", "搜尋並維護 Broker 選項。"),
    ("11.16", "Invoice Type", "15-invoice-type.png", "搜尋並維護 Invoice Type 選項。"),
    ("11.17", "Order Priority", "16-order-priority.png", "搜尋並維護 Order Priority 選項。"),
    ("11.18", "Customized", "17-customized.png", "搜尋並維護系統自訂參數；變更前確認使用範圍。"),
    ("11.19", "Shipping Report", "18-shipping-report.png", "設定條件後查詢或匯出；結果會套用 DataScope。"),
    ("11.20", "Compare ICP vs AR UR", "19-compare-icp-arur.png", "設定比對條件，執行後檢查 ICP 與 AR/UR 差異。"),
    ("11.21", "Mass Data Report", "20-mass-data-report.png", "設定大量資料查詢條件並匯出；結果會套用 DataScope。"),
    ("11.22", "Forwarder Data Upload", "21-forwarder-data-upload.png", "選擇規定格式檔案，上傳前確認模板與檔案內容。"),
    ("11.23", "Customs Data Download", "22-customs-data-download.png", "設定下載條件並產生檔案；輸出資料會依權限範圍限制。"),
    ("11.24", "Tariff Data", "23-tariff-data.png", "查詢、維護或匯出 Tariff Data；清單與附件會套用 DataScope。"),
    ("11.25", "Languages", "24-languages.png", "搜尋資源鍵並維護各語言文字，儲存後重新載入驗證。"),
    ("11.26", "Users", "25-users.png", "搜尋使用者並查看其角色與 Permissions。"),
    ("11.27", "Resources", "26-resources.png", "搜尋或維護 ResourceCode、Route 與 Action。"),
    ("11.28", "Roles", "27-roles.png", "建立、搜尋或編輯角色。"),
    ("11.29", "Role Permissions", "28-role-permissions.png", "選擇角色與資源；需要限制資料時設定 DataScope。"),
    ("11.30", "User Roles", "29-user-roles.png", "輸入 TELID 並指派角色，完成後讓使用者重新登入。"),
    ("11.31", "Department Roles", "30-department-roles.png", "選擇部門並指派角色，確認套用範圍。"),
    ("11.32", "Mail Group Roles", "31-mail-group-roles.png", "選擇 Mail Group 並指派角色，確認群組成員與權限。"),
]

for screen_number, (_, page_name, file_name, operation) in enumerate(screen_pages, start=2):
    source = SCREENSHOTS / file_name
    if not source.exists():
        continue
    with Image.open(source) as page_image:
        w, h = page_image.size
    active_y = active_menu_center_y(source)
    markers = [
        (1, int(w*0.16), active_y, int(w*0.09), active_y),
        (2, int(w*0.31), int(h*0.08), int(w*0.40), int(h*0.08)),
        (3, int(w*0.82), int(h*0.14), int(w*0.43), int(h*0.15)),
        (4, int(w*0.33), int(h*0.34), int(w*0.48), int(h*0.34)),
    ]
    crop_height = min(h, max(720, active_y + 70))
    annotated = annotate_screenshot(source, ANNOTATED / file_name, markers, redact_identity=False, crop_height=crop_height)
    doc.add_page_break()
    doc.add_heading(f"Guide {screen_number:02d} — {page_name}", level=2)
    picture(annotated, f"{page_name} page", max_height=5.4)
    if "Upload" in page_name or page_name in {"Add DI/SA", "Mass Update Non NCPI", "Mass Update NCPI"}:
        main_area_note = "在主要工作區選擇檔案；上傳後查看預覽、驗證結果或錯誤訊息，再決定是否儲存。"
    elif page_name in {"User Roles", "Department Roles", "Mail Group Roles"}:
        main_area_note = "在清單確認既有角色指派；新增指派後重新載入權限，並以目標帳號驗證。"
    elif page_name == "Languages":
        main_area_note = "在翻譯表格編輯各語言欄位；儲存後重新載入頁面確認新文字。"
    else:
        main_area_note = "在主要資料區查看結果或使用欄位 Filter；儲存、下載或匯出前先核對目前條件。"
    add_table(["No.", "Operation"], [
        ("1", "使用 Search menu 或左側分類切換頁面；常用頁面可按 Pin 加入 Pinned，再按一次取消。"),
        ("2", f"確認目前頁面為 {page_name}。"),
        ("3", operation),
        ("4", main_area_note),
    ], [0.65, 6.35])

footer = sec.footer.paragraphs[0]
footer.alignment = WD_ALIGN_PARAGRAPH.CENTER
footer.add_run("ICP 系統操作手冊 版本 1.1")

doc.core_properties.title = "ICP 系統操作手冊"
doc.core_properties.subject = "ICP 功能操作與權限管理"
doc.core_properties.author = "ICP Project Team"
doc.save(DOCX)
print(DOCX)

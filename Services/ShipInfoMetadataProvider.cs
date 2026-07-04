using ICP.Models.ShipInfo;

namespace ICP.Services;

public class ShipInfoMetadataProvider
{
    public ShipInfoPageConfig GetPageConfig(string? culture = null)
    {
        var headerFields = BuildHeaderFields();
        return new ShipInfoPageConfig
        {
            Culture = culture ?? "zh-TW",
            HeaderFields = headerFields,
            DetailFields = BuildDetailFields(),
            SearchFields = ShipInfoMetadataHelper.GetSearchFields(headerFields),
            StatusRules = ShipInfoStatusRules.BuildMatrix()
        };
    }

    public IReadOnlyList<ShipInfoFieldMetadata> GetHeaderEditFields() =>
        BuildHeaderFields()
            .Where(x => x.FieldName is "Status" or "SaDate" or "Eta")
            .ToList();

    private static IReadOnlyList<ShipInfoFieldMetadata> BuildHeaderFields() =>
    [
        Field("header-status", "Status", "Status", "狀態", 10, ShipInfoControlTypes.Select,
            lookupCategory: ShipInfoStatuses.LookupCategory, editable: true),
        Field("header-create-date", "CreateDate", "Create Date", "建立日期", 20, ShipInfoControlTypes.Text,
            group: "Basic", editable: false, maxLength: 20),
        Field("header-sa-date", "SaDate", "SA Date", "SA 日期", 30, ShipInfoControlTypes.DateRange,
            group: "Shipping", editable: true, maxLength: 10),
        Field("header-invoice-no", "InvoiceNo", "Invoice No", "發票號碼", 40, ShipInfoControlTypes.Text,
            group: "Invoice", editable: false, maxLength: 30),
        Field("header-broker", "Broker", "Broker", "報關行", 50, ShipInfoControlTypes.Select,
            group: "Customs", lookupCategory: "Broker", editable: false, maxLength: 30),
        Field("header-eta", "Eta", "ETA", "ETA", 60, ShipInfoControlTypes.DateRange,
            group: "Shipping", editable: true, maxLength: 10),
        Field("header-tet-po", "TetPo", "TET PO", "TET PO", 70, ShipInfoControlTypes.Text,
            group: "Order", visible: false, editable: false, maxLength: 35),
        Field("header-forwarder", "Forwarder", "Forwarder", "貨代", 80, ShipInfoControlTypes.Text,
            group: "Shipping", visible: false, editable: false, maxLength: 50),
        Field("header-etd", "Etd", "ETD", "ETD", 90, ShipInfoControlTypes.Text,
            group: "Shipping", visible: false, editable: false, maxLength: 10),
        Field("header-invoice-date", "InvoiceDate", "Invoice Date", "發票日期", 100, ShipInfoControlTypes.Text,
            group: "Invoice", visible: false, editable: false, maxLength: 10),
        Field("header-mawb", "Mawb", "MAWB", "MAWB", 110, ShipInfoControlTypes.Text,
            group: "Shipping", visible: false, editable: false, maxLength: 20),
        Field("header-hawb", "Hawb", "HAWB", "HAWB", 120, ShipInfoControlTypes.Text,
            group: "Shipping", visible: false, editable: false, maxLength: 20),
        Field("header-flt", "Flt", "Flight", "航班", 130, ShipInfoControlTypes.Text,
            group: "Shipping", visible: false, editable: false, maxLength: 20),
        Field("header-delivery-to", "DeliveryTo", "Delivery To", "交貨對象", 140, ShipInfoControlTypes.Select,
            group: "Warehouse", lookupCategory: "DeliveryToList", visible: false, editable: false, maxLength: 20),
        Field("header-warehouse", "Warehouse", "Warehouse", "倉庫", 150, ShipInfoControlTypes.Text,
            group: "Warehouse", visible: false, editable: false, maxLength: 20),
        Field("header-order-type", "OrderType", "Order Type", "訂單類型", 160, ShipInfoControlTypes.Text,
            group: "Order", visible: false, editable: false, maxLength: 20),
        Field("header-deposit", "Deposit", "Deposit", "押金", 170, ShipInfoControlTypes.Text,
            group: "Case", visible: false, editable: false, maxLength: 10),
        Field("header-rt-no", "RtNo", "RT No", "RT 單號", 180, ShipInfoControlTypes.Text,
            group: "Case", visible: false, editable: false, maxLength: 30),
        Field("header-notes", "Notes", "Notes", "備註", 190, ShipInfoControlTypes.Textarea,
            group: "Other", visible: false, editable: false, maxLength: 1000),
        Field("header-sap-remarks", "SapRemarks", "SAP Remarks", "SAP 備註", 200, ShipInfoControlTypes.Textarea,
            group: "Other", visible: false, editable: false, maxLength: 1000)
    ];

    public IReadOnlyList<ShipInfoFieldMetadata> GetDetailEditFields() =>
        BuildDetailFields().Where(x => x.Editable).ToList();

    private static IReadOnlyList<ShipInfoFieldMetadata> BuildDetailFields() =>
    [
        Field("detail-invoice-seq", "InvoiceSeq", "Invoice Seq", "發票序號", 10, ShipInfoControlTypes.Decimal,
            group: "Basic", editable: false),
        Field("detail-tet-po-line", "TetPoLine", "TET PO Line", "TET PO 項次", 20, ShipInfoControlTypes.Text,
            group: "Basic", editable: false, maxLength: 35),
        Field("detail-item-no", "ItemNo", "Item No", "料號", 30, ShipInfoControlTypes.Text,
            group: "Basic", editable: false, maxLength: 47),
        Field("detail-description", "Description", "Description", "品名", 40, ShipInfoControlTypes.Text,
            group: "Basic", editable: true, maxLength: 60),
        Field("detail-qty", "Qty", "Qty", "數量", 50, ShipInfoControlTypes.Decimal,
            group: "Basic", editable: true, required: true, minValue: 0),
        Field("detail-uom", "Uom", "UOM", "單位", 60, ShipInfoControlTypes.Text,
            group: "Basic", editable: true, maxLength: 10),
        Field("detail-coo", "Coo", "COO", "產地", 70, ShipInfoControlTypes.Text,
            group: "Basic", editable: true, maxLength: 50),
        Field("detail-price", "Price", "Price", "單價", 80, ShipInfoControlTypes.Decimal,
            group: "Amount", editable: false),
        Field("detail-amount", "Amount", "Amount", "金額", 90, ShipInfoControlTypes.Decimal,
            group: "Amount", editable: false),
        Field("detail-currency", "Currency", "Currency", "幣別", 100, ShipInfoControlTypes.Text,
            group: "Amount", editable: false, maxLength: 3),
        Field("detail-carton-no", "CartonNo", "Carton No", "箱號", 110, ShipInfoControlTypes.Decimal,
            group: "Packing", editable: true),
        Field("detail-gross-weight", "GrossWeight", "Gross Weight", "毛重", 120, ShipInfoControlTypes.Decimal,
            group: "Packing", editable: true)
    ];

    private static ShipInfoFieldMetadata Field(
        string id,
        string fieldName,
        string displayName,
        string displayNameZh,
        int displayOrder,
        string controlType,
        string? group = null,
        string? lookupCategory = null,
        bool searchable = true,
        bool editable = true,
        bool required = false,
        bool visible = true,
        string? searchControlType = null,
        string? placeholder = null,
        int? maxLength = null,
        string? tooltip = null,
        decimal? minValue = null)
    {
        return new ShipInfoFieldMetadata
        {
            Id = id,
            FieldName = fieldName,
            DisplayName = displayName,
            DisplayNameZh = displayNameZh,
            DisplayOrder = displayOrder,
            ControlType = controlType,
            SearchControlType = searchControlType,
            LookupCategory = lookupCategory,
            Searchable = searchable,
            Editable = editable,
            Required = required,
            Visible = visible,
            Placeholder = placeholder,
            MaxLength = maxLength,
            Tooltip = tooltip,
            MinValue = minValue,
            Group = group
        };
    }
}

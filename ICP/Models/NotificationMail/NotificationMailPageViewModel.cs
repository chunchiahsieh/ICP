namespace ICP.Models.NotificationMail;

public sealed record DeliveryDelayListRow(Guid HeaderId, string InvoiceNo, string? Hawb,
    string? Warehouse, string? Flt, string? Eta, string? Reason,
    string? NotificationDate, string Status);

public sealed record ArrivalNoticePreview(
    string MailTo,
    string InvoiceNos,
    int InvoiceCount,
    DateTime ScheduledAtUtc,
    string Subject,
    string BodyHtml);

public sealed record ArrivalNoticeScheduleListRow(
    string InvoiceNo, string MailTo, DateTime ScheduledAtUtc, string State);

public sealed record ControlledGoodsNoticePageRow(string InvoiceNo, string Eta,
    DateTime ScheduledAtUtc, string State, string Subject, string BodyHtml);

public sealed record NotificationMailQueryRow(
    Guid Id, string InvoiceNo, string? Hawb, string? Warehouse, string? Flt,
    string? Eta, string? Reason, string? NotificationDate, string? MailTo,
    DateTime? ScheduledAtUtc, string Status, string? FailureMessage = null);

public sealed class NotificationMailQueryViewModel
{
    public required string Type { get; init; }
    public required IReadOnlyList<NotificationMailQueryRow> Rows { get; init; }
}

public sealed class NotificationMailQueryCriteria
{
    public string Type { get; set; } = string.Empty;
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
    public Dictionary<string, string> Text { get; set; } = [];
    public Dictionary<string, string> Date { get; set; } = [];
    public Dictionary<string, List<string>> Checkbox { get; set; } = [];
}

public sealed class NotificationMailPageViewModel
{
    public required NotificationMailOptions Options { get; init; }
    public required string Today { get; init; }
    public required string SelectedNotificationDate { get; init; }
    public required string SelectedArrivalDate { get; init; }
    public required string SelectedControlledEta { get; init; }
    public required string ActivePreviewType { get; init; }
    public required int DeliveryDelayCount { get; init; }
    public required IReadOnlyList<DeliveryDelayListRow> DeliveryDelayRows { get; init; }
    public required string DeliveryDelaySubject { get; init; }
    public required string DeliveryDelayBodyHtml { get; init; }
    public required bool DeliveryDelayCanSend { get; init; }
    public required IReadOnlyList<string> DeliveryDelayMissingSettings { get; init; }
    public required IReadOnlyList<ArrivalNoticePreview> ArrivalNoticePreviews { get; init; }
    public required IReadOnlyList<ArrivalNoticeScheduleListRow> ArrivalNoticeSchedules { get; init; }
    public required IReadOnlyList<ControlledGoodsNoticePageRow> ControlledGoodsNoticeRows { get; init; }
    public required bool ControlledGoodsScheduleAvailable { get; init; }
    public required IReadOnlyList<string> ControlledGoodsMissingSettings { get; init; }
    public required IReadOnlyList<string> ArrivalNoticeInvalidInvoiceNos { get; init; }
    public required IReadOnlyList<string> ArrivalNoticeMissingSettings { get; init; }
    public required bool ArrivalScheduleAvailable { get; init; }
}

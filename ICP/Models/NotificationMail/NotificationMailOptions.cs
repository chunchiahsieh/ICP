namespace ICP.Models.NotificationMail;

public sealed class NotificationMailOptions
{
    public const string SectionName = "NotificationMail";
    public NotificationSmtpOptions Smtp { get; set; } = new();
    public NotificationMessageOptions DeliveryDelay { get; set; } = new();
    public NotificationMessageOptions ArrivalNotice { get; set; } = new();
    public NotificationMessageOptions ControlledGoodsArrival { get; set; } = new();
    public NotificationMessageOptions DepositCase { get; set; } = new();
    public string ShipInfoUrl { get; set; } = string.Empty;
}

public sealed class NotificationSmtpOptions
{
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 587;
    public bool EnableSsl { get; set; } = true;
    public string UserName { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string FromAddress { get; set; } = string.Empty;
    public string FromName { get; set; } = string.Empty;
}

public sealed class NotificationMessageOptions
{
    public bool Enabled { get; set; }
    // Daily or Immediate. Daily uses every configured HH:mm slot; Immediate has no send time.
    public string Frequency { get; set; } = "Daily";
    public List<string> SendTimes { get; set; } = [];
    public string Title { get; set; } = string.Empty;
    public List<string> MailTo { get; set; } = [];
    public List<string> CcTo { get; set; } = [];
}

namespace ICP.Models.Auth;

public class LoggedInUser
{
    public int KeyId { get; set; }

    public string TelId { get; set; } = string.Empty;

    public string? UserName { get; set; }

    public string? DisplayName { get; set; }

    public string? EmailAddress { get; set; }

    public string? DepId { get; set; }

    public string? DepName { get; set; }
}

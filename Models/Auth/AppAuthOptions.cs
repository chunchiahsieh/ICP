namespace ICP.Models.Auth;

public class AppAuthOptions
{
    public const string SectionName = "App";

    public string Mode { get; set; } = "PRD";//"DEV";

    public string? SimulatedWindowsIdentity { get; set; }

    public string SuperUser { get; set; } = "Off";

    public bool IsSuperUserEnabled =>
        string.Equals(SuperUser, "On", StringComparison.OrdinalIgnoreCase);

    public DevUserOptions DevUser { get; set; } = new();
}

public class DevUserOptions
{
    public string TelId { get; set; } = string.Empty;

    public string? UserName { get; set; }

    public string? DisplayName { get; set; }

    public string? EmailAddress { get; set; }

    public string? DepId { get; set; }

    public string? DepName { get; set; }
}

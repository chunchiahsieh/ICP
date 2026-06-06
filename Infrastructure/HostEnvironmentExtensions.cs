namespace ICP.Infrastructure;

public static class HostEnvironmentExtensions
{
    public static bool IsAgaComputer() =>
        string.Equals(Environment.MachineName, "AGA-PC", StringComparison.OrdinalIgnoreCase);
}

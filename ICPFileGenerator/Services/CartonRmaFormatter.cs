namespace ICPFileGenerator.Services;

internal static class CartonRmaFormatter
{
    public static string Join(IEnumerable<string> rmas) => string.Join(" & ", rmas
        .Where(rma => !string.IsNullOrWhiteSpace(rma))
        .Select(rma => rma.Trim())
        .Distinct(StringComparer.OrdinalIgnoreCase));
}

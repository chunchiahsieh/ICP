namespace ICP.Models.ShipInfo;

public static class ShipInfoFilterTypes
{
    public const string Checkbox = "Checkbox";
    public const string Text = "Text";
    public const string DateRange = "DateRange";
    public const string Date = "Date";

    public static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Checkbox;
        }

        return value.Trim() switch
        {
            Text => Text,
            DateRange => DateRange,
            Date => Date,
            Checkbox => Checkbox,
            _ => Checkbox
        };
    }
}

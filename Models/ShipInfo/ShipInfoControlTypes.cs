namespace ICP.Models.ShipInfo;

public static class ShipInfoControlTypes
{
    public const string Text = "Text";
    public const string Number = "Number";
    public const string Decimal = "Decimal";
    public const string Currency = "Currency";
    public const string Date = "Date";
    public const string DateTime = "DateTime";
    public const string DateRange = "DateRange";
    public const string Select = "Select";
    public const string Checkbox = "Checkbox";
    public const string Radio = "Radio";
    public const string Textarea = "Textarea";
    public const string Label = "Label";

    public static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Text;
        }

        return value.Trim() switch
        {
            Text => Text,
            Number => Number,
            Decimal => Decimal,
            Currency => Currency,
            Date => Date,
            DateTime => DateTime,
            DateRange => DateRange,
            Select => Select,
            Checkbox => Checkbox,
            Radio => Radio,
            Textarea => Textarea,
            Label => Label,
            _ => Text
        };
    }
}

using System.Globalization;
using System.Text.RegularExpressions;

namespace ICP.Helpers;

public static class TariffImportErrorFormatter
{
    private static readonly Regex RowPrefix = new(
        @"^(?:【)?第\s*(?<row>[0-9]+)\s*列(?:】)?\s*(?<message>.+)$",
        RegexOptions.CultureInvariant);

    public static string Format(IEnumerable<string> errors)
    {
        var generalErrors = new List<string>();
        var seenGeneralErrors = new HashSet<string>(StringComparer.Ordinal);
        var rows = new SortedDictionary<int, List<string>>();
        var seenRowErrors = new Dictionary<int, HashSet<string>>();

        foreach (var error in errors)
        {
            if (string.IsNullOrWhiteSpace(error)) continue;
            // One source row is one output line, even if an uploaded value contains a newline.
            var text = error.Replace("\r\n", " ", StringComparison.Ordinal)
                .Replace('\r', ' ').Replace('\n', ' ').Trim();
            var match = RowPrefix.Match(text);
            if (match.Success && int.TryParse(match.Groups["row"].Value, NumberStyles.None,
                CultureInfo.InvariantCulture, out var rowNumber))
            {
                var message = match.Groups["message"].Value.Trim();
                if (!rows.TryGetValue(rowNumber, out var messages))
                {
                    messages = [];
                    rows[rowNumber] = messages;
                    seenRowErrors[rowNumber] = new HashSet<string>(StringComparer.Ordinal);
                }
                if (seenRowErrors[rowNumber].Add(message)) messages.Add(message);
            }
            else if (seenGeneralErrors.Add(text))
            {
                generalErrors.Add(text);
            }
        }

        return string.Join("\n", generalErrors.Concat(rows.Select(row =>
            $"【第{row.Key}列】{string.Join("；", row.Value)}")));
    }
}

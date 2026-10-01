using System.Reflection;
using ICP.Models.Icp;
using ICP.Models.Tariff;

namespace ICP.Helpers;

public static class TariffTableSortHelper
{
    private static readonly IReadOnlyDictionary<string, PropertyInfo> Properties =
        typeof(TariffData).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .ToDictionary(property => property.Name, StringComparer.OrdinalIgnoreCase);

    private static readonly IComparer<object?> Values = Comparer<object?>.Create(CompareValues);

    public static List<TariffData> Apply(IReadOnlyList<TariffData> rows, TariffTablePageConfig config)
    {
        var fields = config.Fields.Where(field => field.Visible).ToList();
        IOrderedEnumerable<TariffData>? ordered = null;
        foreach (var column in config.ResolveInitialSortColumns())
        {
            if (!Properties.TryGetValue(fields[(int)column[0]].FieldName, out var property))
            {
                continue;
            }

            Func<TariffData, object?> key = string.Equals(property.Name, nameof(TariffData.CreateUser), StringComparison.OrdinalIgnoreCase)
                ? row => TariffTableViewHelper.FormatCellValue(row, nameof(TariffData.CreateUser))
                : row => property.GetValue(row);
            var descending = (string)column[1] == "desc";
            ordered = ordered is null
                ? descending ? rows.OrderByDescending(key, Values) : rows.OrderBy(key, Values)
                : descending ? ordered.ThenByDescending(key, Values) : ordered.ThenBy(key, Values);
        }

        // LINQ's stable ordering retains the input order when all configured keys tie.
        return (ordered ?? rows.AsEnumerable()).ToList();
    }

    private static int CompareValues(object? left, object? right)
    {
        if (left is string || right is string)
        {
            return CompareNatural(left as string ?? string.Empty, right as string ?? string.Empty);
        }

        if (left is null) return right is null ? 0 : -1;
        if (right is null) return 1;
        return ((IComparable)left).CompareTo(right);
    }

    private static int CompareNatural(string left, string right)
    {
        var leftIndex = 0;
        var rightIndex = 0;
        while (leftIndex < left.Length && rightIndex < right.Length)
        {
            if (IsDigit(left[leftIndex]) && IsDigit(right[rightIndex]))
            {
                var leftEnd = leftIndex;
                var rightEnd = rightIndex;
                while (leftEnd < left.Length && IsDigit(left[leftEnd])) leftEnd++;
                while (rightEnd < right.Length && IsDigit(right[rightEnd])) rightEnd++;
                while (leftIndex < leftEnd && left[leftIndex] == '0') leftIndex++;
                while (rightIndex < rightEnd && right[rightIndex] == '0') rightIndex++;

                var leftLength = leftEnd - leftIndex;
                var rightLength = rightEnd - rightIndex;
                var comparison = leftLength.CompareTo(rightLength);
                if (comparison == 0)
                {
                    comparison = string.CompareOrdinal(left, leftIndex, right, rightIndex, leftLength);
                }
                if (comparison != 0) return comparison;
                leftIndex = leftEnd;
                rightIndex = rightEnd;
            }
            else
            {
                var comparison = string.Compare(left, leftIndex, right, rightIndex, 1, StringComparison.OrdinalIgnoreCase);
                if (comparison != 0) return comparison;
                leftIndex++;
                rightIndex++;
            }
        }

        return (left.Length - leftIndex).CompareTo(right.Length - rightIndex);
    }

    private static bool IsDigit(char value) => value is >= '0' and <= '9';
}

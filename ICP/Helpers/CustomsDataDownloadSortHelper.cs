using System.Reflection;
using ICP.Models.CustomsDataDownload;
using ICP.Models.Icp;

namespace ICP.Helpers;

public static class CustomsDataDownloadSortHelper
{
    private static readonly IReadOnlyDictionary<string, PropertyInfo> Properties =
        typeof(StgRawShippingAdvice).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .ToDictionary(property => property.Name, StringComparer.OrdinalIgnoreCase);

    private static readonly IComparer<object?> Values = Comparer<object?>.Create(CompareValues);

    public static List<StgRawShippingAdvice> Apply(
        IReadOnlyList<StgRawShippingAdvice> rows,
        CustomsDataDownloadTablePageConfig config)
    {
        var fields = config.Fields.Where(field => field.Visible).ToList();
        IOrderedEnumerable<StgRawShippingAdvice>? ordered = null;
        foreach (var column in config.ResolveInitialSortColumns())
        {
            if (!Properties.TryGetValue(fields[(int)column[0]].FieldName, out var property)) continue;

            Func<StgRawShippingAdvice, object?> key = row => property.GetValue(row);
            var descending = (string)column[1] == "desc";
            ordered = ordered is null
                ? descending ? rows.OrderByDescending(key, Values) : rows.OrderBy(key, Values)
                : descending ? ordered.ThenByDescending(key, Values) : ordered.ThenBy(key, Values);
        }

        return (ordered ?? rows.AsEnumerable()).ToList();
    }

    private static int CompareValues(object? left, object? right)
    {
        if (left is string || right is string)
            return CompareNatural(left as string ?? string.Empty, right as string ?? string.Empty);
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
                var result = leftLength.CompareTo(rightLength);
                if (result == 0)
                    result = string.CompareOrdinal(left, leftIndex, right, rightIndex, leftLength);
                if (result != 0) return result;
                leftIndex = leftEnd;
                rightIndex = rightEnd;
            }
            else
            {
                var result = string.Compare(left, leftIndex, right, rightIndex, 1,
                    StringComparison.OrdinalIgnoreCase);
                if (result != 0) return result;
                leftIndex++;
                rightIndex++;
            }
        }

        return (left.Length - leftIndex).CompareTo(right.Length - rightIndex);
    }

    private static bool IsDigit(char value) => value is >= '0' and <= '9';
}

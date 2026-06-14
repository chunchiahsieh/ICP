using Microsoft.EntityFrameworkCore;

namespace ICP.Helpers;

public static class SearchFilterHelper
{
    public const int FilterOptionsLimit = 500;

    public static async Task<List<string>> DistinctNonEmptyAsync(
        IQueryable<string?> selector,
        string? search,
        CancellationToken cancellationToken)
    {
        var query = selector.Where(v => v != null && v != "");

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(v => v!.Contains(search));
        }

        return await query
            .Select(v => v!)
            .Distinct()
            .OrderBy(v => v)
            .Take(FilterOptionsLimit)
            .ToListAsync(cancellationToken);
    }

    public static async Task<List<string>> DistinctIntAsync(
        IQueryable<int> selector,
        string? search,
        CancellationToken cancellationToken)
    {
        var values = await selector.Distinct().OrderBy(v => v).ToListAsync(cancellationToken);

        IEnumerable<string> formatted = values.Select(v => v.ToString());

        if (!string.IsNullOrWhiteSpace(search))
        {
            formatted = formatted.Where(v => v.Contains(search, StringComparison.OrdinalIgnoreCase));
        }

        return formatted
            .Take(FilterOptionsLimit)
            .ToList();
    }

    public static async Task<List<string>> DistinctBoolAsync(
        IQueryable<bool> selector,
        CancellationToken cancellationToken)
    {
        var values = await selector.Distinct().OrderBy(v => v).ToListAsync(cancellationToken);
        return values.Select(v => v.ToString()).ToList();
    }

    public static async Task<List<string>> DistinctDateTimeAsync(
        IQueryable<DateTime> selector,
        string? search,
        CancellationToken cancellationToken)
    {
        const string format = "yyyy-MM-dd HH:mm:ss";

        var values = await selector
            .Distinct()
            .OrderBy(v => v)
            .ToListAsync(cancellationToken);

        IEnumerable<string> formatted = values.Select(v => v.ToString(format));

        if (!string.IsNullOrWhiteSpace(search))
        {
            formatted = formatted.Where(v => v.Contains(search, StringComparison.OrdinalIgnoreCase));
        }

        return formatted
            .Take(FilterOptionsLimit)
            .ToList();
    }

    public static async Task<List<string>> DistinctNullableDateTimeAsync(
        IQueryable<DateTime?> selector,
        string? search,
        CancellationToken cancellationToken)
    {
        return await DistinctDateTimeAsync(
            selector.Where(v => v.HasValue).Select(v => v!.Value),
            search,
            cancellationToken);
    }

    public static async Task<List<string>> DistinctDateOnlyAsync(
        IQueryable<DateOnly> selector,
        string? search,
        CancellationToken cancellationToken)
    {
        const string format = "yyyy/MM/dd";

        var values = await selector
            .Distinct()
            .OrderBy(v => v)
            .ToListAsync(cancellationToken);

        IEnumerable<string> formatted = values.Select(v => v.ToString(format));

        if (!string.IsNullOrWhiteSpace(search))
        {
            formatted = formatted.Where(v => v.Contains(search, StringComparison.OrdinalIgnoreCase));
        }

        return formatted
            .Take(FilterOptionsLimit)
            .ToList();
    }

    public static List<DateOnly> ParseDateOnlyValues(IReadOnlyList<string> values)
    {
        return values
            .Select(v => DateOnly.TryParse(v, out var parsed) ? (DateOnly?)parsed : null)
            .Where(v => v.HasValue)
            .Select(v => v!.Value)
            .Distinct()
            .ToList();
    }

    public static List<bool> ParseBoolValues(IReadOnlyList<string> values)
    {
        return values
            .Select(v => bool.TryParse(v, out var parsed) ? (bool?)parsed : null)
            .Where(v => v.HasValue)
            .Select(v => v!.Value)
            .Distinct()
            .ToList();
    }

    public static List<int> ParseIntValues(IReadOnlyList<string> values)
    {
        return values
            .Select(v => int.TryParse(v, out var parsed) ? (int?)parsed : null)
            .Where(v => v.HasValue)
            .Select(v => v!.Value)
            .Distinct()
            .ToList();
    }
}

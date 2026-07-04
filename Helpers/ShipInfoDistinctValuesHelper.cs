using System.Globalization;
using System.Reflection;
using ICP.Models.Icp;
using Microsoft.EntityFrameworkCore;

namespace ICP.Helpers;

public static class ShipInfoDistinctValuesHelper
{
    private const int Limit = 200;

    private static readonly Dictionary<string, PropertyInfo> HeaderProperties =
        typeof(IcpHeader).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .ToDictionary(x => x.Name, x => x, StringComparer.OrdinalIgnoreCase);

    private static readonly Dictionary<string, PropertyInfo> DetailProperties =
        typeof(IcpDetail).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .ToDictionary(x => x.Name, x => x, StringComparer.OrdinalIgnoreCase);

    public static Task<IReadOnlyList<string>> GetHeaderDistinctValuesAsync(
        IQueryable<IcpHeader> query,
        string column,
        string? search,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(column))
        {
            return Task.FromResult<IReadOnlyList<string>>([]);
        }

        var term = search?.Trim();

        if (column.Equals("Status", StringComparison.OrdinalIgnoreCase))
        {
            return GetHeaderStatusDistinctAsync(query, term, cancellationToken);
        }

        if (IsCaseStatusColumn(column))
        {
            return GetCaseStatusDistinctAsync(query, column, term, cancellationToken);
        }

        if (!HeaderProperties.TryGetValue(column, out var property))
        {
            return Task.FromResult<IReadOnlyList<string>>([]);
        }

        return GetDistinctByPropertyTypeAsync(query, column, property, term, cancellationToken);
    }

    public static Task<IReadOnlyList<string>> GetDetailDistinctValuesAsync(
        IQueryable<IcpDetail> query,
        string column,
        string? search,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(column))
        {
            return Task.FromResult<IReadOnlyList<string>>([]);
        }

        var term = search?.Trim();

        if (IsCaseStatusColumn(column))
        {
            return GetCaseStatusDistinctAsync(query, column, term, cancellationToken);
        }

        if (!DetailProperties.TryGetValue(column, out var property))
        {
            return Task.FromResult<IReadOnlyList<string>>([]);
        }

        return GetDistinctByPropertyTypeAsync(query, column, property, term, cancellationToken);
    }

    private static bool IsCaseStatusColumn(string column) =>
        column.Equals("DepositCaseStatus", StringComparison.OrdinalIgnoreCase)
        || column.Equals("ArurCaseStatus", StringComparison.OrdinalIgnoreCase);

    private static async Task<IReadOnlyList<string>> GetHeaderStatusDistinctAsync(
        IQueryable<IcpHeader> query,
        string? search,
        CancellationToken cancellationToken)
    {
        var headers = await query.ToListAsync(cancellationToken);
        var statuses = headers
            .Select(ShipInfoStatusResolver.Resolve)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x)
            .AsEnumerable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            statuses = statuses.Where(x => ShipInfoStatusResolver.MatchesSearch(x, search));
        }

        return statuses.Take(Limit).ToList();
    }

    private static async Task<IReadOnlyList<string>> GetCaseStatusDistinctAsync<TEntity>(
        IQueryable<TEntity> query,
        string column,
        string? search,
        CancellationToken cancellationToken)
        where TEntity : class
    {
        var rawValues = await DistinctStringColumnAsync(
            query.Select(entity => EF.Property<string?>(entity, column)),
            null,
            cancellationToken);

        var statuses = rawValues
            .Select(ShipInfoCaseStatusResolver.Normalize)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x)
            .AsEnumerable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            statuses = statuses.Where(x => ShipInfoCaseStatusResolver.MatchesSearch(x, search));
        }

        return statuses.Take(Limit).ToList();
    }

    private static Task<IReadOnlyList<string>> GetDistinctByPropertyTypeAsync<TEntity>(
        IQueryable<TEntity> query,
        string column,
        PropertyInfo property,
        string? search,
        CancellationToken cancellationToken)
        where TEntity : class
    {
        var propertyType = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;

        if (propertyType == typeof(string))
        {
            return DistinctStringColumnAsync(
                query.Select(entity => EF.Property<string?>(entity, column)),
                search,
                cancellationToken);
        }

        if (propertyType == typeof(double))
        {
            return DistinctDoubleColumnAsync(
                query.Select(entity => EF.Property<double?>(entity, column)),
                search,
                cancellationToken);
        }

        if (propertyType == typeof(decimal))
        {
            return DistinctDecimalColumnAsync(
                query.Select(entity => EF.Property<decimal?>(entity, column)),
                search,
                cancellationToken);
        }

        if (propertyType == typeof(int))
        {
            return DistinctIntColumnAsync(
                query.Select(entity => EF.Property<int?>(entity, column)),
                search,
                cancellationToken);
        }

        if (propertyType == typeof(long))
        {
            return DistinctLongColumnAsync(
                query.Select(entity => EF.Property<long?>(entity, column)),
                search,
                cancellationToken);
        }

        if (propertyType == typeof(float))
        {
            return DistinctFloatColumnAsync(
                query.Select(entity => EF.Property<float?>(entity, column)),
                search,
                cancellationToken);
        }

        return Task.FromResult<IReadOnlyList<string>>([]);
    }

    private static async Task<IReadOnlyList<string>> DistinctStringColumnAsync(
        IQueryable<string?> source,
        string? search,
        CancellationToken cancellationToken)
    {
        var query = source.Where(x => x != null && x != string.Empty);
        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(x => x!.Contains(search));
        }

        return await query
            .Select(x => x!)
            .Distinct()
            .OrderBy(x => x)
            .Take(Limit)
            .ToListAsync(cancellationToken);
    }

    private static async Task<IReadOnlyList<string>> DistinctDoubleColumnAsync(
        IQueryable<double?> source,
        string? search,
        CancellationToken cancellationToken) =>
        await DistinctNumericColumnAsync(
            source.Where(x => x != null).Select(x => x!.Value),
            search,
            cancellationToken);

    private static async Task<IReadOnlyList<string>> DistinctDecimalColumnAsync(
        IQueryable<decimal?> source,
        string? search,
        CancellationToken cancellationToken) =>
        await DistinctNumericColumnAsync(
            source.Where(x => x != null).Select(x => x!.Value),
            search,
            cancellationToken);

    private static async Task<IReadOnlyList<string>> DistinctIntColumnAsync(
        IQueryable<int?> source,
        string? search,
        CancellationToken cancellationToken) =>
        await DistinctNumericColumnAsync(
            source.Where(x => x != null).Select(x => x!.Value),
            search,
            cancellationToken);

    private static async Task<IReadOnlyList<string>> DistinctLongColumnAsync(
        IQueryable<long?> source,
        string? search,
        CancellationToken cancellationToken) =>
        await DistinctNumericColumnAsync(
            source.Where(x => x != null).Select(x => x!.Value),
            search,
            cancellationToken);

    private static async Task<IReadOnlyList<string>> DistinctFloatColumnAsync(
        IQueryable<float?> source,
        string? search,
        CancellationToken cancellationToken) =>
        await DistinctNumericColumnAsync(
            source.Where(x => x != null).Select(x => x!.Value),
            search,
            cancellationToken);

    private static async Task<IReadOnlyList<string>> DistinctNumericColumnAsync<TValue>(
        IQueryable<TValue> source,
        string? search,
        CancellationToken cancellationToken)
        where TValue : struct
    {
        var values = await source
            .Distinct()
            .OrderBy(x => x)
            .Take(Limit)
            .ToListAsync(cancellationToken);

        var strings = values
            .Select(x => Convert.ToString(x, CultureInfo.InvariantCulture) ?? string.Empty)
            .AsEnumerable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            strings = strings.Where(x => x.Contains(search, StringComparison.OrdinalIgnoreCase));
        }

        return strings.ToList();
    }
}

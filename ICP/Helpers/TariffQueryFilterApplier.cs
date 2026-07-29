using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;
using ICP.Models.Icp;
using ICP.Models.ShipInfo;
using ICP.Models.Tariff;
using Microsoft.EntityFrameworkCore;

namespace ICP.Helpers;

public static class TariffQueryFilterApplier
{
    private static readonly Dictionary<string, PropertyInfo> Properties =
        typeof(TariffData).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .ToDictionary(x => x.Name, x => x, StringComparer.OrdinalIgnoreCase);

    public static IQueryable<TariffData> ApplyFilters(
        IQueryable<TariffData> query,
        TariffDataQueryModel criteria,
        IReadOnlyList<TariffTableFieldMetadata> fields)
    {
        var searchable = fields
            .Where(field => field.Searchable && !TariffMetadataHelper.IsVirtualField(field.FieldName))
            .ToDictionary(field => field.FieldName, field => field, StringComparer.OrdinalIgnoreCase);

        foreach (var (fieldName, values) in criteria.Checkbox)
        {
            if (values.Count == 0 || !searchable.TryGetValue(fieldName, out var meta))
            {
                continue;
            }

            if (!string.Equals(TariffMetadataHelper.ResolveFilterType(meta), ShipInfoFilterTypes.Checkbox, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!Properties.TryGetValue(fieldName, out _))
            {
                continue;
            }

            query = ApplyCheckboxFilter(query, fieldName, values);
        }

        foreach (var (fieldName, term) in criteria.Text)
        {
            if (string.IsNullOrWhiteSpace(term) || !searchable.TryGetValue(fieldName, out var meta))
            {
                continue;
            }

            if (!string.Equals(TariffMetadataHelper.ResolveFilterType(meta), ShipInfoFilterTypes.Text, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!Properties.TryGetValue(fieldName, out _))
            {
                continue;
            }

            query = ApplyTextFilter(query, fieldName, term.Trim());
        }

        foreach (var (fieldName, fromValue) in criteria.DateFrom)
        {
            if (string.IsNullOrWhiteSpace(fromValue) || !searchable.TryGetValue(fieldName, out var meta))
            {
                continue;
            }

            if (!string.Equals(TariffMetadataHelper.ResolveFilterType(meta), ShipInfoFilterTypes.DateRange, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!Properties.TryGetValue(fieldName, out _))
            {
                continue;
            }

            query = ApplyDateFromFilter(query, fieldName, fromValue.Trim());
        }

        foreach (var (fieldName, toValue) in criteria.DateTo)
        {
            if (string.IsNullOrWhiteSpace(toValue) || !searchable.TryGetValue(fieldName, out var meta))
            {
                continue;
            }

            if (!string.Equals(TariffMetadataHelper.ResolveFilterType(meta), ShipInfoFilterTypes.DateRange, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!Properties.TryGetValue(fieldName, out _))
            {
                continue;
            }

            query = ApplyDateToFilter(query, fieldName, toValue.Trim());
        }

        foreach (var (fieldName, dateValue) in criteria.Date)
        {
            if (string.IsNullOrWhiteSpace(dateValue) || !searchable.TryGetValue(fieldName, out var meta))
            {
                continue;
            }

            if (!string.Equals(TariffMetadataHelper.ResolveFilterType(meta), ShipInfoFilterTypes.Date, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!Properties.TryGetValue(fieldName, out _))
            {
                continue;
            }

            query = ApplyExactDateFilter(query, fieldName, dateValue.Trim());
        }

        return query;
    }

    private static IQueryable<TariffData> ApplyCheckboxFilter(
        IQueryable<TariffData> query,
        string fieldName,
        List<string> values)
    {
        if (!Properties.TryGetValue(fieldName, out var property))
        {
            return query;
        }

        var propertyType = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
        if (propertyType == typeof(string))
        {
            return ApplyStringInFilter(query, fieldName, values);
        }

        if (propertyType == typeof(DateOnly))
        {
            var dates = SearchFilterHelper.ParseDateOnlyValues(values);
            return dates.Count == 0
                ? query
                : ApplyDateOnlyInFilter(query, fieldName, dates);
        }

        return query;
    }

    private static IQueryable<TariffData> ApplyTextFilter(
        IQueryable<TariffData> query,
        string fieldName,
        string term)
    {
        if (!Properties.TryGetValue(fieldName, out var property))
        {
            return query;
        }

        var propertyType = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
        if (propertyType == typeof(string))
        {
            return query.Where(BuildStringLikeExpression(fieldName, term));
        }

        return query;
    }

    private static IQueryable<TariffData> ApplyDateFromFilter(
        IQueryable<TariffData> query,
        string fieldName,
        string fromValue)
    {
        if (!TryParseDateOnly(fromValue, out var fromDate))
        {
            return query;
        }

        return query.Where(BuildDateOnlyCompareExpression(fieldName, fromDate, greaterOrEqual: true));
    }

    private static IQueryable<TariffData> ApplyDateToFilter(
        IQueryable<TariffData> query,
        string fieldName,
        string toValue)
    {
        if (!TryParseDateOnly(toValue, out var toDate))
        {
            return query;
        }

        return query.Where(BuildDateOnlyCompareExpression(fieldName, toDate, greaterOrEqual: false));
    }

    private static IQueryable<TariffData> ApplyExactDateFilter(
        IQueryable<TariffData> query,
        string fieldName,
        string dateValue)
    {
        if (!TryParseDateOnly(dateValue, out var date))
        {
            return query;
        }

        return ApplyDateOnlyInFilter(query, fieldName, [date]);
    }

    private static IQueryable<TariffData> ApplyStringInFilter(
        IQueryable<TariffData> query,
        string fieldName,
        List<string> values)
    {
        var parameter = Expression.Parameter(typeof(TariffData), "entity");
        var property = Expression.Property(parameter, fieldName);
        var notNull = Expression.NotEqual(property, Expression.Constant(null, typeof(string)));
        var containsMethod = typeof(List<string>).GetMethod(nameof(List<string>.Contains), [typeof(string)])!;
        var callContains = Expression.Call(Expression.Constant(values), containsMethod, property);
        var body = Expression.AndAlso(notNull, callContains);
        var lambda = Expression.Lambda<Func<TariffData, bool>>(body, parameter);
        return query.Where(lambda);
    }

    private static IQueryable<TariffData> ApplyDateOnlyInFilter(
        IQueryable<TariffData> query,
        string fieldName,
        List<DateOnly> dates)
    {
        var parameter = Expression.Parameter(typeof(TariffData), "entity");
        var property = Expression.Property(parameter, fieldName);
        var containsMethod = typeof(List<DateOnly>).GetMethod(nameof(List<DateOnly>.Contains), [typeof(DateOnly)])!;
        var callContains = Expression.Call(Expression.Constant(dates), containsMethod, property);
        var lambda = Expression.Lambda<Func<TariffData, bool>>(callContains, parameter);
        return query.Where(lambda);
    }

    private static Expression<Func<TariffData, bool>> BuildStringLikeExpression(string fieldName, string term)
    {
        var parameter = Expression.Parameter(typeof(TariffData), "entity");
        var property = Expression.Property(parameter, fieldName);
        var notNull = Expression.NotEqual(property, Expression.Constant(null, typeof(string)));
        var efFunctions = Expression.Property(null, typeof(EF), nameof(EF.Functions));
        var likeMethod = typeof(DbFunctionsExtensions).GetMethod(
            nameof(DbFunctionsExtensions.Like),
            [typeof(DbFunctions), typeof(string), typeof(string)])!;
        var pattern = Expression.Constant($"%{term}%");
        var likeCall = Expression.Call(likeMethod, efFunctions, property, pattern);
        var body = Expression.AndAlso(notNull, likeCall);
        return Expression.Lambda<Func<TariffData, bool>>(body, parameter);
    }

    private static Expression<Func<TariffData, bool>> BuildDateOnlyCompareExpression(
        string fieldName,
        DateOnly compareValue,
        bool greaterOrEqual)
    {
        var parameter = Expression.Parameter(typeof(TariffData), "entity");
        var property = Expression.Property(parameter, fieldName);
        var constant = Expression.Constant(compareValue);
        var compare = greaterOrEqual
            ? Expression.GreaterThanOrEqual(property, constant)
            : Expression.LessThanOrEqual(property, constant);
        return Expression.Lambda<Func<TariffData, bool>>(compare, parameter);
    }

    private static bool TryParseDateOnly(string value, out DateOnly date) =>
        DateOnly.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out date)
        || DateOnly.TryParse(value, CultureInfo.CurrentCulture, DateTimeStyles.None, out date);
}

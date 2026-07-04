using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;
using ICP.Models.Icp;
using ICP.Models.ShipInfo;
using Microsoft.EntityFrameworkCore;

namespace ICP.Helpers;

public static class ShipInfoQueryFilterApplier
{
    private static readonly Dictionary<string, PropertyInfo> HeaderProperties =
        typeof(IcpHeader).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .ToDictionary(x => x.Name, x => x, StringComparer.OrdinalIgnoreCase);

    private static readonly Dictionary<string, PropertyInfo> DetailProperties =
        typeof(IcpDetail).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .ToDictionary(x => x.Name, x => x, StringComparer.OrdinalIgnoreCase);

    public static IQueryable<IcpHeader> ApplyHeaderFilters(
        IQueryable<IcpHeader> query,
        ShipInfoHeaderQueryModel criteria,
        IReadOnlyList<ShipInfoFieldMetadata> fields) =>
        ApplyFilters(query, criteria, fields, HeaderProperties, ApplyHeaderStatusCheckbox, isHeader: true);

    public static IQueryable<IcpDetail> ApplyDetailFilters(
        IQueryable<IcpDetail> query,
        ShipInfoDetailQueryModel criteria,
        IReadOnlyList<ShipInfoFieldMetadata> fields) =>
        ApplyDetailFiltersInternal(query, criteria, fields, DetailProperties);

    private static IQueryable<IcpDetail> ApplyDetailFiltersInternal(
        IQueryable<IcpDetail> query,
        ShipInfoDetailQueryModel criteria,
        IReadOnlyList<ShipInfoFieldMetadata> fields,
        IReadOnlyDictionary<string, PropertyInfo> properties) =>
        ApplyFilters(
            query,
            new ShipInfoHeaderQueryModel
            {
                Checkbox = criteria.Checkbox,
                Text = criteria.Text,
                DateFrom = criteria.DateFrom,
                DateTo = criteria.DateTo,
                Date = criteria.Date
            },
            fields,
            properties,
            null,
            isHeader: false);

    private static IQueryable<TEntity> ApplyFilters<TEntity>(
        IQueryable<TEntity> query,
        ShipInfoHeaderQueryModel criteria,
        IReadOnlyList<ShipInfoFieldMetadata> fields,
        IReadOnlyDictionary<string, PropertyInfo> properties,
        Func<IQueryable<TEntity>, List<string>, IQueryable<TEntity>>? statusCheckboxHandler,
        bool isHeader)
        where TEntity : class
    {
        var searchable = fields
            .Where(x => x.Searchable)
            .ToDictionary(x => x.FieldName, x => x, StringComparer.OrdinalIgnoreCase);

        foreach (var (fieldName, values) in criteria.Checkbox)
        {
            if (values.Count == 0 || !searchable.TryGetValue(fieldName, out var meta))
            {
                continue;
            }

            if (!string.Equals(ShipInfoMetadataHelper.ResolveFilterType(meta), ShipInfoFilterTypes.Checkbox, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (statusCheckboxHandler != null
                && string.Equals(fieldName, "Status", StringComparison.OrdinalIgnoreCase))
            {
                query = statusCheckboxHandler(query, values);
                continue;
            }

            var entityColumn = ShipInfoFieldBinding.ResolveEntityPropertyName(meta, isHeader);
            if (string.IsNullOrWhiteSpace(entityColumn))
            {
                continue;
            }

            query = ApplyCheckboxFilter(query, entityColumn, values, properties);
        }

        foreach (var (fieldName, term) in criteria.Text)
        {
            if (string.IsNullOrWhiteSpace(term) || !searchable.TryGetValue(fieldName, out var meta))
            {
                continue;
            }

            if (!string.Equals(ShipInfoMetadataHelper.ResolveFilterType(meta), ShipInfoFilterTypes.Text, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var entityColumn = ShipInfoFieldBinding.ResolveEntityPropertyName(meta, isHeader);
            if (string.IsNullOrWhiteSpace(entityColumn))
            {
                continue;
            }

            query = ApplyTextFilter(query, entityColumn, term.Trim(), properties);
        }

        foreach (var (fieldName, fromValue) in criteria.DateFrom)
        {
            if (string.IsNullOrWhiteSpace(fromValue) || !searchable.TryGetValue(fieldName, out var meta))
            {
                continue;
            }

            if (!string.Equals(ShipInfoMetadataHelper.ResolveFilterType(meta), ShipInfoFilterTypes.DateRange, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var entityColumn = ShipInfoFieldBinding.ResolveEntityPropertyName(meta, isHeader);
            if (string.IsNullOrWhiteSpace(entityColumn))
            {
                continue;
            }

            query = ApplyDateFromFilter(query, entityColumn, fromValue.Trim(), properties);
        }

        foreach (var (fieldName, toValue) in criteria.DateTo)
        {
            if (string.IsNullOrWhiteSpace(toValue) || !searchable.TryGetValue(fieldName, out var meta))
            {
                continue;
            }

            if (!string.Equals(ShipInfoMetadataHelper.ResolveFilterType(meta), ShipInfoFilterTypes.DateRange, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var entityColumn = ShipInfoFieldBinding.ResolveEntityPropertyName(meta, isHeader);
            if (string.IsNullOrWhiteSpace(entityColumn))
            {
                continue;
            }

            query = ApplyDateToFilter(query, entityColumn, toValue.Trim(), properties);
        }

        foreach (var (fieldName, dateValue) in criteria.Date)
        {
            if (string.IsNullOrWhiteSpace(dateValue) || !searchable.TryGetValue(fieldName, out var meta))
            {
                continue;
            }

            if (!string.Equals(ShipInfoMetadataHelper.ResolveFilterType(meta), ShipInfoFilterTypes.Date, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var entityColumn = ShipInfoFieldBinding.ResolveEntityPropertyName(meta, isHeader);
            if (string.IsNullOrWhiteSpace(entityColumn))
            {
                continue;
            }

            query = ApplyExactDateFilter(query, entityColumn, dateValue.Trim(), properties);
        }

        return query;
    }

    private static IQueryable<IcpHeader> ApplyHeaderStatusCheckbox(
        IQueryable<IcpHeader> query,
        List<string> values)
    {
        var normalizedStatuses = values
            .Select(ShipInfoStatusResolver.Normalize)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return query.Where(x =>
            (x.Status != null && x.Status != "" && normalizedStatuses.Contains(x.Status))
            || ((x.Status == null || x.Status == "")
                && normalizedStatuses.Contains(ShipInfoStatuses.Cancelled)
                && x.Cancellation != null && x.Cancellation != "")
            || ((x.Status == null || x.Status == "")
                && normalizedStatuses.Contains(ShipInfoStatuses.Processing)
                && (x.Cancellation == null || x.Cancellation == "")));
    }

    private static IQueryable<TEntity> ApplyCheckboxFilter<TEntity>(
        IQueryable<TEntity> query,
        string fieldName,
        List<string> values,
        IReadOnlyDictionary<string, PropertyInfo> properties)
    {
        if (!properties.TryGetValue(fieldName, out var property))
        {
            return query;
        }

        var propertyType = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
        if (propertyType == typeof(string))
        {
            return ApplyStringInFilter(query, fieldName, values);
        }

        if (propertyType == typeof(double))
        {
            var parsed = ParseValues(values, double.Parse);
            return parsed.Count == 0
                ? query
                : query.Where(BuildNullableContainsExpression<TEntity, double>(fieldName, parsed));
        }

        if (propertyType == typeof(decimal))
        {
            var parsed = ParseValues(values, decimal.Parse);
            return parsed.Count == 0
                ? query
                : query.Where(BuildNullableContainsExpression<TEntity, decimal>(fieldName, parsed));
        }

        if (propertyType == typeof(int))
        {
            var parsed = ParseValues(values, int.Parse);
            return parsed.Count == 0
                ? query
                : query.Where(BuildNullableContainsExpression<TEntity, int>(fieldName, parsed));
        }

        return query;
    }

    private static IQueryable<TEntity> ApplyTextFilter<TEntity>(
        IQueryable<TEntity> query,
        string fieldName,
        string term,
        IReadOnlyDictionary<string, PropertyInfo> properties)
    {
        if (!properties.TryGetValue(fieldName, out var property))
        {
            return query;
        }

        var propertyType = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
        if (propertyType == typeof(string))
        {
            return query.Where(BuildStringLikeExpression<TEntity>(fieldName, term));
        }

        if (propertyType == typeof(double) || propertyType == typeof(decimal) || propertyType == typeof(int))
        {
            return query.Where(BuildNumericTextExpression<TEntity>(fieldName, term, propertyType));
        }

        return query;
    }

    private static IQueryable<TEntity> ApplyDateFromFilter<TEntity>(
        IQueryable<TEntity> query,
        string fieldName,
        string fromValue,
        IReadOnlyDictionary<string, PropertyInfo> properties)
    {
        if (!properties.TryGetValue(fieldName, out var property))
        {
            return query;
        }

        var propertyType = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
        if (propertyType != typeof(string))
        {
            return query;
        }

        if (!IsValidDateFilterValue(fromValue))
        {
            return query;
        }

        return query.Where(BuildStringCompareExpression<TEntity>(fieldName, fromValue, compareGreaterOrEqual: true));
    }

    private static IQueryable<TEntity> ApplyDateToFilter<TEntity>(
        IQueryable<TEntity> query,
        string fieldName,
        string toValue,
        IReadOnlyDictionary<string, PropertyInfo> properties)
    {
        if (!properties.TryGetValue(fieldName, out var property))
        {
            return query;
        }

        var propertyType = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
        if (propertyType != typeof(string))
        {
            return query;
        }

        if (!IsValidDateFilterValue(toValue))
        {
            return query;
        }

        return query.Where(BuildStringCompareExpression<TEntity>(fieldName, toValue, compareGreaterOrEqual: false));
    }

    private static IQueryable<TEntity> ApplyExactDateFilter<TEntity>(
        IQueryable<TEntity> query,
        string fieldName,
        string dateValue,
        IReadOnlyDictionary<string, PropertyInfo> properties)
    {
        if (!properties.TryGetValue(fieldName, out var property))
        {
            return query;
        }

        var propertyType = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
        if (propertyType != typeof(string))
        {
            return query;
        }

        return ApplyStringInFilter(query, fieldName, [dateValue]);
    }

    private static IQueryable<TEntity> ApplyStringInFilter<TEntity>(
        IQueryable<TEntity> query,
        string fieldName,
        List<string> values)
    {
        var parameter = Expression.Parameter(typeof(TEntity), "entity");
        var property = Expression.Property(parameter, fieldName);
        var notNull = Expression.NotEqual(property, Expression.Constant(null, typeof(string)));
        var containsMethod = typeof(List<string>).GetMethod(nameof(List<string>.Contains), [typeof(string)])!;
        var callContains = Expression.Call(Expression.Constant(values), containsMethod, property);
        var body = Expression.AndAlso(notNull, callContains);
        var lambda = Expression.Lambda<Func<TEntity, bool>>(body, parameter);
        return query.Where(lambda);
    }

    private static Expression<Func<TEntity, bool>> BuildStringLikeExpression<TEntity>(string fieldName, string term)
    {
        var parameter = Expression.Parameter(typeof(TEntity), "entity");
        var property = Expression.Property(parameter, fieldName);
        var notNull = Expression.NotEqual(property, Expression.Constant(null, typeof(string)));
        var efFunctions = Expression.Property(null, typeof(EF), nameof(EF.Functions));
        var likeMethod = typeof(DbFunctionsExtensions).GetMethod(
            nameof(DbFunctionsExtensions.Like),
            [typeof(DbFunctions), typeof(string), typeof(string)])!;
        var pattern = Expression.Constant($"%{term}%");
        var likeCall = Expression.Call(likeMethod, efFunctions, property, pattern);
        var body = Expression.AndAlso(notNull, likeCall);
        return Expression.Lambda<Func<TEntity, bool>>(body, parameter);
    }

    private static Expression<Func<TEntity, bool>> BuildStringCompareExpression<TEntity>(
        string fieldName,
        string compareValue,
        bool compareGreaterOrEqual)
    {
        var parameter = Expression.Parameter(typeof(TEntity), "entity");
        var property = Expression.Property(parameter, fieldName);
        var notNull = Expression.NotEqual(property, Expression.Constant(null, typeof(string)));
        var compareConstant = Expression.Constant(compareValue, typeof(string));
        var compareToMethod = typeof(string).GetMethod(nameof(string.CompareTo), [typeof(string)])!;
        var compareToCall = Expression.Call(property, compareToMethod, compareConstant);
        var zero = Expression.Constant(0);
        var compare = compareGreaterOrEqual
            ? Expression.GreaterThanOrEqual(compareToCall, zero)
            : Expression.LessThanOrEqual(compareToCall, zero);
        var body = Expression.AndAlso(notNull, compare);
        return Expression.Lambda<Func<TEntity, bool>>(body, parameter);
    }

    private static bool IsValidDateFilterValue(string value) =>
        DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out _)
        || DateTime.TryParse(value, CultureInfo.CurrentCulture, DateTimeStyles.AssumeLocal, out _);

    private static Expression<Func<TEntity, bool>> BuildNullableContainsExpression<TEntity, TValue>(
        string fieldName,
        List<TValue> values)
        where TValue : struct
    {
        var parameter = Expression.Parameter(typeof(TEntity), "entity");
        var property = Expression.Property(parameter, fieldName);
        var hasValue = Expression.Property(property, nameof(Nullable<int>.HasValue));
        var valueProperty = Expression.Property(property, nameof(Nullable<int>.Value));
        var containsMethod = typeof(List<TValue>).GetMethod(nameof(List<TValue>.Contains), [typeof(TValue)])!;
        var callContains = Expression.Call(Expression.Constant(values), containsMethod, valueProperty);
        var body = Expression.AndAlso(hasValue, callContains);
        return Expression.Lambda<Func<TEntity, bool>>(body, parameter);
    }

    private static Expression<Func<TEntity, bool>> BuildNumericTextExpression<TEntity>(
        string fieldName,
        string term,
        Type propertyType)
    {
        var parameter = Expression.Parameter(typeof(TEntity), "entity");
        var property = Expression.Property(parameter, fieldName);
        var hasValue = Expression.Property(property, nameof(Nullable<int>.HasValue));
        var valueProperty = Expression.Property(property, nameof(Nullable<int>.Value));
        var toString = Expression.Call(valueProperty, nameof(object.ToString), Type.EmptyTypes);
        var efFunctions = Expression.Property(null, typeof(EF), nameof(EF.Functions));
        var likeMethod = typeof(DbFunctionsExtensions).GetMethod(
            nameof(DbFunctionsExtensions.Like),
            [typeof(DbFunctions), typeof(string), typeof(string)])!;
        var pattern = Expression.Constant($"%{term}%");
        var likeCall = Expression.Call(likeMethod, efFunctions, toString, pattern);
        var body = Expression.AndAlso(hasValue, likeCall);
        return Expression.Lambda<Func<TEntity, bool>>(body, parameter);
    }

    private static List<TValue> ParseValues<TValue>(IEnumerable<string> values, Func<string, IFormatProvider, TValue> parser)
        where TValue : struct
    {
        var result = new List<TValue>();
        foreach (var value in values)
        {
            try
            {
                result.Add(parser(value, CultureInfo.InvariantCulture));
            }
            catch
            {
                // Ignore invalid numeric filter values.
            }
        }

        return result;
    }
}

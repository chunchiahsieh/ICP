using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;
using ICP.Models.CustomsDataDownload;
using ICP.Models.Icp;
using ICP.Models.ShipInfo;
using Microsoft.EntityFrameworkCore;

namespace ICP.Helpers;

public static class CustomsDataDownloadQueryFilterApplier
{
    private static readonly Dictionary<string, PropertyInfo> Properties =
        typeof(StgRawShippingAdvice).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .ToDictionary(x => x.Name, x => x, StringComparer.OrdinalIgnoreCase);

    public static IQueryable<StgRawShippingAdvice> ApplyFilters(
        IQueryable<StgRawShippingAdvice> query,
        CustomsDataDownloadQueryModel criteria,
        IReadOnlyList<CustomsDataDownloadTableFieldMetadata> fields)
    {
        var searchable = fields
            .Where(field => field.Searchable)
            .ToDictionary(field => field.FieldName, field => field, StringComparer.OrdinalIgnoreCase);

        foreach (var (fieldName, values) in criteria.Checkbox)
        {
            if (values.Count == 0
                || !searchable.TryGetValue(fieldName, out var meta)
                || !string.Equals(CustomsDataDownloadMetadataHelper.ResolveFilterType(meta), ShipInfoFilterTypes.Checkbox, StringComparison.OrdinalIgnoreCase)
                || !Properties.ContainsKey(fieldName))
            {
                continue;
            }

            query = ApplyCheckboxFilter(query, fieldName, values);
        }

        foreach (var (fieldName, term) in criteria.Text)
        {
            if (string.IsNullOrWhiteSpace(term)
                || !searchable.TryGetValue(fieldName, out var meta)
                || !string.Equals(CustomsDataDownloadMetadataHelper.ResolveFilterType(meta), ShipInfoFilterTypes.Text, StringComparison.OrdinalIgnoreCase)
                || !Properties.ContainsKey(fieldName))
            {
                continue;
            }

            query = ApplyTextFilter(query, fieldName, term.Trim());
        }

        foreach (var (fieldName, fromValue) in criteria.DateFrom)
        {
            if (string.IsNullOrWhiteSpace(fromValue)
                || !searchable.TryGetValue(fieldName, out var meta)
                || !string.Equals(CustomsDataDownloadMetadataHelper.ResolveFilterType(meta), ShipInfoFilterTypes.DateRange, StringComparison.OrdinalIgnoreCase)
                || !Properties.ContainsKey(fieldName))
            {
                continue;
            }

            query = ApplyDateFromFilter(query, fieldName, fromValue.Trim());
        }

        foreach (var (fieldName, toValue) in criteria.DateTo)
        {
            if (string.IsNullOrWhiteSpace(toValue)
                || !searchable.TryGetValue(fieldName, out var meta)
                || !string.Equals(CustomsDataDownloadMetadataHelper.ResolveFilterType(meta), ShipInfoFilterTypes.DateRange, StringComparison.OrdinalIgnoreCase)
                || !Properties.ContainsKey(fieldName))
            {
                continue;
            }

            query = ApplyDateToFilter(query, fieldName, toValue.Trim());
        }

        foreach (var (fieldName, dateValue) in criteria.Date)
        {
            if (string.IsNullOrWhiteSpace(dateValue)
                || !searchable.TryGetValue(fieldName, out var meta)
                || !string.Equals(CustomsDataDownloadMetadataHelper.ResolveFilterType(meta), ShipInfoFilterTypes.Date, StringComparison.OrdinalIgnoreCase)
                || !Properties.ContainsKey(fieldName))
            {
                continue;
            }

            query = ApplyExactDateFilter(query, fieldName, dateValue.Trim());
        }

        return query;
    }

    private static IQueryable<StgRawShippingAdvice> ApplyCheckboxFilter(
        IQueryable<StgRawShippingAdvice> query,
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

        return query;
    }

    private static IQueryable<StgRawShippingAdvice> ApplyTextFilter(
        IQueryable<StgRawShippingAdvice> query,
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

        if (propertyType == typeof(Guid))
        {
            if (Guid.TryParse(term, out var guid))
            {
                return ApplyEqualsFilter(query, fieldName, guid, property.PropertyType);
            }

            return query.Where(BuildGuidToStringLikeExpression(fieldName, term));
        }

        if (propertyType == typeof(int)
            && int.TryParse(term, NumberStyles.Integer, CultureInfo.InvariantCulture, out var intValue))
        {
            return ApplyEqualsFilter(query, fieldName, intValue, property.PropertyType);
        }

        if (propertyType == typeof(decimal)
            && decimal.TryParse(term, NumberStyles.Number, CultureInfo.InvariantCulture, out var decimalValue))
        {
            return ApplyEqualsFilter(query, fieldName, decimalValue, property.PropertyType);
        }

        if ((propertyType == typeof(double) || propertyType == typeof(float))
            && double.TryParse(term, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var doubleValue))
        {
            return ApplyEqualsFilter(query, fieldName, Convert.ChangeType(doubleValue, propertyType, CultureInfo.InvariantCulture), property.PropertyType);
        }

        return query;
    }

    private static IQueryable<StgRawShippingAdvice> ApplyDateFromFilter(
        IQueryable<StgRawShippingAdvice> query,
        string fieldName,
        string fromValue)
    {
        if (!TryParseDateOnly(fromValue, out var fromDate))
        {
            return query;
        }

        if (!Properties.TryGetValue(fieldName, out var property))
        {
            return query;
        }

        var propertyType = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
        if (propertyType == typeof(string))
        {
            return query.Where(BuildStringCompareExpression(fieldName, fromDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), greaterOrEqual: true));
        }

        if (propertyType == typeof(DateTime))
        {
            var fromDateTime = fromDate.ToDateTime(TimeOnly.MinValue);
            return query.Where(BuildDateTimeCompareExpression(fieldName, fromDateTime, greaterOrEqual: true, property.PropertyType));
        }

        return query;
    }

    private static IQueryable<StgRawShippingAdvice> ApplyDateToFilter(
        IQueryable<StgRawShippingAdvice> query,
        string fieldName,
        string toValue)
    {
        if (!TryParseDateOnly(toValue, out var toDate))
        {
            return query;
        }

        if (!Properties.TryGetValue(fieldName, out var property))
        {
            return query;
        }

        var propertyType = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
        if (propertyType == typeof(string))
        {
            return query.Where(BuildStringCompareExpression(fieldName, toDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), greaterOrEqual: false));
        }

        if (propertyType == typeof(DateTime))
        {
            var exclusiveEnd = toDate.AddDays(1).ToDateTime(TimeOnly.MinValue);
            return query.Where(BuildDateTimeExclusiveEndExpression(fieldName, exclusiveEnd, property.PropertyType));
        }

        return query;
    }

    private static IQueryable<StgRawShippingAdvice> ApplyExactDateFilter(
        IQueryable<StgRawShippingAdvice> query,
        string fieldName,
        string dateValue)
    {
        if (!TryParseDateOnly(dateValue, out var date))
        {
            return query;
        }

        if (!Properties.TryGetValue(fieldName, out var property))
        {
            return query;
        }

        var propertyType = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
        if (propertyType == typeof(string))
        {
            return ApplyStringInFilter(query, fieldName, [date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)]);
        }

        if (propertyType == typeof(DateTime))
        {
            var start = date.ToDateTime(TimeOnly.MinValue);
            var exclusiveEnd = date.AddDays(1).ToDateTime(TimeOnly.MinValue);
            return query
                .Where(BuildDateTimeCompareExpression(fieldName, start, greaterOrEqual: true, property.PropertyType))
                .Where(BuildDateTimeExclusiveEndExpression(fieldName, exclusiveEnd, property.PropertyType));
        }

        return query;
    }

    private static IQueryable<StgRawShippingAdvice> ApplyStringInFilter(
        IQueryable<StgRawShippingAdvice> query,
        string fieldName,
        List<string> values)
    {
        var parameter = Expression.Parameter(typeof(StgRawShippingAdvice), "entity");
        var property = Expression.Property(parameter, fieldName);
        var notNull = Expression.NotEqual(property, Expression.Constant(null, typeof(string)));
        var containsMethod = typeof(List<string>).GetMethod(nameof(List<string>.Contains), [typeof(string)])!;
        var callContains = Expression.Call(Expression.Constant(values), containsMethod, property);
        var body = Expression.AndAlso(notNull, callContains);
        var lambda = Expression.Lambda<Func<StgRawShippingAdvice, bool>>(body, parameter);
        return query.Where(lambda);
    }

    private static IQueryable<StgRawShippingAdvice> ApplyEqualsFilter(
        IQueryable<StgRawShippingAdvice> query,
        string fieldName,
        object value,
        Type propertyType)
    {
        var parameter = Expression.Parameter(typeof(StgRawShippingAdvice), "entity");
        var property = Expression.Property(parameter, fieldName);
        var constant = BuildTypedConstant(value, propertyType);
        var equal = Expression.Equal(property, constant);
        var lambda = Expression.Lambda<Func<StgRawShippingAdvice, bool>>(equal, parameter);
        return query.Where(lambda);
    }

    private static Expression<Func<StgRawShippingAdvice, bool>> BuildStringLikeExpression(string fieldName, string term)
    {
        var parameter = Expression.Parameter(typeof(StgRawShippingAdvice), "entity");
        var property = Expression.Property(parameter, fieldName);
        var notNull = Expression.NotEqual(property, Expression.Constant(null, typeof(string)));
        var efFunctions = Expression.Property(null, typeof(EF), nameof(EF.Functions));
        var likeMethod = typeof(DbFunctionsExtensions).GetMethod(
            nameof(DbFunctionsExtensions.Like),
            [typeof(DbFunctions), typeof(string), typeof(string)])!;
        var pattern = Expression.Constant($"%{term}%");
        var likeCall = Expression.Call(likeMethod, efFunctions, property, pattern);
        var body = Expression.AndAlso(notNull, likeCall);
        return Expression.Lambda<Func<StgRawShippingAdvice, bool>>(body, parameter);
    }

    private static Expression<Func<StgRawShippingAdvice, bool>> BuildGuidToStringLikeExpression(string fieldName, string term)
    {
        var parameter = Expression.Parameter(typeof(StgRawShippingAdvice), "entity");
        var property = Expression.Property(parameter, fieldName);
        var toString = Expression.Call(property, nameof(Guid.ToString), Type.EmptyTypes);
        var efFunctions = Expression.Property(null, typeof(EF), nameof(EF.Functions));
        var likeMethod = typeof(DbFunctionsExtensions).GetMethod(
            nameof(DbFunctionsExtensions.Like),
            [typeof(DbFunctions), typeof(string), typeof(string)])!;
        var pattern = Expression.Constant($"%{term}%");
        var likeCall = Expression.Call(likeMethod, efFunctions, toString, pattern);
        return Expression.Lambda<Func<StgRawShippingAdvice, bool>>(likeCall, parameter);
    }

    private static Expression<Func<StgRawShippingAdvice, bool>> BuildStringCompareExpression(
        string fieldName,
        string compareValue,
        bool greaterOrEqual)
    {
        var parameter = Expression.Parameter(typeof(StgRawShippingAdvice), "entity");
        var property = Expression.Property(parameter, fieldName);
        var notNull = Expression.NotEqual(property, Expression.Constant(null, typeof(string)));
        var constant = Expression.Constant(compareValue);
        var compare = greaterOrEqual
            ? Expression.GreaterThanOrEqual(property, constant)
            : Expression.LessThanOrEqual(property, constant);
        var body = Expression.AndAlso(notNull, compare);
        return Expression.Lambda<Func<StgRawShippingAdvice, bool>>(body, parameter);
    }

    private static Expression<Func<StgRawShippingAdvice, bool>> BuildDateTimeCompareExpression(
        string fieldName,
        DateTime compareValue,
        bool greaterOrEqual,
        Type propertyType)
    {
        var parameter = Expression.Parameter(typeof(StgRawShippingAdvice), "entity");
        var property = Expression.Property(parameter, fieldName);
        var constant = BuildTypedConstant(compareValue, propertyType);
        var compare = greaterOrEqual
            ? Expression.GreaterThanOrEqual(property, constant)
            : Expression.LessThanOrEqual(property, constant);
        return Expression.Lambda<Func<StgRawShippingAdvice, bool>>(compare, parameter);
    }

    private static Expression<Func<StgRawShippingAdvice, bool>> BuildDateTimeExclusiveEndExpression(
        string fieldName,
        DateTime exclusiveEnd,
        Type propertyType)
    {
        var parameter = Expression.Parameter(typeof(StgRawShippingAdvice), "entity");
        var property = Expression.Property(parameter, fieldName);
        var constant = BuildTypedConstant(exclusiveEnd, propertyType);
        var compare = Expression.LessThan(property, constant);
        return Expression.Lambda<Func<StgRawShippingAdvice, bool>>(compare, parameter);
    }

    private static Expression BuildTypedConstant(object value, Type targetType)
    {
        var underlying = Nullable.GetUnderlyingType(targetType);
        if (underlying is not null)
        {
            var converted = Convert.ChangeType(value, underlying, CultureInfo.InvariantCulture);
            return Expression.Convert(Expression.Constant(converted, underlying), targetType);
        }

        return Expression.Constant(Convert.ChangeType(value, targetType, CultureInfo.InvariantCulture), targetType);
    }

    private static bool TryParseDateOnly(string value, out DateOnly date) =>
        DateOnly.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out date)
        || DateOnly.TryParse(value, CultureInfo.CurrentCulture, DateTimeStyles.None, out date);
}

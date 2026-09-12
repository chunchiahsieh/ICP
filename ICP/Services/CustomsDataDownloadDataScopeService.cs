using System.Linq.Expressions;
using System.Text.Json;
using ICP.Models;
using ICP.Models.Icp;

namespace ICP.Services;

/// <summary>Validates and applies role data scopes for Customs Data Download.</summary>
public static class CustomsDataDownloadDataScopeService
{
    public const string ResourceCode = "Views.Broker.CustomsDataDownload.View";
    private const string CustomsDataTable = "StgRaw_ShippingAdvice";

    private static readonly IReadOnlyDictionary<string, Expression<Func<StgRawShippingAdvice, string?>>> AllowedFields =
        new Dictionary<string, Expression<Func<StgRawShippingAdvice, string?>>>(StringComparer.OrdinalIgnoreCase)
        {
            ["Forwarder"] = row => row.Forwarder,
            ["Bu"] = row => row.Bu,
            ["DestinationCountry"] = row => row.DestinationCountry,
            ["DestinationPort"] = row => row.DestinationPort,
            ["InvoiceType"] = row => row.InvoiceType,
            ["Coo"] = row => row.Coo
        };

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static bool TrySerialize(DataScopeDefinition? definition, out string? json, out string? error)
    {
        json = null;
        error = null;

        if (definition is null || definition.Conditions.Count == 0)
        {
            return true;
        }

        if (!TryValidateForPersistence(definition, out error))
        {
            return false;
        }

        definition.Version = 1;
        json = JsonSerializer.Serialize(definition, JsonOptions);
        return true;
    }

    public static IQueryable<StgRawShippingAdvice> Apply(
        IQueryable<StgRawShippingAdvice> query,
        IEnumerable<string?> dataScopes)
    {
        var scopes = dataScopes.ToList();
        if (scopes.Count == 0 || scopes.Any(scope =>
                string.IsNullOrWhiteSpace(scope)
                || string.Equals(scope, "ALL", StringComparison.OrdinalIgnoreCase)))
        {
            return query;
        }

        Expression<Func<StgRawShippingAdvice, bool>>? combined = null;
        foreach (var json in scopes)
        {
            if (!TryDeserialize(json!, out var scope))
            {
                // A scope that does not apply to this page is treated as unrestricted.
                return query;
            }

            var predicate = BuildPredicate(scope!);
            combined = combined is null ? predicate : OrElse(combined, predicate);
        }

        return combined is null ? query.Where(_ => false) : query.Where(combined);
    }

    private static bool TryDeserialize(string json, out DataScopeDefinition? definition)
    {
        definition = null;
        try
        {
            definition = JsonSerializer.Deserialize<DataScopeDefinition>(json, JsonOptions);
            return definition is not null && TryValidateForCustomsQuery(definition);
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool TryValidateForPersistence(DataScopeDefinition definition, out string? error)
    {
        error = null;
        if (definition.Version is not 0 and not 1 || definition.Conditions.Count == 0)
        {
            error = "Invalid data scope format.";
            return false;
        }

        foreach (var condition in definition.Conditions)
        {
            if (string.IsNullOrWhiteSpace(condition.Table)
                || string.IsNullOrWhiteSpace(condition.Field)
                || !IsAllowedOperator(condition.Operator)
                || condition.Values.Count == 0
                || condition.Values.Any(value => string.IsNullOrWhiteSpace(value) || value.Trim().Length > 100)
                || (condition.Operator.Equals("Equal", StringComparison.OrdinalIgnoreCase) && condition.Values.Count != 1))
            {
                error = "Invalid data scope condition.";
                return false;
            }
        }

        return true;
    }

    private static bool TryValidateForCustomsQuery(DataScopeDefinition definition) =>
        definition.Conditions.All(condition =>
            string.Equals(condition.Table, CustomsDataTable, StringComparison.OrdinalIgnoreCase)
            && AllowedFields.ContainsKey(condition.Field)
            && IsAllowedOperator(condition.Operator)
            && condition.Values.Count > 0);

    private static Expression<Func<StgRawShippingAdvice, bool>> BuildPredicate(DataScopeDefinition definition)
    {
        var parameter = Expression.Parameter(typeof(StgRawShippingAdvice), "row");
        Expression? body = null;

        foreach (var condition in definition.Conditions)
        {
            var source = AllowedFields[condition.Field];
            var property = new ReplaceParameterVisitor(source.Parameters[0], parameter).Visit(source.Body)!;
            var values = condition.Values.Select(value => value.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            var conditionBody = condition.Operator.ToUpperInvariant() switch
            {
                "EQUAL" => Expression.Equal(property, Expression.Constant(values[0], typeof(string))),
                "IN" => Expression.Call(
                    typeof(Enumerable),
                    nameof(Enumerable.Contains),
                    [typeof(string)],
                    Expression.Constant(values),
                    property),
                "CONTAINS" => BuildContainsPredicate(property, values),
                _ => throw new InvalidOperationException("Validated data scope contains an unsupported operator.")
            };
            body = body is null ? conditionBody : Expression.AndAlso(body, conditionBody);
        }

        return Expression.Lambda<Func<StgRawShippingAdvice, bool>>(body!, parameter);
    }

    private static bool IsAllowedOperator(string? value) =>
        value is not null && (value.Equals("Equal", StringComparison.OrdinalIgnoreCase)
            || value.Equals("In", StringComparison.OrdinalIgnoreCase)
            || value.Equals("Contains", StringComparison.OrdinalIgnoreCase));

    private static Expression BuildContainsPredicate(Expression property, IReadOnlyList<string> values)
    {
        var notNull = Expression.NotEqual(property, Expression.Constant(null, typeof(string)));
        Expression? matches = null;
        foreach (var value in values)
        {
            var contains = Expression.Call(property, nameof(string.Contains), Type.EmptyTypes, Expression.Constant(value));
            matches = matches is null ? contains : Expression.OrElse(matches, contains);
        }

        return Expression.AndAlso(notNull, matches!);
    }

    private static Expression<Func<StgRawShippingAdvice, bool>> OrElse(
        Expression<Func<StgRawShippingAdvice, bool>> left,
        Expression<Func<StgRawShippingAdvice, bool>> right)
    {
        var parameter = Expression.Parameter(typeof(StgRawShippingAdvice), "row");
        var leftBody = new ReplaceParameterVisitor(left.Parameters[0], parameter).Visit(left.Body)!;
        var rightBody = new ReplaceParameterVisitor(right.Parameters[0], parameter).Visit(right.Body)!;
        return Expression.Lambda<Func<StgRawShippingAdvice, bool>>(Expression.OrElse(leftBody, rightBody), parameter);
    }

    private sealed class ReplaceParameterVisitor(ParameterExpression source, ParameterExpression target) : ExpressionVisitor
    {
        protected override Expression VisitParameter(ParameterExpression node) => node == source ? target : base.VisitParameter(node);
    }
}

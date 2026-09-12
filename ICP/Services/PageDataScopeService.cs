using System.Globalization;
using System.Linq.Expressions;
using System.Text.Json;
using ICP.Data;
using ICP.Models;
using ICP.Models.Icp;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace ICP.Services;

/// <summary>Applies the current page's View scope before querying, paging or exporting.</summary>
public sealed class PageDataScopeService(ApplicationDbContext db,
    UserResourcePermissionService permissions, IHttpContextAccessor context)
{
    public string? ResourceCode => context.HttpContext?.Request.RouteValues["controller"]?.ToString()?.ToLowerInvariant() switch
    {
        "shipinfo" => "Views.Function.ShipInfo.View",
        "shippingreport" => "Views.Report.ShippingReport.View",
        "massdatareport" => "Views.Report.MassDataReport.View",
        "tariffdata" => "Views.Broker.TariffData.View",
        _ => null
    };

    public IQueryable<T> Apply<T>(IQueryable<T> query) where T : class
    {
        if (ResourceCode is not { } code) return query;
        var scopes = permissions.GetSessionResources()
            .Where(r => r.IsAllowed && r.ResourceCode.Equals(code, StringComparison.OrdinalIgnoreCase))
            .SelectMany(r => r.DataScopes).ToList();
        return ApplyDefinitions(query, db.Model, scopes);
    }

    public IQueryable<IcpDetail> ApplyDetails(IQueryable<IcpDetail> query)
    {
        if (ResourceCode is null) return query;
        var original = db.IcpHeaders.AsNoTracking();
        var headers = Apply(original);
        if (ReferenceEquals(original, headers)) return query;
        return query.Where(detail => headers.Any(header =>
            header.InvoiceNo == detail.InvoiceNo && header.TetPo == detail.TetPo));
    }

    public static IQueryable<T> ApplyDefinitions<T>(IQueryable<T> query, IModel model,
        IEnumerable<string?> scopes) where T : class
    {
        var entity = model.FindEntityType(typeof(T))!;
        var table = entity.GetTableName()!;
        var store = StoreObjectIdentifier.Table(table, entity.GetSchema());
        var parameter = Expression.Parameter(typeof(T), "row");
        Expression? grants = null;
        foreach (var json in scopes)
        {
            // User-selected policy: missing or invalid scope means unrestricted.
            if (string.IsNullOrWhiteSpace(json) || json.Equals("ALL", StringComparison.OrdinalIgnoreCase)) return query;
            try
            {
                var definition = JsonSerializer.Deserialize<DataScopeDefinition>(json,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (definition?.Version != 1 || definition.Conditions is not { Count: > 0 }) return query;
                Expression? conditions = null;
                foreach (var condition in definition.Conditions)
                {
                    if (condition is null || !(string.Equals(condition.Table, table, StringComparison.OrdinalIgnoreCase)
                        || string.Equals(condition.Table, $"{entity.GetSchema() ?? "dbo"}.{table}", StringComparison.OrdinalIgnoreCase))) return query;
                    var property = entity.GetProperties().FirstOrDefault(p =>
                        string.Equals(p.GetColumnName(store), condition.Field, StringComparison.OrdinalIgnoreCase)
                        || string.Equals(p.Name, condition.Field, StringComparison.OrdinalIgnoreCase));
                    if (property?.PropertyInfo is null || condition.Values is not { Count: > 0 }
                        || condition.Values.Any(string.IsNullOrWhiteSpace)) return query;
                    var op = condition.Operator?.ToUpperInvariant();
                    if (op is not ("IN" or "EQUAL" or "=" or "CONTAINS")
                        || (op is "EQUAL" or "=" && condition.Values.Count != 1)) return query;
                    var member = Expression.Property(parameter, property.PropertyInfo);
                    var type = Nullable.GetUnderlyingType(member.Type) ?? member.Type;
                    Expression? alternatives = null;
                    foreach (var value in condition.Values)
                    {
                        Expression match;
                        if (op == "CONTAINS")
                        {
                            if (type != typeof(string)) return query;
                            match = Expression.AndAlso(Expression.NotEqual(member, Expression.Constant(null, member.Type)),
                                Expression.Call(member, nameof(string.Contains), Type.EmptyTypes, Expression.Constant(value.Trim())));
                        }
                        else
                        {
                            object parsed = type == typeof(string) ? value.Trim()
                                : type == typeof(Guid) ? Guid.Parse(value)
                                : type == typeof(DateOnly) ? DateOnly.Parse(value, CultureInfo.InvariantCulture)
                                : Convert.ChangeType(value, type, CultureInfo.InvariantCulture);
                            Expression constant = Expression.Constant(parsed, type);
                            if (type != member.Type) constant = Expression.Convert(constant, member.Type);
                            match = Expression.Equal(member, constant);
                        }
                        alternatives = alternatives is null ? match : Expression.OrElse(alternatives, match);
                    }
                    conditions = conditions is null ? alternatives : Expression.AndAlso(conditions, alternatives!);
                }
                grants = grants is null ? conditions : Expression.OrElse(grants, conditions!);
            }
            catch (Exception ex) when (ex is JsonException or FormatException or InvalidCastException or OverflowException or ArgumentException)
            {
                return query;
            }
        }
        return grants is null ? query : query.Where(Expression.Lambda<Func<T, bool>>(grants, parameter));
    }
}

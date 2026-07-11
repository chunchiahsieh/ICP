using System.Text.Json.Serialization;
using ICP.Helpers;

namespace ICP.Models.ShipInfo;

public class ShipInfoActionPermission
{
    [JsonPropertyName("edit")]
    public bool Edit { get; init; }

    [JsonPropertyName("delete")]
    public bool Delete { get; init; }

    [JsonPropertyName("deposit")]
    public bool Deposit { get; init; }

    [JsonPropertyName("arur")]
    public bool Arur { get; init; }
}

public static class ShipInfoStatusRules
{
    public static ShipInfoActionPermission Resolve(string? status)
    {
        var normalized = ShipInfoStatusResolver.Normalize(status);
        return normalized switch
        {
            ShipInfoStatuses.Processing => new ShipInfoActionPermission
            {
                Edit = true,
                Delete = true,
                Deposit = true,
                Arur = true
            },
            ShipInfoStatuses.Cancelled => new ShipInfoActionPermission
            {
                Edit = false,
                Delete = false,
                Deposit = false,
                Arur = false
            },
            _ => new ShipInfoActionPermission
            {
                Edit = true,
                Delete = true,
                Deposit = true,
                Arur = true
            }
        };
    }

    public static IReadOnlyDictionary<string, ShipInfoActionPermission> BuildMatrix() =>
        ShipInfoStatuses.LookupOptions
            .Select(x => x.Value)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                x => x,
                Resolve,
                StringComparer.OrdinalIgnoreCase);
}

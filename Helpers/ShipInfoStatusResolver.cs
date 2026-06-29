using ICP.Models.Icp;
using ICP.Models.ShipInfo;

namespace ICP.Helpers;

public static class ShipInfoStatusResolver
{
    public static string Resolve(IcpHeader header)
    {
        if (!string.IsNullOrWhiteSpace(header.Status))
        {
            return Normalize(header.Status);
        }

        if (!string.IsNullOrWhiteSpace(header.Cancellation))
        {
            return ShipInfoStatuses.Cancelled;
        }

        return ShipInfoStatuses.Processing;
    }

    public static string Normalize(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            return ShipInfoStatuses.Processing;
        }

        var trimmed = status.Trim();
        if (trimmed.Equals(ShipInfoStatuses.Processing, StringComparison.OrdinalIgnoreCase)
            || trimmed.Equals("處理中", StringComparison.Ordinal))
        {
            return ShipInfoStatuses.Processing;
        }

        if (trimmed.Equals(ShipInfoStatuses.WarehouseReceived, StringComparison.OrdinalIgnoreCase)
            || trimmed.Equals("庫房已接收", StringComparison.Ordinal))
        {
            return ShipInfoStatuses.WarehouseReceived;
        }

        if (trimmed.Equals(ShipInfoStatuses.Cancelled, StringComparison.OrdinalIgnoreCase)
            || trimmed.Equals("作廢", StringComparison.Ordinal))
        {
            return ShipInfoStatuses.Cancelled;
        }

        return trimmed;
    }
}

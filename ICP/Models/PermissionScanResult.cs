namespace ICP.Models;

public class PermissionScanResult
{
    public int ScannedCount { get; set; }

    public int InsertedCount { get; set; }

    public int UpdatedCount { get; set; }

    public int DisabledLegacyCount { get; set; }

    public int MigratedRolePermissionCount { get; set; }

    public IReadOnlyList<string> ResourceCodes { get; set; } = [];
}

using ICP.Models.Icp;

namespace ICP.Models;

public class SystemConfigSearchListViewModel
{
    public IList<SystemConfig> ListData { get; init; } = [];

    public string PermissionPrefix { get; set; } = string.Empty;

    public IReadOnlyDictionary<string, string> EtaCalendarTypeDisplayByKey { get; set; }
        = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
}

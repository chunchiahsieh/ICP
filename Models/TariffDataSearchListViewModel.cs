using ICP.Models.Icp;
using ICP.Models.Tariff;

namespace ICP.Models;

public class TariffDataSearchListViewModel
{
    public IReadOnlyList<TariffData> ListData { get; init; } = [];

    public IReadOnlyList<TariffTableFieldMetadata> Fields { get; init; } = [];

    public TariffTableUiOptions TableUi { get; init; } = TariffTableUiOptions.MergeDefaults(null);

    public bool HasFilterRow { get; init; }

    public string StorageRoot { get; init; } = string.Empty;
}

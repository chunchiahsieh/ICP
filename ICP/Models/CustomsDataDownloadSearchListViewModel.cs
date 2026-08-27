using ICP.Models.CustomsDataDownload;
using ICP.Models.Icp;

namespace ICP.Models;

public class CustomsDataDownloadSearchListViewModel
{
    public IReadOnlyList<StgRawShippingAdvice> ListData { get; init; } = [];

    public IReadOnlyList<CustomsDataDownloadTableFieldMetadata> Fields { get; init; } = [];

    public CustomsDataDownloadTableUiOptions TableUi { get; init; } =
        CustomsDataDownloadTableUiOptions.MergeDefaults(null);

    public bool HasFilterRow { get; init; }
}

using ICP.Models.Forwarder;

namespace ICP.Models;

public class ForwarderDataUploadListViewModel
{
    public IReadOnlyList<ForwarderDataUploadRowViewModel> ListData { get; init; } = [];

    public IReadOnlyList<ForwarderTableFieldMetadata> Fields { get; init; } = [];

    public ForwarderTableUiOptions TableUi { get; init; } = ForwarderTableUiOptions.MergeDefaults(null);

    public bool HasFilterRow { get; init; }
}

using ICP.Models.Icp;

namespace ICP.Models;

public class ForwarderDataUploadListViewModel
{
    public IReadOnlyList<ForwarderDataUpload> ListData { get; init; } = [];
}

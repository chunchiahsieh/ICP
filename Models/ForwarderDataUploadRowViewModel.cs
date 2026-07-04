using ICP.Models.Icp;

namespace ICP.Models;

public class ForwarderDataUploadRowViewModel
{
    public ForwarderDataUpload Row { get; init; } = null!;

    public bool IsDbDuplicate { get; init; }

    public bool IsInFileMultiLine { get; init; }
}

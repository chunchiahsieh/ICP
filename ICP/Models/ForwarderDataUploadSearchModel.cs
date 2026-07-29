namespace ICP.Models;

public class ForwarderDataUploadSearchModel
{
    public string? FilePath { get; set; }

    public bool Preview { get; set; } = true;

    public List<string> DuplicateStatuses { get; set; } = [];
}

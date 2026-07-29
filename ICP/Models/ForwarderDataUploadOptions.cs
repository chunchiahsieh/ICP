namespace ICP.Models;

public class ForwarderDataUploadOptions
{
    public const string SectionName = "ForwarderDataUpload";

    public string StoragePath { get; set; } = "uploads/forwarder";

    public int MaxSizeMb { get; set; } = 50;

    public long MaxSizeBytes => MaxSizeMb * 1024L * 1024L;
}

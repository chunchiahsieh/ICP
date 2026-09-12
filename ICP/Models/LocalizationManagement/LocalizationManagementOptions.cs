namespace ICP.Models.LocalizationManagement;

/// <summary>Controls whether the localization management page is available.</summary>
public sealed class LocalizationManagementOptions
{
    public const string SectionName = "LocalizationManagement";

    public bool Enabled { get; set; }
}

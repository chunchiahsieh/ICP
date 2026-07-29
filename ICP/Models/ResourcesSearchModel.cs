namespace ICP.Models;

public class ResourcesSearchModel
{
    public List<string> ResourceCodes { get; set; } = [];

    public List<string> ResourceNames { get; set; } = [];

    public List<string> ResourceTypes { get; set; } = [];

    public List<string> SystemCodes { get; set; } = [];

    public List<string> ModuleCodes { get; set; } = [];

    public List<string> Routes { get; set; } = [];

    public List<string> Sorts { get; set; } = [];

    public List<string> IsVisibles { get; set; } = [];

    public List<string> IsEnableds { get; set; } = [];
}

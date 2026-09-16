using ICP;
using ICP.Data;
using ICP.Infrastructure;
using Microsoft.Extensions.Localization;

namespace ICP.Controllers.Setting;

[SettingModule]
public class CountryOfOriginController : SystemConfigControllerBase
{
    public CountryOfOriginController(ApplicationDbContext icpDb, IStringLocalizer<SharedResource> localizer)
        : base(icpDb, localizer)
    {
    }

    protected override string Category => "CountryOfOrigin";

    protected override string DataCategory => "Country of Origin";
}

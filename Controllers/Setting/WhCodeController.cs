using ICP;
using ICP.Data;
using ICP.Infrastructure;
using Microsoft.Extensions.Localization;

namespace ICP.Controllers.Setting;

[SettingModule]
public class WhCodeController : SystemConfigControllerBase
{
    public WhCodeController(ApplicationDbContext icpDb, IStringLocalizer<SharedResource> localizer)
        : base(icpDb, localizer)
    {
    }

    protected override string Category => "WhCode";
}

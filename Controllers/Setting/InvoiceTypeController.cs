using ICP;
using ICP.Data;
using ICP.Infrastructure;
using Microsoft.Extensions.Localization;

namespace ICP.Controllers.Setting;

[SettingModule]
public class InvoiceTypeController : SystemConfigControllerBase
{
    public InvoiceTypeController(ApplicationDbContext icpDb, IStringLocalizer<SharedResource> localizer)
        : base(icpDb, localizer)
    {
    }

    protected override string Category => "InvoiceType";
}

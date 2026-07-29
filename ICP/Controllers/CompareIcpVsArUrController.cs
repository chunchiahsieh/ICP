using ICP.Models.Report;
using ICP.Services;
using Microsoft.Extensions.Localization;

namespace ICP.Controllers;

public class CompareIcpVsArUrController : ReportControllerBase
{
    public CompareIcpVsArUrController(
        IReportDataService reportDataService,
        IStringLocalizer<SharedResource> localizer)
        : base(reportDataService, localizer)
    {
    }

    protected override string ReportKey => ReportKeys.CompareIcpVsArUr;

    protected override string PermissionCode => "Views.Shared._SidebarNav.Report.CompareIcpVsArUr";

    protected override string TitleKey => "Views.Shared._SidebarNav.Report.CompareIcpVsArUr";
}

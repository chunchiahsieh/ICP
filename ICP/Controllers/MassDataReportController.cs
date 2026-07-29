using ICP.Models.Report;
using ICP.Services;
using Microsoft.Extensions.Localization;

namespace ICP.Controllers;

public class MassDataReportController : ReportControllerBase
{
    public MassDataReportController(
        IReportDataService reportDataService,
        IStringLocalizer<SharedResource> localizer)
        : base(reportDataService, localizer)
    {
    }

    protected override string ReportKey => ReportKeys.MassDataReport;

    protected override string PermissionCode => "Views.Shared._SidebarNav.Report.MassDataReport";

    protected override string TitleKey => "Views.Shared._SidebarNav.Report.MassDataReport";
}

using ICP.Models.Report;
using ICP.Services;
using Microsoft.Extensions.Localization;

namespace ICP.Controllers;

public class ShippingReportController : ReportControllerBase
{
    public ShippingReportController(
        IReportDataService reportDataService,
        IStringLocalizer<SharedResource> localizer)
        : base(reportDataService, localizer)
    {
    }

    protected override string ReportKey => ReportKeys.ShippingReport;

    protected override string PermissionCode => "Views.Shared._SidebarNav.Report.ShippingReport";

    protected override string TitleKey => "Views.Shared._SidebarNav.Report.ShippingReport";
}

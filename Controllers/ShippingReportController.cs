using Microsoft.AspNetCore.Mvc;

namespace ICP.Controllers;

public class ShippingReportController : Controller
{
    public IActionResult Index()
    {
        return View("~/Views/REPORT/ShippingReport/View.cshtml");
    }
}

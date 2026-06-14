using Microsoft.AspNetCore.Mvc;

namespace ICP.Controllers;

public class MassDataReportController : Controller
{
    public IActionResult Index()
    {
        return View("~/Views/REPORT/MassDataReport/View.cshtml");
    }
}

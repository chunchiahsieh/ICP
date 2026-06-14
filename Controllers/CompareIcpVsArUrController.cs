using Microsoft.AspNetCore.Mvc;

namespace ICP.Controllers;

public class CompareIcpVsArUrController : Controller
{
    public IActionResult Index()
    {
        return View("~/Views/REPORT/CompareIcpVsArUr/View.cshtml");
    }
}

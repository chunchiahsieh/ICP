using Microsoft.AspNetCore.Mvc;

namespace ICP.Controllers;

public class ExportController : Controller
{
    public IActionResult Index()
    {
        return View("~/Views/FUNCTION/Export/View.cshtml");
    }
}

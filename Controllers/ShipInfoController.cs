using Microsoft.AspNetCore.Mvc;

namespace ICP.Controllers;

public class ShipInfoController : Controller
{
    public IActionResult Index()
    {
        return View("~/Views/FUNCTION/ShipInfo/View.cshtml");
    }
}

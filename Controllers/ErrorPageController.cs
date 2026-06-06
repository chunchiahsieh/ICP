using Microsoft.AspNetCore.Mvc;

namespace ICP.Controllers;

public class ErrorPageController : Controller
{
    public IActionResult Index()
    {
        return View();
    }
}

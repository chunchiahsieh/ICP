using ICP.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace ICP.Controllers;

[SkipResourcePermission]
public class ErrorPageController : Controller
{
    public IActionResult Index()
    {
        return View();
    }
}

using Microsoft.AspNetCore.Mvc;

namespace ICP.Controllers;

public class CustomsDataDownloadController : Controller
{
    public IActionResult Index()
    {
        return View("~/Views/BROKER/CustomsDataDownload/View.cshtml");
    }
}

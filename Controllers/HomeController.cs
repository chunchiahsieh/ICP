using System.Diagnostics;
using ICP.Models;
using ICP.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ICP.Controllers;

public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;
    private readonly UserAuthService _userAuthService;

    public HomeController(
        ILogger<HomeController> logger,
        UserAuthService userAuthService)
    {
        _logger = logger;
        _userAuthService = userAuthService;
    }

    [AllowAnonymous]
    public async Task<IActionResult> Index(string Login = "", string Type = "", CancellationToken cancellationToken = default)
    {
        if (!await _userAuthService.TempDataSet(this, Login, Type, cancellationToken))
        {
            return RedirectToAction("Index", "Login");
        }

        return View("~/Views/Home/Index.cshtml");
    }

    public IActionResult Privacy()
    {
        return View("~/Views/Home/Privacy.cshtml");
    }

    [AllowAnonymous]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View("~/Views/Home/Error.cshtml", new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}

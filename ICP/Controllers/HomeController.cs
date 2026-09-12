using System.Diagnostics;
using ICP.Models;
using ICP.Services;
using ICP.Models.Sidebar;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ICP.Controllers;

public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;
    private readonly UserAuthService _userAuthService;
    private readonly SidebarOptions _sidebarOptions;

    public HomeController(
        ILogger<HomeController> logger,
        UserAuthService userAuthService,
        Microsoft.Extensions.Options.IOptions<SidebarOptions> sidebarOptions)
    {
        _logger = logger;
        _userAuthService = userAuthService;
        _sidebarOptions = sidebarOptions.Value;
    }

    [AllowAnonymous]
    public async Task<IActionResult> Index(string Login = "", string Type = "", CancellationToken cancellationToken = default)
    {
        if (!await _userAuthService.TempDataSet(this, Login, Type, cancellationToken))
        {
            return RedirectToAction("Index", "Login");
        }

        return RedirectToAction("Index", _sidebarOptions.DefaultPage);
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

using System.Diagnostics;
using ICP.Models;
using ICP.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;

namespace ICP.Controllers;

public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;
    private readonly LoginSessionService _loginSessionService;
    private readonly IStringLocalizer<SharedResource> _localizer;

    public HomeController(
        ILogger<HomeController> logger,
        LoginSessionService loginSessionService,
        IStringLocalizer<SharedResource> localizer)
    {
        _logger = logger;
        _loginSessionService = loginSessionService;
        _localizer = localizer;
    }

    [AllowAnonymous]
    public async Task<IActionResult> Index(string? login, CancellationToken cancellationToken)
    {
        if (!await _loginSessionService.TryEstablishSessionAsync(login, cancellationToken))
        {
            TempData["ReturnMsg"] = _localizer["Auth.FailedContactIs"].Value;
            return RedirectToAction("Index", "Login");
        }

        return View();
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [AllowAnonymous]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}

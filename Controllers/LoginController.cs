using ICP.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ICP.Controllers;

[AllowAnonymous]
public class LoginController : Controller
{
    private readonly UserAuthService _userAuthService;

    public LoginController(UserAuthService userAuthService)
    {
        _userAuthService = userAuthService;
    }

    [HttpGet]
    public IActionResult Index()
    {
        _userAuthService.SessionClear(this);
        return View();
    }

    [HttpGet]
    public async Task<IActionResult> Login(string TELID = "", CancellationToken cancellationToken = default)
    {
        var ok = await _userAuthService.CanLoginAsync(TELID, cancellationToken);
        return Json(ok ? "Y" : "E");
    }

    [HttpGet]
    public IActionResult Logout()
    {
        _userAuthService.SessionClear(this);
        return RedirectToAction(nameof(Index));
    }
}

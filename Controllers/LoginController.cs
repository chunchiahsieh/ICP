using ICP.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ICP.Controllers;

[AllowAnonymous]
public class LoginController : Controller
{
    private readonly LoginSessionService _loginSessionService;
    private readonly UserInfoResolver _userInfoResolver;

    public LoginController(
        LoginSessionService loginSessionService,
        UserInfoResolver userInfoResolver)
    {
        _loginSessionService = loginSessionService;
        _userInfoResolver = userInfoResolver;
    }

    [HttpGet]
    public IActionResult Index()
    {
        _loginSessionService.ClearSession();
        return View();
    }

    [HttpGet]
    public async Task<IActionResult> Login(string? telId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(telId))
        {
            return Json(new { success = false });
        }

        var user = await _userInfoResolver.ResolveFromTelIdAsync(telId, cancellationToken);
        var success = user is not null && !string.IsNullOrWhiteSpace(user.TelId);
        return Json(new { success });
    }

    [HttpPost]
    public Task<IActionResult> LoginPost([FromForm] string? telId, CancellationToken cancellationToken) =>
        Login(telId, cancellationToken);

    [HttpGet]
    public IActionResult Logout()
    {
        _loginSessionService.ClearSession();
        return RedirectToAction(nameof(Index));
    }
}

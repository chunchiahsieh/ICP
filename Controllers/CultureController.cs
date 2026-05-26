using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;

namespace ICP.Controllers;

[AllowAnonymous]
public class CultureController : Controller
{
    [HttpGet]
    public IActionResult Set(string culture, string? returnUrl)
    {
        var supported = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "zh-TW",
            "en",
            "ja"
        };

        if (string.IsNullOrWhiteSpace(culture) || !supported.Contains(culture))
        {
            culture = "zh-TW";
        }

        Response.Cookies.Append(
            CookieRequestCultureProvider.DefaultCookieName,
            CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(culture)),
            new CookieOptions
            {
                Expires = DateTimeOffset.UtcNow.AddYears(1),
                IsEssential = true,
                HttpOnly = false,
                SameSite = SameSiteMode.Lax,
                Secure = Request.IsHttps
            });

        if (string.IsNullOrWhiteSpace(returnUrl) || !Url.IsLocalUrl(returnUrl))
        {
            return RedirectToAction("Index", "Home");
        }

        return LocalRedirect(returnUrl);
    }
}

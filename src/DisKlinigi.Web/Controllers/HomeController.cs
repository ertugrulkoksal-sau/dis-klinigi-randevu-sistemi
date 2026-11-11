using System.Diagnostics;
using DisKlinigi.Web.Models;
using DisKlinigi.Web.Navigation;
using DisKlinigi.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DisKlinigi.Web.Controllers;

[AllowAnonymous]
public class HomeController : Controller
{
    /// <summary>
    /// Kök adres ayrı bir sayfa değil: girişli kullanıcı kendi rolünün ilk ekranına, girişsiz
    /// kullanıcı giriş sayfasına gidiyor (arayüz tasarımında ilk ekran giriş/kayıt).
    /// </summary>
    public IActionResult Index()
    {
        if (User.GetRole() is not { } role)
            return RedirectToAction(nameof(AccountController.Login), "Account");

        var home = MainMenu.HomeFor(role);
        return RedirectToAction(home.Action, home.Controller);
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}

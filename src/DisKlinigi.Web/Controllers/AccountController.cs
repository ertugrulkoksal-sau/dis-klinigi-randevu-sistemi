using DisKlinigi.Core.Abstractions;
using DisKlinigi.Core.Accounts;
using DisKlinigi.Core.Domain.Users;
using DisKlinigi.Web.Models;
using DisKlinigi.Web.Navigation;
using DisKlinigi.Web.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DisKlinigi.Web.Controllers;

/// <summary>
/// Giriş, hasta kaydı ve çıkış. Kimin hangi kullanıcı olduğuna <see cref="AccountService"/>
/// karar veriyor; bu sınıf sadece formu okuyup çerezi yazıyor.
/// </summary>
[AllowAnonymous]
public class AccountController : Controller
{
    private const string LoginFailed = "E-posta veya parola hatalı.";

    private readonly AccountService _accounts;
    private readonly IBranchRepository _branches;

    public AccountController(AccountService accounts, IBranchRepository branches)
    {
        _accounts = accounts;
        _branches = branches;
    }

    [HttpGet]
    public async Task<IActionResult> Login(string? returnUrl, CancellationToken cancellationToken)
    {
        if (User.GetRole() is { } role)
            return RedirectToHome(role);

        return await LoginPageAsync(returnUrl: returnUrl, cancellationToken: cancellationToken);
    }

    [HttpPost]
    public async Task<IActionResult> Login(
        [Bind(Prefix = nameof(AccountPageViewModel.Login))] LoginViewModel login,
        string? returnUrl,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return await LoginPageAsync(login: login, returnUrl: returnUrl, cancellationToken: cancellationToken);

        var user = await _accounts.AuthenticateAsync(login.Email, login.Password, cancellationToken);
        if (user is null)
        {
            ModelState.AddModelError($"{nameof(AccountPageViewModel.Login)}.{nameof(LoginViewModel.Password)}", LoginFailed);
            return await LoginPageAsync(
                login: new LoginViewModel { Email = login.Email }, returnUrl: returnUrl, cancellationToken: cancellationToken);
        }

        await SignInAsync(user);

        // Sadece bu sitenin içindeki bir adrese geri dönülüyor. "returnUrl=https://baska-site"
        // ile gelen bir bağlantı, girişten sonra kullanıcıyı başka bir siteye götüremesin (açık yönlendirme).
        if (Url.IsLocalUrl(returnUrl))
            return LocalRedirect(returnUrl);
        return RedirectToHome(user.Role);
    }

    [HttpPost]
    public async Task<IActionResult> Register(
        [Bind(Prefix = nameof(AccountPageViewModel.Register))] RegisterViewModel register,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return await LoginPageAsync(register: WithoutPasswords(register), cancellationToken: cancellationToken);

        var result = await _accounts.RegisterPatientAsync(
            new PatientRegistration(register.FullName, register.Email, register.Password, register.PhoneNumber),
            cancellationToken);

        if (!result.Succeeded)
        {
            var field = result.Field switch
            {
                RegistrationField.Email => $".{nameof(RegisterViewModel.Email)}",
                RegistrationField.Password => $".{nameof(RegisterViewModel.Password)}",
                _ => ""
            };
            ModelState.AddModelError(nameof(AccountPageViewModel.Register) + field, result.Error!);
            return await LoginPageAsync(register: WithoutPasswords(register), cancellationToken: cancellationToken);
        }

        await SignInAsync(result.Patient!);
        TempData["Status"] = "Kaydınız oluşturuldu. Randevu almak için şubeyi seçerek başlayabilirsiniz.";
        return RedirectToHome(UserRole.Patient);
    }

    [HttpPost]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction(nameof(Login));
    }

    [HttpGet]
    public IActionResult AccessDenied() => View();

    private Task SignInAsync(User user) =>
        HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, UserClaims.CreatePrincipal(user));

    private IActionResult RedirectToHome(UserRole role)
    {
        var home = MainMenu.HomeFor(role);
        return RedirectToAction(home.Action, home.Controller);
    }

    /// <summary>
    /// Giriş sayfası. Tasarımdaki gibi üst satırda şubelerin adları var; girişsiz kullanıcı
    /// menü görmediği için başlıkta zincirin hangi şubelerden oluştuğu yazıyor.
    /// </summary>
    private async Task<ViewResult> LoginPageAsync(
        LoginViewModel? login = null, RegisterViewModel? register = null, string? returnUrl = null,
        CancellationToken cancellationToken = default)
    {
        var branches = await _branches.GetAllAsync(cancellationToken);
        ViewData["Branches"] = string.Join(" · ", branches.Select(b => b.Name));

        return View(nameof(Login), new AccountPageViewModel
        {
            Login = login ?? new LoginViewModel(),
            Register = register ?? new RegisterViewModel(),
            ReturnUrl = returnUrl
        });
    }

    /// <summary>Hatalı formda parola alanları geri doldurulmuyor; parola sayfanın HTML'ine yazılmasın.</summary>
    private static RegisterViewModel WithoutPasswords(RegisterViewModel form) => new()
    {
        FullName = form.FullName,
        Email = form.Email,
        PhoneNumber = form.PhoneNumber
    };
}

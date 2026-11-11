using DisKlinigi.Core.Abstractions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace DisKlinigi.Web.Security;

/// <summary>
/// Her istekte giriş çerezini veritabanıyla karşılaştırıyor (K42). Kullanıcı silinmişse ya da
/// rolü/e-postası değişmişse oturum kapanıyor; adı ya da şubesi değişmişse çerez yenileniyor.
/// </summary>
/// <remarks>
/// Neden gerekli: çerez 8 saat geçerli ve veritabanından bağımsız. Sunumdan önce veritabanı
/// silinip yeniden oluşturulursa (README) eski bir çerezdeki Id artık yok ya da başka birine
/// ait olabilir; rolü değiştirilen bir personel de eski yetkisiyle dolaşmaya devam ederdi.
/// Bedeli istek başına bir küçük sorgu; bu projenin yükünde fark edilmiyor.
/// </remarks>
public sealed class UserSessionValidator : CookieAuthenticationEvents
{
    private readonly IUserRepository _users;

    public UserSessionValidator(IUserRepository users)
    {
        _users = users;
    }

    public override async Task ValidatePrincipal(CookieValidatePrincipalContext context)
    {
        var principal = context.Principal;
        var id = principal?.GetUserId();
        var user = id is null ? null : await _users.GetByIdAsync(id.Value, context.HttpContext.RequestAborted);

        if (principal is null || user is null || !principal.Identifies(user))
        {
            context.RejectPrincipal();
            await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return;
        }

        if (!principal.DisplayMatches(user))
        {
            context.ReplacePrincipal(UserClaims.CreatePrincipal(user));
            context.ShouldRenew = true;
        }
    }
}

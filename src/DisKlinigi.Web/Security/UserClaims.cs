using System.Globalization;
using System.Security.Claims;
using DisKlinigi.Core.Domain.Users;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace DisKlinigi.Web.Security;

/// <summary>
/// Giriş çerezine yazılan bilgiler ve geri okunması. Çerezde parola ya da özeti yok; sadece
/// kullanıcının Id'si, e-postası, rolü ve menüde gösterilecek ad.
/// </summary>
/// <remarks>
/// Çerez tarayıcıda duruyor ama ASP.NET Core'un veri koruma anahtarlarıyla şifreli ve imzalı;
/// kullanıcı içindeki rolü değiştiremiyor. Yine de her istekte veritabanıyla karşılaştırılıyor
/// (<see cref="UserSessionValidator"/>).
/// </remarks>
public static class UserClaims
{
    /// <summary>Bağlı olunan şubenin adı; sadece menüde gösteriliyor, yetki kararında kullanılmıyor.</summary>
    public const string BranchName = "disklinigi:branch";

    public static ClaimsPrincipal CreatePrincipal(User user)
    {
        ArgumentNullException.ThrowIfNull(user);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString(CultureInfo.InvariantCulture)),
            new(ClaimTypes.Name, user.FullName),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.Role, user.Role.ToString())
        };
        if (user.AssignedBranch is { } branch)
            claims.Add(new Claim(BranchName, branch.Name));

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        return new ClaimsPrincipal(identity);
    }

    /// <summary>Girişli kullanıcının Id'si; giriş yapılmamışsa ya da çerez bozuksa <c>null</c>.</summary>
    public static int? GetUserId(this ClaimsPrincipal principal) =>
        int.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), NumberStyles.None,
            CultureInfo.InvariantCulture, out var id)
            ? id
            : null;

    public static UserRole? GetRole(this ClaimsPrincipal principal) =>
        Enum.TryParse<UserRole>(principal.FindFirstValue(ClaimTypes.Role), out var role) ? role : null;

    /// <summary>
    /// Çerezdeki bilgi veritabanındaki kullanıcıyla aynı kişiyi mi gösteriyor? Id, rol ve
    /// e-posta aynı olmalı. Ad ya da şube değişmişse kişi aynı, sadece gösterim eski.
    /// </summary>
    internal static bool Identifies(this ClaimsPrincipal principal, User user) =>
        principal.GetUserId() == user.Id
        && principal.GetRole() == user.Role
        && principal.FindFirstValue(ClaimTypes.Email) == user.Email;

    internal static bool DisplayMatches(this ClaimsPrincipal principal, User user) =>
        principal.FindFirstValue(ClaimTypes.Name) == user.FullName
        && principal.FindFirstValue(BranchName) == user.AssignedBranch?.Name;
}

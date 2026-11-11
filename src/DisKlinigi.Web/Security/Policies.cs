using DisKlinigi.Core.Domain.Users;
using Microsoft.AspNetCore.Authorization;

namespace DisKlinigi.Web.Security;

/// <summary>
/// Sayfa düzeyindeki yetki politikaları: "bu kişi bu ekranı açabilir mi?" (K41).
/// </summary>
/// <remarks>
/// Politikalar sadece kaba bir kapı. "Bu randevuyu onaylayabilir mi?", "bu şubeyi yönetebilir mi?"
/// gibi kayda bağlı sorular burada değil, kullanıcı sınıflarının çok biçimli metotlarında
/// (<c>CanApprove</c>, <c>CanCancel</c>, <c>CanManageBranch</c>) cevaplanıyor; çünkü cevap rolün
/// yanında randevuya ve şubeye de bağlı. Örneğin şube yöneticisi "Şubeler" ekranını açabiliyor
/// ama içinde sadece <c>CanManageBranch</c>'in evet dediği şubeyi görecek.
/// </remarks>
public static class Policies
{
    public const string Patient = nameof(Patient);

    /// <summary>Takvim ve onay kuyruğu: hekim ve asistan.</summary>
    public const string ClinicStaff = nameof(ClinicStaff);

    /// <summary>Müsaitlik aralıkları: sadece hekim, kendi aralıklarını giriyor.</summary>
    public const string Dentist = nameof(Dentist);

    /// <summary>Şube, katalog ve personel ekranları: şube yöneticisi ve zincir yöneticisi.</summary>
    public const string Management = nameof(Management);

    /// <summary>Hekim kaydı ve hekimin ortak bilgileri: sadece zincir yöneticisi (K34).</summary>
    public const string ChainAdministrator = nameof(ChainAdministrator);

    public static void AddTo(AuthorizationOptions options)
    {
        options.AddPolicy(Patient, Roles(UserRole.Patient));
        options.AddPolicy(ClinicStaff, Roles(UserRole.Dentist, UserRole.Assistant));
        options.AddPolicy(Dentist, Roles(UserRole.Dentist));
        options.AddPolicy(Management, Roles(UserRole.BranchManager, UserRole.ChainAdministrator));
        options.AddPolicy(ChainAdministrator, Roles(UserRole.ChainAdministrator));

        // Varsayılan kapalı: [Authorize] yazılmayan bir sayfa da girişsiz açılmıyor.
        // Girişsiz açılması gerekenler (giriş, kayıt, hata) [AllowAnonymous] ile işaretli.
        options.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
    }

    private static Action<AuthorizationPolicyBuilder> Roles(params UserRole[] roles) =>
        policy => policy.RequireRole(roles.Select(r => r.ToString()));
}

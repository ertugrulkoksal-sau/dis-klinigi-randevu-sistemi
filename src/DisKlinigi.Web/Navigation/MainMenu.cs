using DisKlinigi.Core.Domain.Users;

namespace DisKlinigi.Web.Navigation;

/// <summary>Üst menüde bir bağlantı. <paramref name="Badge"/> doluysa yanında o adlı bileşenin çizdiği sayı çıkıyor.</summary>
public sealed record MenuItem(string Text, string Controller, string Action, string? Badge = null);

/// <summary>
/// Role göre üst menü (arayüz tasarımı, "Ortak kararlar"). Girişten sonra herkes kendi
/// menüsünün ilk ekranına yönleniyor.
/// </summary>
/// <remarks>
/// Rol değerine bakan tek yer burası ve <see cref="Security.Policies"/>; <c>UserRole</c>'ün
/// açıklamasında yazdığı gibi rol sadece menüyü seçmek ve çerezde taşınmak için var. Menüde
/// bir bağlantının görünmesi yetki demek değil: her sayfanın kendi politikası var, menüden
/// kaldırılan bir ekran adresi yazılarak da açılamıyor.
/// </remarks>
public static class MainMenu
{
    public static IReadOnlyList<MenuItem> For(UserRole role) => role switch
    {
        UserRole.Patient =>
        [
            new("Randevu al", "Patient", "Book"),
            new("Randevularım", "Patient", "Appointments")
        ],
        UserRole.Dentist =>
        [
            new("Takvim", "Clinic", "Calendar"),
            new("Onay kuyruğu", "Clinic", "Queue", Badge: "QueueCount"),
            new("Müsaitlik", "Clinic", "Availability")
        ],
        UserRole.Assistant =>
        [
            new("Takvim", "Clinic", "Calendar"),
            new("Onay kuyruğu", "Clinic", "Queue", Badge: "QueueCount")
        ],
        // Şube yöneticisi tek şubeyi yönettiği için "Şubem"; personel ekranı şube personeli (5c).
        UserRole.BranchManager =>
        [
            new("Şubem", "Admin", "Branches"),
            new("Bölüm ve hizmetler", "Admin", "Catalog"),
            new("Personel", "Admin", "Staff")
        ],
        // Zincir yöneticisinin personel ekranı hekim kaydı (5d); şube personeline oradan geçiliyor.
        UserRole.ChainAdministrator =>
        [
            new("Şubeler", "Admin", "Branches"),
            new("Bölüm ve hizmetler", "Admin", "Catalog"),
            new("Personel", "Admin", "Dentists")
        ],
        _ => throw new ArgumentOutOfRangeException(nameof(role))
    };

    /// <summary>Rolün giriş sonrası açılan ekranı: menüsünün ilk bağlantısı.</summary>
    public static MenuItem HomeFor(UserRole role) => For(role)[0];
}

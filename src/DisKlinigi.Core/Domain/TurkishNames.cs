using DisKlinigi.Core.Domain.Appointments;
using DisKlinigi.Core.Domain.Users;

namespace DisKlinigi.Core.Domain;

/// <summary>
/// Hata mesajlarında ve ekranda kullanılan Türkçe gün, durum ve rol adları. İşletim sisteminin kültür
/// ayarına bağlı kalmasın diye (macOS/Windows farkı) sabit tablo olarak tutuluyor.
/// </summary>
public static class TurkishNames
{
    public static string Day(DayOfWeek day) => day switch
    {
        DayOfWeek.Monday => "Pazartesi",
        DayOfWeek.Tuesday => "Salı",
        DayOfWeek.Wednesday => "Çarşamba",
        DayOfWeek.Thursday => "Perşembe",
        DayOfWeek.Friday => "Cuma",
        DayOfWeek.Saturday => "Cumartesi",
        DayOfWeek.Sunday => "Pazar",
        _ => throw new ArgumentOutOfRangeException(nameof(day))
    };

    /// <summary>Randevu durumunun ekranda görünen adı (arayüz tasarımındaki rozetlerle aynı).</summary>
    public static string Status(AppointmentStatus status) => status switch
    {
        AppointmentStatus.PendingApproval => "Onay bekliyor",
        AppointmentStatus.Confirmed => "Onaylandı",
        AppointmentStatus.Rejected => "Reddedildi",
        AppointmentStatus.Cancelled => "İptal edildi",
        AppointmentStatus.Completed => "Tamamlandı",
        AppointmentStatus.NoShow => "Gelmedi",
        _ => throw new ArgumentOutOfRangeException(nameof(status))
    };

    /// <summary>Rolün menüde görünen adı (arayüz tasarımındaki "Ali Yıldız · Hasta" satırı).</summary>
    public static string Role(UserRole role) => role switch
    {
        UserRole.Patient => "Hasta",
        UserRole.Dentist => "Hekim",
        UserRole.Assistant => "Asistan",
        UserRole.BranchManager => "Şube yöneticisi",
        UserRole.ChainAdministrator => "Zincir yöneticisi",
        _ => throw new ArgumentOutOfRangeException(nameof(role))
    };
}

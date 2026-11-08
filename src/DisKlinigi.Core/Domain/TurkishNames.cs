using DisKlinigi.Core.Domain.Appointments;

namespace DisKlinigi.Core.Domain;

/// <summary>
/// Hata mesajlarında ve ekranda kullanılan Türkçe gün ve durum adları. İşletim sisteminin kültür
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
}

namespace DisKlinigi.Core.Domain;

/// <summary>
/// Hata mesajlarında kullanılan Türkçe gün adları. İşletim sisteminin kültür
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
}

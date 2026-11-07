namespace DisKlinigi.Core.Domain;

/// <summary>
/// Şubenin haftanın bir günündeki açılış–kapanış saati (ör. Cumartesi 09:00–14:00).
/// Kapalı günler için kayıt tutulmuyor; kaydı olmayan gün kapalı demek.
/// </summary>
/// <remarks>
/// Kendi başına anlamı olmadığı için sadece <see cref="Branch"/> üzerinden oluşturulup
/// değiştirilebiliyor (yapıcı internal). Böylece "aynı güne iki kayıt" gibi bir durum
/// şubenin haberi olmadan ortaya çıkamıyor.
/// </remarks>
public class DailyWorkingHours
{
    public DayOfWeek Day { get; private set; }
    public TimeOfDayRange Hours { get; private set; } = null!;

    private DailyWorkingHours()
    {
    }

    internal DailyWorkingHours(DayOfWeek day, TimeOfDayRange hours)
    {
        Day = day;
        Hours = hours;
    }

    internal void Change(TimeOfDayRange hours)
    {
        Hours = hours;
    }
}

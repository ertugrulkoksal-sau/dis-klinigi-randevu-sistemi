using System.Globalization;
using DisKlinigi.Core.Domain;
using DisKlinigi.Core.Domain.Appointments;
using DisKlinigi.Core.Domain.Users;

namespace DisKlinigi.Web.Models;

/// <summary>
/// Ekrandaki tarih, ücret, süre ve saat özetlerinin biçimi; tasarımdaki yazımla aynı
/// ("5.000 ₺", "60 dk", "10 Kasım 2026, Salı", "Hafta içi 09:00–18:00, Cumartesi 09:00–14:00").
/// </summary>
/// <remarks>
/// Gün adları <see cref="TurkishNames"/>'ten, ay adları tr-TR kültüründen geliyor. Para birimi için
/// kültürün hazır biçimi kullanılmıyor, çünkü tr-TR'de simge başta ("₺5.000,00"); tasarımda sonda
/// ve kuruşsuz. Bu sınıf sadece gösterim; hiçbir iş kuralı yok.
/// </remarks>
public static class Display
{
    private static readonly CultureInfo Turkish = CultureInfo.GetCultureInfo("tr-TR");

    /// <summary>Tarihleri sorgu parametresine yazarken kullanılan biçim (kültürden bağımsız).</summary>
    public const string DateParameterFormat = "yyyy-MM-dd";

    public const string TimeParameterFormat = "HH:mm";

    /// <summary>"5.000 ₺"; kuruş varsa "2.500,50 ₺".</summary>
    public static string Price(decimal price) => price.ToString("#,##0.##", Turkish) + " ₺";

    /// <summary>"60 dk".</summary>
    public static string Minutes(TimeSpan duration) => $"{(int)duration.TotalMinutes} dk";

    /// <summary>"10 Kasım 2026, Salı".</summary>
    public static string LongDate(DateOnly date) =>
        $"{date.Day} {Turkish.DateTimeFormat.GetMonthName(date.Month)} {date.Year}, {TurkishNames.Day(date.DayOfWeek)}";

    /// <summary>"10 Kasım Salı" (yıl olmadan, başlıklarda).</summary>
    public static string DayAndMonth(DateOnly date) =>
        $"{date.Day} {Turkish.DateTimeFormat.GetMonthName(date.Month)} {TurkishNames.Day(date.DayOfWeek)}";

    /// <summary>"10 Kasım 2026 Salı, 11:00–12:00" (onay kuyruğundaki randevu kartlarında).</summary>
    public static string LongDateTime(TimeRange period) =>
        $"{period.Start.Day} {Turkish.DateTimeFormat.GetMonthName(period.Start.Month)} {period.Start.Year} " +
        $"{TurkishNames.Day(period.Start.DayOfWeek)}, {period.ToTimeString()}";

    /// <summary>"7 Kas 09:24" (randevunun alındığı an).</summary>
    public static string ShortMoment(DateTime moment) => $"{ShortDate(DateOnly.FromDateTime(moment))} {moment:HH:mm}";

    /// <summary>Haftanın günleri Pazartesi'den başlayarak (formlardaki gün listesi).</summary>
    public static IReadOnlyList<DayOfWeek> Week => WeekOrder;

    /// <summary>"10 Kas".</summary>
    public static string ShortDate(DateOnly date) =>
        $"{date.Day} {Turkish.DateTimeFormat.GetAbbreviatedMonthName(date.Month)}";

    /// <summary>"10 Kas Sal" (randevu kartlarında).</summary>
    public static string ShortDateWithDay(DateTime moment) =>
        $"{ShortDate(DateOnly.FromDateTime(moment))} {Turkish.DateTimeFormat.GetAbbreviatedDayName(moment.DayOfWeek)}";

    /// <summary>"20 Eki 2026, 14:00" (geçmiş randevular tablosunda).</summary>
    public static string ShortDateTime(DateTime moment) =>
        $"{ShortDate(DateOnly.FromDateTime(moment))} {moment.Year}, {moment:HH:mm}";

    /// <summary>
    /// Şubenin haftalık saatleri tek satırda: aynı saatte açık olan art arda günler birleşiyor,
    /// Pazartesi–Cuma "Hafta içi" yazılıyor. Kapalı günler yazılmıyor.
    /// </summary>
    public static string WorkingHours(Branch branch)
    {
        var parts = new List<string>();
        var days = WeekOrder.Select(day => (Day: day, Hours: branch.GetWorkingHours(day))).ToList();

        for (var i = 0; i < days.Count;)
        {
            var hours = days[i].Hours;
            var j = i;
            while (j + 1 < days.Count && hours is not null && days[j + 1].Hours == hours)
                j++;

            if (hours is not null)
                parts.Add($"{DayRange(days[i].Day, days[j].Day)} {hours}");
            i = j + 1;
        }

        return parts.Count == 0 ? "Şu an randevu verilmiyor" : string.Join(", ", parts);
    }

    /// <summary>
    /// Hekimin bu şubedeki aralıkları: aynı saatteki günler birleşiyor,
    /// ör. "Pazartesi ve Perşembe 09:00–17:00" ya da "Salı 09:00–13:00, Çarşamba 13:00–18:00".
    /// </summary>
    public static string Availability(Dentist dentist, Branch branch)
    {
        var groups = WeekOrder
            .SelectMany(day => dentist.AvailabilityAt(branch, day))
            .GroupBy(window => window.Hours)
            .Select(group => $"{JoinTurkish(group.Select(w => TurkishNames.Day(w.Day)).Distinct().ToList())} {group.Key}");
        return string.Join(", ", groups);
    }

    /// <summary>Hekim kartındaki baş harfler: "Dt. Ayşe Kaya" → "AK".</summary>
    public static string Initials(string fullName)
    {
        var words = fullName.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(w => !w.EndsWith('.'))
            .ToList();
        if (words.Count == 0)
            return "?";
        var first = words[0][0];
        var last = words.Count > 1 ? words[^1][0].ToString() : "";
        return (first + last).ToUpper(Turkish);
    }

    /// <summary>"Kanal tedavisi" → "kanal tedavisi" (cümle içinde; Türkçe büyük İ doğru küçülüyor).</summary>
    public static string InSentence(string name) =>
        name.Length == 0 ? name : char.ToLower(name[0], Turkish) + name[1..];

    /// <summary>
    /// Durum rozetinin metni ve rengi. Onay bekleyen randevunun saati geldiyse "Saati geçti" (K28);
    /// rozetler sadece renge güvenmiyor, her durumun kendi yazısı var (arayüz tasarımı, "Ortak kararlar").
    /// </summary>
    public static (string Text, string CssClass) Status(Appointment appointment, DateTime now)
    {
        if (appointment.IsOverdue(now))
            return ("Saati geçti", "badge-overdue");

        var css = appointment.Status switch
        {
            AppointmentStatus.PendingApproval => "badge-pending",
            AppointmentStatus.Confirmed => "badge-confirmed",
            AppointmentStatus.Rejected => "badge-rejected",
            AppointmentStatus.Cancelled => "badge-cancelled",
            AppointmentStatus.Completed => "badge-completed",
            AppointmentStatus.NoShow => "badge-noshow",
            _ => "badge-cancelled"
        };
        return (TurkishNames.Status(appointment.Status), css);
    }

    private static readonly DayOfWeek[] WeekOrder =
    [
        DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday,
        DayOfWeek.Friday, DayOfWeek.Saturday, DayOfWeek.Sunday
    ];

    private static string DayRange(DayOfWeek first, DayOfWeek last)
    {
        if (first == DayOfWeek.Monday && last == DayOfWeek.Friday)
            return "Hafta içi";
        return first == last ? TurkishNames.Day(first) : $"{TurkishNames.Day(first)}–{TurkishNames.Day(last)}";
    }

    /// <summary>"Salı", "Salı ve Perşembe", "Pazartesi, Çarşamba ve Cuma".</summary>
    private static string JoinTurkish(IReadOnlyList<string> items) => items.Count switch
    {
        0 => "",
        1 => items[0],
        _ => $"{string.Join(", ", items.Take(items.Count - 1))} ve {items[^1]}"
    };
}

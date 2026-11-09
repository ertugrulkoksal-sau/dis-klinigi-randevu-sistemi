using DisKlinigi.Core.Domain;
using DisKlinigi.Core.Domain.Appointments;

namespace DisKlinigi.Core.Booking;

/// <summary>
/// Kuralların ihtiyaç duyduğu dış bilgi: "şu an" ve randevu gününün aktif randevuları.
/// </summary>
/// <remarks>
/// Kurallar veritabanına kendileri gitmiyor; planlayıcı günün randevularını bir kez
/// okuyup bu nesneyle bütün kurallara veriyor (K31). Böylece kurallar saf, test için
/// sadece bir liste yeterli; boş saat üretiminde onlarca aday saat tek sorguyla
/// kontrol ediliyor.
///
/// Sadece aktif randevular (Onay bekliyor, Onaylandı) tutuluyor (K5). Filtre burada
/// yapıldığı için kuralların her birinin "iptal edilen randevuyu sayma" diye ayrıca
/// düşünmesi gerekmiyor; repository fazladan randevu döndürse de sonuç değişmiyor.
/// </remarks>
public sealed class BookingContext
{
    private readonly List<Appointment> _activeAppointments;

    public BookingContext(DateTime now, DateOnly date, IEnumerable<Appointment> appointments)
    {
        ArgumentNullException.ThrowIfNull(appointments);

        Now = now;
        Date = date;
        Day = new TimeRange(date.ToDateTime(TimeOnly.MinValue), date.AddDays(1).ToDateTime(TimeOnly.MinValue));
        _activeAppointments = appointments
            .Where(a => a.IsActive && a.Period.Overlaps(Day))
            .ToList();
    }

    /// <summary>Kontrolün yapıldığı an; planlayıcı bunu <see cref="TimeProvider"/>'dan alıyor (K16).</summary>
    public DateTime Now { get; }

    /// <summary>Bağlamın ait olduğu gün. Başka güne ait istek bu bağlamla kontrol edilemez.</summary>
    public DateOnly Date { get; }

    private TimeRange Day { get; }

    public IReadOnlyList<Appointment> ActiveAppointments => _activeAppointments.AsReadOnly();

    /// <summary>Verilen aralıkla kesişen aktif randevular (yarı açık: uç uca olanlar kesişmiyor).</summary>
    public IEnumerable<Appointment> ActiveOverlapping(TimeRange period) =>
        _activeAppointments.Where(a => a.Period.Overlaps(period));

    /// <summary>
    /// İstek bu bağlamın gününde mi başlıyor? Değilse kurallar başka bir günün randevu
    /// listesiyle çalışıp yanlışlıkla "boş" diyebilirdi; planlayıcı her istekte bunu kontrol ediyor.
    /// Gece yarısını geçen istek burada değil, çalışma saati kuralında reddediliyor.
    /// </summary>
    public bool Covers(BookingRequest request) => request.Date == Date;
}

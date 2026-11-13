using DisKlinigi.Core.Domain;
using DisKlinigi.Core.Domain.Appointments;
using DisKlinigi.Core.Domain.Users;

namespace DisKlinigi.Core.Appointments;

/// <summary>Günlük takvimde bir satır: bir randevu ya da müsaitlik aralığının içindeki boş bir parça.</summary>
public sealed record DayScheduleEntry(TimeRange Period, Appointment? Appointment)
{
    public bool IsFree => Appointment is null;
}

/// <summary>
/// Hekimin bir günü (ekran 4a): o günkü müsaitlik aralıkları, randevular ve aralıkların içinde kalan
/// boş parçalar, saate göre sıralı; üstüne günün özeti (randevu sayısı, onay bekleyen, boş süre) (K53).
/// </summary>
/// <remarks>
/// Takvimde reddedilen ve iptal edilen randevular yok, çünkü saati bırakmışlar (K5); o saat boş görünüyor.
/// Tamamlandı ve Gelmedi ise o saatte koltuğu gerçekten tutmuş randevular, bu yüzden takvimde kalıyorlar.
/// Boş parçalar yarı açık aralık mantığıyla hesaplanıyor: 09:00–09:20 randevusundan sonra 09:20'de boşluk
/// başlıyor. Müsaitlik aralığının dışına düşen bir randevu (aralık sonradan değiştiyse) yine listede görünüyor.
/// </remarks>
public sealed class DaySchedule
{
    private DaySchedule(
        DateOnly date, IReadOnlyList<AvailabilityWindow> windows, IReadOnlyList<DayScheduleEntry> entries)
    {
        Date = date;
        Windows = windows;
        Entries = entries;
    }

    public DateOnly Date { get; }

    /// <summary>Gösterilen şubelerdeki o günkü müsaitlik aralıkları, saate göre sıralı.</summary>
    public IReadOnlyList<AvailabilityWindow> Windows { get; }

    public IReadOnlyList<DayScheduleEntry> Entries { get; }

    public int AppointmentCount => Entries.Count(e => !e.IsFree);

    public int PendingCount => Entries.Count(e => e.Appointment?.Status == AppointmentStatus.PendingApproval);

    public TimeSpan FreeTime => Entries.Where(e => e.IsFree).Aggregate(TimeSpan.Zero, (sum, e) => sum + e.Period.Duration);

    /// <param name="onlyAt">
    /// Sadece bu şubenin aralıkları gösterilsin (asistanın şubesi); <c>null</c> ise hekimin o günkü bütün
    /// aralıkları. Randevular zaten süzülmüş olarak geliyor (<see cref="AppointmentService.GetDayAsync"/>).
    /// </param>
    public static DaySchedule Build(Dentist dentist, DateOnly date, IEnumerable<Appointment> appointments, Branch? onlyAt = null)
    {
        ArgumentNullException.ThrowIfNull(dentist);
        ArgumentNullException.ThrowIfNull(appointments);

        var windows = dentist.Availability
            .Where(w => w.Day == date.DayOfWeek && (onlyAt is null || w.Branch.IsSameAs(onlyAt)))
            .OrderBy(w => w.Hours.Start)
            .ToList();

        var occupying = appointments
            .Where(a => a.IsActive || a.Status is AppointmentStatus.Completed or AppointmentStatus.NoShow)
            .OrderBy(a => a.Period.Start)
            .ToList();

        var entries = occupying.Select(a => new DayScheduleEntry(a.Period, a)).ToList();
        foreach (var window in windows)
            entries.AddRange(FreeParts(window.Hours.On(date), occupying).Select(free => new DayScheduleEntry(free, null)));

        return new DaySchedule(date, windows, entries.OrderBy(e => e.Period.Start).ThenBy(e => e.IsFree).ToList());
    }

    /// <summary>Aralığın randevularla kesişmeyen parçaları; randevular başlangıca göre sıralı geliyor.</summary>
    private static IEnumerable<TimeRange> FreeParts(TimeRange window, IReadOnlyList<Appointment> appointments)
    {
        var cursor = window.Start;
        foreach (var period in appointments.Select(a => a.Period).Where(p => p.Overlaps(window)))
        {
            if (period.Start > cursor)
                yield return new TimeRange(cursor, period.Start);
            if (period.End > cursor)
                cursor = period.End;
        }
        if (cursor < window.End)
            yield return new TimeRange(cursor, window.End);
    }
}

using DisKlinigi.Core.Abstractions;
using DisKlinigi.Core.Domain;
using DisKlinigi.Core.Domain.Appointments;
using DisKlinigi.Core.Domain.Users;

namespace DisKlinigi.Core.Availability;

/// <summary>Bir müsaitlik aralığı ve o aralığa düşen, henüz başlamamış aktif randevular.</summary>
public sealed record WindowUsage(AvailabilityWindow Window, IReadOnlyList<Appointment> UpcomingAppointments)
{
    /// <summary>İleri tarihte randevusu olan aralık silinemiyor.</summary>
    public bool CanRemove => UpcomingAppointments.Count == 0;
}

/// <summary>
/// Hekimin kendi müsaitlik aralıklarını yönetmesi (ekran 4c).
/// </summary>
/// <remarks>
/// Aralık eklemenin kuralları (şubeye atanmış olmak, şube saatine sığmak, başka aralıkla kesişmemek)
/// hekim sınıfında, <see cref="Dentist.AddAvailability"/> içinde (K24); bu servis onları tekrar yazmıyor.
/// Silmenin kuralı ise burada: ileri tarihte aktif randevusu olan aralık silinemiyor. Bu kural hekim
/// sınıfında olamazdı, çünkü hekim nesnesi randevularını taşımıyor; randevular ayrı bir repository'den
/// geliyor (K52). Aralık silinseydi o randevular "hekim müsait değil" kuralını çiğneyen kayıtlar olarak
/// kalırdı.
/// </remarks>
public sealed class AvailabilityService
{
    private readonly IDentistRepository _dentists;
    private readonly IAppointmentRepository _appointments;
    private readonly TimeProvider _time;

    public AvailabilityService(IDentistRepository dentists, IAppointmentRepository appointments, TimeProvider time)
    {
        _dentists = dentists ?? throw new ArgumentNullException(nameof(dentists));
        _appointments = appointments ?? throw new ArgumentNullException(nameof(appointments));
        _time = time ?? throw new ArgumentNullException(nameof(time));
    }

    public DateTime Now => _time.GetLocalNow().DateTime;

    /// <summary>Hekimin aralıkları haftanın gün sırasıyla (Pazartesi önce), her biri ileri tarihli randevularıyla.</summary>
    public async Task<IReadOnlyList<WindowUsage>> GetUsageAsync(Dentist dentist, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dentist);

        var upcoming = await _appointments.GetUpcomingForDentistAsync(dentist, Now, cancellationToken);
        return dentist.Availability
            .OrderBy(w => ((int)w.Day + 6) % 7)
            .ThenBy(w => w.Hours.Start)
            .Select(w => new WindowUsage(w, BookedIn(w, upcoming)))
            .ToList();
    }

    /// <summary>
    /// Aralık ekler. Kurallar hekim sınıfında; ihlal edilirse alan sınıfının mesajı ekrana dönüyor ve
    /// hiçbir şey kaydedilmiyor.
    /// </summary>
    public async Task<ChangeResult> AddAsync(
        Dentist dentist, int branchId, DayOfWeek day, TimeOnly start, TimeOnly end, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dentist);

        // Hekimin listesinde olmayan şube seçilemiyor; adres elle değiştirilse de başka şubeye aralık eklenmiyor.
        var branch = dentist.Branches.FirstOrDefault(b => b.Id == branchId);
        if (branch is null)
            return ChangeResult.Refused("Listeden çalıştığınız şubelerden birini seçin.");

        try
        {
            dentist.AddAvailability(branch, day, new TimeOfDayRange(start, end));
        }
        catch (DomainException ex)
        {
            return ChangeResult.Refused(ex.Message);
        }

        await _dentists.SaveChangesAsync(cancellationToken);
        return ChangeResult.Done;
    }

    /// <summary>
    /// Aralığı siler. Aralık bu hekimin değilse "bulunamadı"; ileri tarihte aktif randevusu varsa reddedilir.
    /// Geçmiş randevular silmeyi engellemiyor, çünkü aralık her hafta tekrar eden bir kalıp ve silinmesi
    /// geçmiş kayıtları değiştirmiyor.
    /// </summary>
    public async Task<ChangeResult> RemoveAsync(Dentist dentist, int windowId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dentist);

        var window = dentist.Availability.FirstOrDefault(w => w.Id == windowId);
        if (window is null)
            return ChangeResult.Missing;

        var booked = BookedIn(window, await _appointments.GetUpcomingForDentistAsync(dentist, Now, cancellationToken));
        if (booked.Count > 0)
            return ChangeResult.Refused(
                $"{TurkishNames.Day(window.Day)} {window.Hours} aralığında ({window.Branch.Name}) ileri tarihte {booked.Count} randevu var; " +
                "aralık silinmeden önce bu randevuların iptal edilmesi gerekiyor.");

        dentist.RemoveAvailability(window);
        await _dentists.SaveChangesAsync(cancellationToken);
        return ChangeResult.Done;
    }

    /// <summary>Aralığa (şube, gün ve saat olarak) düşen randevular; aynı soru randevu kuralında da <see cref="AvailabilityWindow.Covers"/>.</summary>
    private static IReadOnlyList<Appointment> BookedIn(AvailabilityWindow window, IReadOnlyList<Appointment> upcoming) =>
        upcoming.Where(a => window.Covers(a.Branch, a.Period)).ToList();
}

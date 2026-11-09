using DisKlinigi.Core.Abstractions;
using DisKlinigi.Core.Domain;
using DisKlinigi.Core.Domain.Appointments;
using DisKlinigi.Core.Domain.Users;

namespace DisKlinigi.Core.Booking;

/// <summary>
/// Randevu alma ve hastaya gösterilecek boş saatleri üretme.
/// </summary>
/// <remarks>
/// İki iş de aynı kuralları aynı şekilde çalıştırıyor (<see cref="Evaluate"/>); yani ekranda
/// boş görünen saat ile kaydederken kabul edilen saat aynı koddan çıkıyor. Planlayıcı
/// kuralları tek tek bilmiyor, DI'dan gelen <see cref="IBookingRule"/> listesini sırayla
/// çalıştırıyor (K12). Hangi durumda başlanacağı (otomatik onay ya da onay bekliyor)
/// planlayıcının değil randevunun kararı (K2, K30); planlayıcı sadece randevuyu oluşturuyor.
/// </remarks>
public sealed class AppointmentScheduler
{
    /// <summary>Boş saatler müsaitlik aralığının başından itibaren bu adımla üretiliyor (K32).</summary>
    public static readonly TimeSpan SlotStep = TimeSpan.FromMinutes(15);

    private readonly IAppointmentRepository _appointments;
    private readonly IReadOnlyList<IBookingRule> _rules;
    private readonly TimeProvider _time;

    public AppointmentScheduler(IAppointmentRepository appointments, IEnumerable<IBookingRule> rules, TimeProvider time)
    {
        _appointments = appointments ?? throw new ArgumentNullException(nameof(appointments));
        _rules = rules?.ToList() ?? throw new ArgumentNullException(nameof(rules));
        _time = time ?? throw new ArgumentNullException(nameof(time));

        if (_rules.Count == 0)
            throw new ArgumentException("Planlayıcı en az bir kuralla çalışmalıdır.", nameof(rules));
    }

    /// <summary>
    /// Bütün kurallar geçerse randevuyu kaydeder; geçmezse çiğnenen kuralların hepsini döndürür.
    /// </summary>
    /// <remarks>
    /// Günün randevuları işlem açıldıktan sonra okunuyor (K19). Okuma işlemden önce yapılsaydı
    /// aynı saate aynı anda gelen iki istek de "boş" görüp kaydedebilirdi.
    /// </remarks>
    public async Task<BookingResult> BookAsync(BookingRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        await using var transaction = await _appointments.BeginBookingTransactionAsync(cancellationToken);

        var context = await LoadContextAsync(request.Dentist, request.Patient, request.Branch, request.Date, cancellationToken);
        var violations = Evaluate(request, context);
        if (violations.Count > 0)
            return BookingResult.Rejected(violations);

        var appointment = new Appointment(request.Patient, request.Dentist, request.Service, request.Branch, request.Period.Start);
        _appointments.Add(appointment);
        await transaction.CommitAsync(cancellationToken);

        return BookingResult.Booked(appointment);
    }

    /// <summary>
    /// Hastanın bu hekimden, bu şubede, bu hizmet için o gün alabileceği saatler.
    /// </summary>
    /// <remarks>
    /// Aday saatler hekimin o gün o şubedeki müsaitlik aralıklarından çıkıyor: aralığın
    /// başından itibaren <see cref="SlotStep"/> adımlarla, hizmet aralığın sonuna sığdığı
    /// sürece. Her aday kaydetmedeki kuralların aynısından geçiyor; hastanın kendi başka
    /// randevusuyla çakışan saat de listede görünmüyor. Gün için tek sorgu yapılıyor.
    /// İşlem açılmıyor: liste bir öneri, saat kaydederken yeniden kontrol ediliyor.
    /// </remarks>
    public async Task<IReadOnlyList<TimeRange>> GetFreeSlotsAsync(
        Patient patient, Dentist dentist, Service service, Branch branch, DateOnly date,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(patient);
        ArgumentNullException.ThrowIfNull(dentist);
        ArgumentNullException.ThrowIfNull(service);
        ArgumentNullException.ThrowIfNull(branch);

        var windows = dentist.AvailabilityAt(branch, date.DayOfWeek);
        if (windows.Count == 0)
            return [];

        var context = await LoadContextAsync(dentist, patient, branch, date, cancellationToken);
        var slots = new List<TimeRange>();

        foreach (var window in windows)
        {
            var hours = window.Hours.On(date);
            for (var start = hours.Start; start + service.Duration <= hours.End; start += SlotStep)
            {
                var candidate = new BookingRequest(patient, dentist, service, branch, start);
                if (Evaluate(candidate, context).Count == 0)
                    slots.Add(candidate.Period);
            }
        }

        return slots;
    }

    /// <summary>
    /// Bütün kuralları çalıştırır ve çiğnenenlerin mesajlarını kuralların sırasıyla döndürür.
    /// İlk hatada durulmuyor; kullanıcı neyi değiştirmesi gerektiğini bir seferde görüyor.
    /// </summary>
    private List<string> Evaluate(BookingRequest request, BookingContext context)
    {
        if (!context.Covers(request))
            throw new InvalidOperationException("Randevu isteği, kuralların yüklendiği günün dışında.");

        return _rules
            .Select(rule => rule.Check(request, context))
            .Where(result => result.IsViolated)
            .Select(result => result.Message!)
            .ToList();
    }

    private async Task<BookingContext> LoadContextAsync(
        Dentist dentist, Patient patient, Branch branch, DateOnly date, CancellationToken cancellationToken)
    {
        // K10: saatler yerel saat olarak tutuluyor, "şu an" da yerel saat.
        var now = _time.GetLocalNow().DateTime;
        var appointments = await _appointments.GetActiveOnDateAsync(dentist, patient, branch, date, cancellationToken);
        return new BookingContext(now, date, appointments);
    }
}

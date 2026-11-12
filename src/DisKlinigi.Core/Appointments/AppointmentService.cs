using DisKlinigi.Core.Abstractions;
using DisKlinigi.Core.Domain;
using DisKlinigi.Core.Domain.Appointments;
using DisKlinigi.Core.Domain.Users;

namespace DisKlinigi.Core.Appointments;

/// <summary>
/// Var olan randevular üzerindeki işlemler: hastanın randevu listesi ve iptal. Personel paneli
/// (13 Kasım) onay, ret ve sonuç işlemlerini buraya aynı kalıpla ekleyecek.
/// </summary>
/// <remarks>
/// Bu sınıf kural yazmıyor; sırayla (1) randevuyu yüklüyor, (2) kullanıcının randevuyla ilgisi
/// var mı diye bakıyor, (3) işi randevunun kendi metoduna bırakıyor, (4) kaydediyor. Kimin iptal
/// edebileceği (<see cref="User.CanCancel"/>, hasta için 24 saat kuralı) ve hangi durumda iptal
/// edilebileceği (State deseni) alan sınıflarında. Böylece aynı <see cref="CancelAsync"/> hem
/// hastanın hem asistanın iptali için çalışıyor; aradaki fark kullanıcı nesnesinin sınıfında.
///
/// "Şu an" <see cref="TimeProvider"/>'dan alınıyor (K16); 24 saat kuralı testte sabit saatle sınanıyor.
/// </remarks>
public sealed class AppointmentService
{
    private readonly IAppointmentRepository _appointments;
    private readonly TimeProvider _time;

    public AppointmentService(IAppointmentRepository appointments, TimeProvider time)
    {
        _appointments = appointments ?? throw new ArgumentNullException(nameof(appointments));
        _time = time ?? throw new ArgumentNullException(nameof(time));
    }

    /// <summary>Kuralların kontrol edildiği an (K10: yerel saat). Ekran da butonları bu ana göre çiziyor.</summary>
    public DateTime Now => _time.GetLocalNow().DateTime;

    /// <summary>Hastanın bütün randevuları, başlangıca göre sıralı.</summary>
    public Task<IReadOnlyList<Appointment>> GetForPatientAsync(Patient patient, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(patient);
        return _appointments.GetForPatientAsync(patient, cancellationToken);
    }

    /// <summary>
    /// Randevuyu iptal eder. Randevu yoksa ya da kullanıcının randevuyla ilgisi yoksa
    /// <see cref="AppointmentActionOutcome.NotFound"/>; iptal kurallara takılırsa nedeniyle birlikte
    /// <see cref="AppointmentActionOutcome.Refused"/> döner ve hiçbir şey kaydedilmez.
    /// </summary>
    /// <remarks>
    /// İlgisiz kullanıcıya "yetkiniz yok" yerine "bulunamadı" denmesinin sebebi: başka bir hastanın
    /// randevu numarasını deneyen biri, hata mesajından o randevunun var olduğunu ya da durumunu
    /// öğrenmesin. Alan sınıfının fırlattığı <see cref="DomainException"/> burada sonuca çevriliyor;
    /// mesajlar zaten kullanıcıya gösterilecek Türkçe metinler.
    /// </remarks>
    public async Task<AppointmentActionResult> CancelAsync(User by, int appointmentId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(by);

        var appointment = await _appointments.GetByIdAsync(appointmentId, cancellationToken);
        if (appointment is null || !appointment.Involves(by))
            return AppointmentActionResult.NotFound;

        try
        {
            appointment.Cancel(by, Now);
        }
        catch (DomainException ex)
        {
            return AppointmentActionResult.Refused(appointment, ex.Message);
        }

        await _appointments.SaveChangesAsync(cancellationToken);
        return AppointmentActionResult.Done(appointment);
    }
}

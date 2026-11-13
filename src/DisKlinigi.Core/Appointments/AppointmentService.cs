using DisKlinigi.Core.Abstractions;
using DisKlinigi.Core.Domain;
using DisKlinigi.Core.Domain.Appointments;
using DisKlinigi.Core.Domain.Users;

namespace DisKlinigi.Core.Appointments;

/// <summary>
/// Var olan randevular üzerindeki işlemler (iptal, onay, ret, tamamlandı, gelmedi) ve bu işlemlerin
/// yapıldığı listeler (hastanın randevuları, onay kuyruğu, hekimin günü).
/// </summary>
/// <remarks>
/// Bu sınıf kural yazmıyor. Her işlem aynı sırayla yürüyor (<see cref="RunAsync"/>): randevuyu yükle,
/// kullanıcının randevuyla ilgisi var mı bak, işi randevunun kendi metoduna bırak, kaydet. Kimin
/// yapabileceği kullanıcı sınıflarında (<see cref="User.CanApprove"/>, <see cref="User.CanCancel"/>),
/// hangi durumda ve hangi anda yapılabileceği durum sınıflarında (State deseni). Böylece aynı
/// <see cref="CancelAsync"/> hem hastanın hem asistanın iptali için çalışıyor; fark kullanıcı nesnesinin
/// sınıfında. 12 Kasım'da sadece iptal vardı; personel paneli (13 Kasım) diğer dört işlemi aynı
/// kalıpla ekledi ve kalıp tek metoda alındı.
///
/// "Şu an" <see cref="TimeProvider"/>'dan alınıyor (K16); zamana bağlı kurallar testte sabit saatle sınanıyor.
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

    // ---- Listeler ----

    /// <summary>Hastanın bütün randevuları, başlangıca göre sıralı.</summary>
    public Task<IReadOnlyList<Appointment>> GetForPatientAsync(Patient patient, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(patient);
        return _appointments.GetForPatientAsync(patient, cancellationToken);
    }

    /// <summary>
    /// Kullanıcının onay kuyruğu: onay bekleyen ve kullanıcının onaylayabileceği randevular, en yakın tarih
    /// önce. Saati geçmiş bekleyen randevular da burada; ekranda "Saati geçti" ile ve sadece ret butonuyla çıkıyor (K28).
    /// </summary>
    /// <remarks>
    /// Kimin kuyruğunda ne olduğu ayrıca yazılmadı: hekim kendi randevularını, asistan şubesinin randevularını
    /// görüyor, çünkü <see cref="User.CanApprove"/> böyle cevaplıyor (K49). Yönetici ve hasta için liste boş.
    /// </remarks>
    public async Task<IReadOnlyList<Appointment>> GetApprovalQueueAsync(User user, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);
        var pending = await _appointments.GetPendingAsync(cancellationToken);
        return pending.Where(user.CanApprove).ToList();
    }

    /// <summary>
    /// Hekimin günü, bakan kişinin işlem yapabileceği randevularla sınırlı: hekim kendi takviminde bütün
    /// şubelerdeki randevularını, asistan sadece kendi şubesindekileri görüyor (yine <see cref="User.CanApprove"/>).
    /// </summary>
    public async Task<DaySchedule> GetDayAsync(User viewer, Dentist dentist, DateOnly date, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(viewer);
        ArgumentNullException.ThrowIfNull(dentist);

        var appointments = await _appointments.GetForDentistOnDateAsync(dentist, date, cancellationToken);
        return DaySchedule.Build(dentist, date, appointments.Where(viewer.CanApprove), onlyAt: viewer.AssignedBranch);
    }

    // ---- İşlemler ----

    /// <summary>
    /// Randevuyu iptal eder. Randevu yoksa ya da kullanıcının randevuyla ilgisi yoksa
    /// <see cref="AppointmentActionOutcome.NotFound"/>; iptal kurallara takılırsa nedeniyle birlikte
    /// <see cref="AppointmentActionOutcome.Refused"/> döner ve hiçbir şey kaydedilmez.
    /// </summary>
    public Task<AppointmentActionResult> CancelAsync(User by, int appointmentId, CancellationToken cancellationToken = default) =>
        RunAsync(by, appointmentId, (appointment, now) => appointment.Cancel(by, now), cancellationToken);

    public Task<AppointmentActionResult> ApproveAsync(User by, int appointmentId, CancellationToken cancellationToken = default) =>
        RunAsync(by, appointmentId, (appointment, now) => appointment.Approve(by, now), cancellationToken);

    /// <summary>
    /// Randevuyu gerekçesiyle reddeder. Gerekçe burada zorunlu, çünkü hastaya "Randevularım"da gösteriliyor
    /// ve hasta neden reddedildiğini bilmeden aynı randevuyu tekrar alabilir. Alan sınıfında isteğe bağlı
    /// kaldı: gerekçe randevunun tutarlılığıyla ilgili değil, hastaya nasıl haber verildiğiyle ilgili (K51).
    /// </summary>
    public Task<AppointmentActionResult> RejectAsync(User by, int appointmentId, string? reason, CancellationToken cancellationToken = default) =>
        RunAsync(by, appointmentId, (appointment, _) =>
        {
            if (string.IsNullOrWhiteSpace(reason))
                throw new DomainException("Ret gerekçesi yazılmalıdır; hastaya gösterilecek.");
            appointment.Reject(by, reason);
        }, cancellationToken);

    public Task<AppointmentActionResult> CompleteAsync(User by, int appointmentId, CancellationToken cancellationToken = default) =>
        RunAsync(by, appointmentId, (appointment, now) => appointment.Complete(by, now), cancellationToken);

    public Task<AppointmentActionResult> MarkNoShowAsync(User by, int appointmentId, CancellationToken cancellationToken = default) =>
        RunAsync(by, appointmentId, (appointment, now) => appointment.MarkNoShow(by, now), cancellationToken);

    /// <summary>
    /// Bütün işlemlerin ortak sırası: yükle, ilgi kontrolü, işlemi randevuya bırak, kaydet.
    /// </summary>
    /// <remarks>
    /// İlgisiz kullanıcıya "yetkiniz yok" yerine "bulunamadı" denmesinin sebebi: başka bir hastanın
    /// randevu numarasını deneyen biri, hata mesajından o randevunun var olduğunu ya da durumunu
    /// öğrenmesin (K46). Alan sınıfının fırlattığı <see cref="DomainException"/> burada sonuca çevriliyor;
    /// mesajlar zaten kullanıcıya gösterilecek Türkçe metinler. Hata olursa kaydetme çağrılmıyor.
    /// </remarks>
    private async Task<AppointmentActionResult> RunAsync(
        User by, int appointmentId, Action<Appointment, DateTime> action, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(by);

        var appointment = await _appointments.GetByIdAsync(appointmentId, cancellationToken);
        if (appointment is null || !appointment.Involves(by))
            return AppointmentActionResult.NotFound;

        try
        {
            action(appointment, Now);
        }
        catch (DomainException ex)
        {
            return AppointmentActionResult.Refused(appointment, ex.Message);
        }

        await _appointments.SaveChangesAsync(cancellationToken);
        return AppointmentActionResult.Done(appointment);
    }
}

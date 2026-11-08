using DisKlinigi.Core.Domain.Users;

namespace DisKlinigi.Core.Domain.Appointments;

/// <summary>
/// Hasta + hekim + hizmet + şube + zaman aralığından oluşan randevu.
/// </summary>
/// <remarks>
/// Durum değişiklikleri sadece <see cref="Approve"/>, <see cref="Reject"/>,
/// <see cref="Cancel"/>, <see cref="Complete"/> ve <see cref="MarkNoShow"/> ile
/// yapılabiliyor; <see cref="Status"/> dışarıdan atanamıyor. Her metot işi o anki
/// durum nesnesine bırakıyor (State deseni, K13): geçişin geçerli olup olmadığını,
/// zaman koşulunu ve kimin yapabileceğini durum sınıfı kontrol ediyor.
///
/// Zaman metotlara <c>now</c> parametresiyle veriliyor. Randevu saati kendisi
/// okumuyor; "şu an"ı uygulama katmanı <see cref="TimeProvider"/> ile alıp geçiriyor
/// (K16). Böylece sınıf saate bağımlı değil ve testte istenen an doğrudan verilebiliyor.
///
/// Bu sınıf randevunun kendi tutarlılığından sorumlu. Başka randevulara bakan kurallar
/// (hekim/hasta çakışması, ünit kapasitesi, müsaitlik, çalışma saati) tek bir randevunun
/// bilemeyeceği şeyler; onlar <c>IBookingRule</c> sınıflarında ve randevu oluşturulmadan
/// önce kontrol ediliyor (9 Kasım).
/// </remarks>
public class Appointment : Entity
{
    /// <summary>Hasta randevusunu kendisi iptal edecekse randevuya bu süreden fazla olmalı (K4).</summary>
    public static readonly TimeSpan PatientCancellationNotice = TimeSpan.FromHours(24);

    public Patient Patient { get; private set; } = null!;
    public Dentist Dentist { get; private set; } = null!;
    public Branch Branch { get; private set; } = null!;

    /// <summary>Hizmet kaydının kendisi; rapor ve filtreleme için. Gösterilen ad ve ücret aşağıdaki kopyalardan okunuyor.</summary>
    public Service Service { get; private set; } = null!;

    /// <summary>Başlangıç ve bitiş; bitiş = başlangıç + randevu anındaki hizmet süresi.</summary>
    public TimeRange Period { get; private set; } = null!;

    /// <summary>Randevu alındığı andaki hizmet adı (K9).</summary>
    public string ServiceNameAtBooking { get; private set; } = null!;

    /// <summary>Randevu alındığı andaki ücret (K9). Hizmetin ücreti sonradan değişse de bu değişmiyor.</summary>
    public decimal PriceAtBooking { get; private set; }

    public AppointmentStatus Status { get; private set; }

    /// <summary>Reddedilen randevuda hastaya gösterilecek gerekçe; yazılmadıysa null.</summary>
    public string? RejectionReason { get; private set; }

    private Appointment()
    {
    }

    public Appointment(Patient patient, Dentist dentist, Service service, Branch branch, DateTime start)
    {
        Patient = patient ?? throw new DomainException("Randevunun hastası belirtilmelidir.");
        Dentist = dentist ?? throw new DomainException("Randevunun hekimi belirtilmelidir.");
        Service = service ?? throw new DomainException("Randevunun hizmeti belirtilmelidir.");
        Branch = branch ?? throw new DomainException("Randevunun şubesi belirtilmelidir.");

        Period = TimeRange.FromDuration(start, service.Duration);
        ServiceNameAtBooking = service.Name;
        PriceAtBooking = service.Price;
        Status = AppointmentState.Initial(service).Status;
    }

    private AppointmentState State => AppointmentState.For(Status);

    /// <summary>Randevu saati tutuyor mu (Onay bekliyor ya da Onaylandı)? Çakışma kontrolü sadece bunlara bakıyor (K5).</summary>
    public bool IsActive => State.IsActive;

    public TimeSpan Duration => Period.Duration;

    /// <summary>Randevu başlama anı geldi mi? Başlama anı dahil.</summary>
    public bool HasStarted(DateTime now) => now >= Period.Start;

    public void Approve(User by, DateTime now) => State.Approve(this, Require(by), now);

    /// <summary>
    /// Randevuyu reddeder. Gerekçe isteğe bağlı; boş bırakılırsa saklanmıyor.
    /// Gerekçe, geçiş başarılı olduktan sonra yazılıyor; reddetme yetkisi olmayan
    /// birinin denemesi randevuda iz bırakmıyor.
    /// </summary>
    public void Reject(User by, string? reason = null)
    {
        State.Reject(this, Require(by));
        RejectionReason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
    }

    public void Cancel(User by, DateTime now) => State.Cancel(this, Require(by), now);

    public void Complete(User by, DateTime now) => State.Complete(this, Require(by), now);

    public void MarkNoShow(User by, DateTime now) => State.MarkNoShow(this, Require(by), now);

    /// <summary>Sadece durum sınıfları çağırıyor; durum dışarıdan atanamıyor.</summary>
    internal void TransitionTo(AppointmentState next) => Status = next.Status;

    private static User Require(User by) =>
        by ?? throw new DomainException("İşlemi yapan kullanıcı belirtilmelidir.");

    public override string ToString() => $"{Period} {ServiceNameAtBooking}, {Dentist.FullName}";
}

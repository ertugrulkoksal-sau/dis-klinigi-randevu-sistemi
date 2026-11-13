using DisKlinigi.Core.Domain.Users;

namespace DisKlinigi.Core.Domain.Appointments;

/// <summary>
/// Randevu durumlarının ortak tabanı (State deseni, tasarım kararı K13).
/// </summary>
/// <remarks>
/// Her işlem (onayla, reddet, iptal et, tamamlandı, gelmedi) burada varsayılan
/// olarak "bu durumda yapılamaz" hatası fırlatıyor. Bir durum sınıfı sadece kendi
/// izin verdiği işlemleri eziyor. Böylece geçersiz bir geçişe izin vermek için
/// bilerek kod yazmak gerekiyor; unutulan bir geçiş kendiliğinden yasak kalıyor.
///
/// Durum nesnelerinin kendi verisi yok, bu yüzden her durumdan tek bir örnek var
/// ve randevu sadece <see cref="AppointmentStatus"/> değerini saklıyor; durum nesnesi
/// her seferinde bu değerden seçiliyor (<see cref="For"/>). Veritabanından okunan
/// randevuda "enum bir şey, durum nesnesi başka bir şey söylüyor" durumu oluşamıyor (K26).
///
/// İşlem metotları internal: dışarıdan sadece <see cref="Appointment"/> üzerinden
/// çağrılabiliyorlar, randevuyu atlayıp durumu doğrudan değiştirmek mümkün değil.
/// </remarks>
public abstract class AppointmentState
{
    private protected AppointmentState()
    {
    }

    public abstract AppointmentStatus Status { get; }

    /// <summary>
    /// Randevu saati hâlâ tutuyor mu? Onay bekleyen ve onaylanan randevular çakışma
    /// kontrolünde sayılıyor (K5); reddedilen, iptal edilen ve biten randevular saati bırakıyor.
    /// </summary>
    public abstract bool IsActive { get; }

    /// <summary>
    /// Bu durum verilen anda hangi işlemlere izin veriyor? Kimin yaptığına bakmıyor; o soru
    /// kullanıcı sınıflarında (<see cref="Appointment.ActionsFor"/> ikisini birleştiriyor).
    /// Varsayılan cevap "hiçbiri"; aşağıdaki işlem metotları gibi sadece izin veren durum eziyor.
    /// </summary>
    /// <remarks>
    /// Bu metot ekranda butonların çıkması için, işlem metotları ise işlemin kendisi için var;
    /// ikisi aynı koşulları ayrı ayrı yazıyor. Birbirinden kopmasınlar diye
    /// <c>AppointmentActionsTests</c> her durum, her an ve her işlem için "izin var diyorsa işlem
    /// başarılı, yok diyorsa hata" eşleşmesini kontrol ediyor.
    /// </remarks>
    internal virtual AppointmentActions ActionsAt(Appointment appointment, DateTime now) => AppointmentActions.None;

    internal virtual void Approve(Appointment appointment, User by, DateTime now) =>
        throw NotAllowed("onaylanamaz");

    internal virtual void Reject(Appointment appointment, User by) =>
        throw NotAllowed("reddedilemez");

    internal virtual void Cancel(Appointment appointment, User by, DateTime now) =>
        throw NotAllowed("iptal edilemez");

    internal virtual void Complete(Appointment appointment, User by, DateTime now) =>
        throw NotAllowed("tamamlandı olarak işaretlenemez");

    internal virtual void MarkNoShow(Appointment appointment, User by, DateTime now) =>
        throw NotAllowed("gelmedi olarak işaretlenemez");

    private DomainException NotAllowed(string action) =>
        new($"Durumu “{TurkishNames.Status(Status)}” olan randevu {action}.");

    /// <summary>Yeni randevunun ilk durumu hizmete göre belirleniyor (K2).</summary>
    public static AppointmentState Initial(Service service) =>
        service.RequiresApproval ? PendingState.Instance : ConfirmedState.Instance;

    /// <summary>
    /// Saati tutan durumların listesi (K5). Veritabanı sorgusu durum nesnesini çalıştıramadığı
    /// için "aktif randevular" filtresini bu listeyle yapıyor; liste elle yazılmadı, durum
    /// sınıflarının <see cref="IsActive"/> cevabından çıkarılıyor, yani kural tek yerde.
    /// </summary>
    public static IReadOnlyList<AppointmentStatus> ActiveStatuses { get; } =
        Enum.GetValues<AppointmentStatus>().Where(s => For(s).IsActive).ToArray();

    /// <summary>Saklanan durum değerine karşılık gelen durum nesnesi.</summary>
    public static AppointmentState For(AppointmentStatus status) => status switch
    {
        AppointmentStatus.PendingApproval => PendingState.Instance,
        AppointmentStatus.Confirmed => ConfirmedState.Instance,
        AppointmentStatus.Rejected => FinalState.Rejected,
        AppointmentStatus.Cancelled => FinalState.Cancelled,
        AppointmentStatus.Completed => FinalState.Completed,
        AppointmentStatus.NoShow => FinalState.NoShow,
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Tanımsız randevu durumu.")
    };
}

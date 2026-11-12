using DisKlinigi.Core.Domain;
using DisKlinigi.Core.Domain.Appointments;
using DisKlinigi.Core.Domain.Users;

namespace DisKlinigi.Core.Abstractions;

/// <summary>
/// Randevu verisine erişim. Randevu planlayıcısının ve randevu işlemlerinin (<c>AppointmentService</c>)
/// ihtiyaç duyduğu sorgular var, genel amaçlı bir "her şeyi getir" yok (K17);
/// EF Core gerçekleştirimi Web projesinde (<c>EfAppointmentRepository</c>).
/// </summary>
public interface IAppointmentRepository
{
    /// <summary>
    /// Verilen günle kesişen ve bu hekime, bu hastaya ya da bu şubeye ait aktif randevular.
    /// </summary>
    /// <remarks>
    /// Üç kural (hekim çakışması, hasta çakışması, ünit kapasitesi) aynı günün farklı
    /// randevularına bakıyor; üçünü tek sorguyla getirmek için tek metot. Fazladan randevu
    /// döndürmek sonucu bozmuyor (kurallar kendi filtrelerini yapıyor), eksik döndürmek bozar.
    /// </remarks>
    Task<IReadOnlyList<Appointment>> GetActiveOnDateAsync(
        Dentist dentist, Patient patient, Branch branch, DateOnly date, CancellationToken cancellationToken = default);

    /// <summary>
    /// Randevuyu hastası, hekimi, hizmeti ve şubesiyle birlikte getirir; yoksa <c>null</c>.
    /// Hepsi yükleniyor, çünkü yetki soruları (<c>CanCancel</c>, <c>CanApprove</c>) bunlara bakıyor.
    /// </summary>
    Task<Appointment?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>Hastanın bütün randevuları (her durumda), başlangıç saatine göre sıralı; hekim ve şubeyle.</summary>
    Task<IReadOnlyList<Appointment>> GetForPatientAsync(Patient patient, CancellationToken cancellationToken = default);

    /// <summary>Yüklenmiş randevularda yapılan durum değişikliklerini (iptal, onay...) kaydeder.</summary>
    Task SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>Randevuyu kaydedilmek üzere ekler; kayıt <see cref="IBookingTransaction.CommitAsync"/> ile yapılıyor.</summary>
    void Add(Appointment appointment);

    /// <summary>
    /// Kontrol ve kaydı birlikte kapsayan işlemi başlatır (K19). İşlem açık olduğu sürece başka
    /// bir randevu kaydı araya giremiyor; ikinci istek bu işlemin bitmesini bekliyor ve
    /// kontrolünü güncel veriyle yapıyor.
    /// </summary>
    Task<IBookingTransaction> BeginBookingTransactionAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Randevu alma işlemi. <see cref="CommitAsync"/> çağrılmadan kapatılırsa (kural çiğnendiğinde
/// ya da hata olduğunda) eklenen randevu kaydedilmiyor.
/// </summary>
public interface IBookingTransaction : IAsyncDisposable
{
    /// <summary>Eklenen randevuyu kaydeder ve işlemi onaylar.</summary>
    Task CommitAsync(CancellationToken cancellationToken = default);
}

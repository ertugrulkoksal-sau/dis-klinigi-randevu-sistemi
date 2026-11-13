using DisKlinigi.Core.Domain;
using DisKlinigi.Core.Domain.Appointments;

namespace DisKlinigi.Tests.Domain.Appointments;

/// <summary>
/// Ekrandaki butonların kaynağı <see cref="Appointment.ActionsFor"/>: durumun izin verdiği işlemler
/// ile kişinin yetkisinin kesişimi. Randevu 10 Kasım 11:00, Serdivan, Dt. Ayşe Kaya, hasta Ali.
/// </summary>
public class AppointmentActionsTests
{
    private readonly TestClinic _clinic = new();

    private static readonly AppointmentActions[] AllActions =
    [
        AppointmentActions.Approve, AppointmentActions.Reject, AppointmentActions.Cancel,
        AppointmentActions.Complete, AppointmentActions.MarkNoShow
    ];

    /// <summary>Randevudan önce (50 saat), başlama anı ve başladıktan 2 saat sonra.</summary>
    public static TheoryData<AppointmentStatus, string> StatusAndMoment()
    {
        var data = new TheoryData<AppointmentStatus, string>();
        foreach (var status in Enum.GetValues<AppointmentStatus>())
        foreach (var moment in new[] { "önce", "başlama anı", "sonra" })
            data.Add(status, moment);
        return data;
    }

    /// <summary>
    /// Butonların listesi ile işlemlerin kendisi ayrı yerlerde yazıldı (durum sınıflarında
    /// <c>ActionsAt</c> ve işlem metotları). Bu test ikisinin kopmadığını gösteriyor: her durum, her an
    /// ve her işlem için "izin var" diyorsa işlem başarılı, "yok" diyorsa hata veriyor. Hekim
    /// randevunun hem onay hem iptal yetkisine sahip, yani fark sadece durumdan geliyor.
    /// </summary>
    [Theory]
    [MemberData(nameof(StatusAndMoment))]
    public void Izin_verilen_islemler_ile_yapilabilen_islemler_ayni(AppointmentStatus status, string moment)
    {
        var now = Moment(moment);

        foreach (var action in AllActions)
        {
            var appointment = InStatus(status);
            var offered = appointment.ActionsFor(_clinic.Ayse, now).HasFlag(action);

            var succeeded = TryPerform(appointment, action, now);

            Assert.True(offered == succeeded,
                $"{status}, {moment}, {action}: ekranda {(offered ? "var" : "yok")} ama işlem {(succeeded ? "başarılı" : "hata verdi")}.");
        }
    }

    public static TheoryData<string, AppointmentStatus, string, AppointmentActions> Buttons() => new()
    {
        // Onay bekleyen randevu, 50 saat kala
        { "Randevunun hekimi", AppointmentStatus.PendingApproval, "önce", AppointmentActions.Approve | AppointmentActions.Reject | AppointmentActions.Cancel },
        { "Şubenin asistanı", AppointmentStatus.PendingApproval, "önce", AppointmentActions.Approve | AppointmentActions.Reject | AppointmentActions.Cancel },
        { "Randevunun hastası", AppointmentStatus.PendingApproval, "önce", AppointmentActions.Cancel },
        { "Başka hekim", AppointmentStatus.PendingApproval, "önce", AppointmentActions.None },
        { "Başka şubenin asistanı", AppointmentStatus.PendingApproval, "önce", AppointmentActions.None },
        { "Şube yöneticisi", AppointmentStatus.PendingApproval, "önce", AppointmentActions.None },
        { "Zincir yöneticisi", AppointmentStatus.PendingApproval, "önce", AppointmentActions.None },
        // Saati geçmiş bekleyen randevu: sadece ret (K28)
        { "Şubenin asistanı", AppointmentStatus.PendingApproval, "sonra", AppointmentActions.Reject },
        { "Randevunun hastası", AppointmentStatus.PendingApproval, "sonra", AppointmentActions.None },
        // Onaylı randevu: önce iptal, saati gelince sonuç
        { "Randevunun hekimi", AppointmentStatus.Confirmed, "önce", AppointmentActions.Cancel },
        { "Randevunun hekimi", AppointmentStatus.Confirmed, "başlama anı", AppointmentActions.Complete | AppointmentActions.MarkNoShow },
        { "Şubenin asistanı", AppointmentStatus.Confirmed, "sonra", AppointmentActions.Complete | AppointmentActions.MarkNoShow },
        { "Randevunun hastası", AppointmentStatus.Confirmed, "sonra", AppointmentActions.None },
        // Son durumlarda hiçbir buton yok
        { "Randevunun hekimi", AppointmentStatus.Completed, "sonra", AppointmentActions.None },
        { "Şubenin asistanı", AppointmentStatus.Rejected, "önce", AppointmentActions.None },
    };

    [Theory]
    [MemberData(nameof(Buttons))]
    public void Butonlar_kisiye_duruma_ve_zamana_gore_cikar(
        string actor, AppointmentStatus status, string moment, AppointmentActions expected)
    {
        var appointment = InStatus(status);

        Assert.Equal(expected, appointment.ActionsFor(_clinic.Actor(actor), Moment(moment)));
    }

    [Fact]
    public void Hasta_24_saat_kala_iptal_butonunu_kaybeder_asistan_kaybetmez()
    {
        var appointment = _clinic.Book(_clinic.Examination);
        var now = TestClinic.AppointmentStart.AddHours(-24);

        Assert.Equal(AppointmentActions.None, appointment.ActionsFor(_clinic.Ali, now));
        Assert.Equal(AppointmentActions.Cancel, appointment.ActionsFor(_clinic.SerdivanAssistant, now));
    }

    // ---- Yardımcılar ----

    private static DateTime Moment(string name) => name switch
    {
        "önce" => TestClinic.ClockStart,
        "başlama anı" => TestClinic.AppointmentStart,
        "sonra" => TestClinic.AppointmentStart.AddHours(2),
        _ => throw new ArgumentOutOfRangeException(nameof(name))
    };

    /// <summary>İstenen durumda bir randevu; durumlara alan metotlarıyla geçiliyor.</summary>
    private Appointment InStatus(AppointmentStatus status)
    {
        var start = TestClinic.AppointmentStart;
        switch (status)
        {
            case AppointmentStatus.PendingApproval:
                return _clinic.Book(_clinic.CanalTreatment);
            case AppointmentStatus.Confirmed:
                return _clinic.Book(_clinic.Examination);
            case AppointmentStatus.Rejected:
                var rejected = _clinic.Book(_clinic.CanalTreatment);
                rejected.Reject(_clinic.Ayse, "Önce muayene gerekli.");
                return rejected;
            case AppointmentStatus.Cancelled:
                var cancelled = _clinic.Book(_clinic.Examination);
                cancelled.Cancel(_clinic.Ayse, TestClinic.ClockStart);
                return cancelled;
            case AppointmentStatus.Completed:
                var completed = _clinic.Book(_clinic.Examination);
                completed.Complete(_clinic.Ayse, start);
                return completed;
            case AppointmentStatus.NoShow:
                var noShow = _clinic.Book(_clinic.Examination);
                noShow.MarkNoShow(_clinic.Ayse, start);
                return noShow;
            default:
                throw new ArgumentOutOfRangeException(nameof(status));
        }
    }

    private bool TryPerform(Appointment appointment, AppointmentActions action, DateTime now)
    {
        try
        {
            switch (action)
            {
                case AppointmentActions.Approve: appointment.Approve(_clinic.Ayse, now); break;
                case AppointmentActions.Reject: appointment.Reject(_clinic.Ayse, "Gerekçe"); break;
                case AppointmentActions.Cancel: appointment.Cancel(_clinic.Ayse, now); break;
                case AppointmentActions.Complete: appointment.Complete(_clinic.Ayse, now); break;
                case AppointmentActions.MarkNoShow: appointment.MarkNoShow(_clinic.Ayse, now); break;
                default: throw new ArgumentOutOfRangeException(nameof(action));
            }
            return true;
        }
        catch (DomainException)
        {
            return false;
        }
    }
}

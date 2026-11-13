using DisKlinigi.Core.Domain;
using DisKlinigi.Core.Domain.Appointments;

namespace DisKlinigi.Tests.Domain.Appointments;

/// <summary>
/// Rol bazlı yetki: <c>CanApprove</c> ve <c>CanCancel</c> her kullanıcı sınıfında farklı
/// cevaplanıyor (çok biçimlilik) ve randevu işlemleri bu cevaplara uyuyor.
/// </summary>
/// <remarks>
/// Randevu Serdivan'da, Dt. Ayşe Kaya'da, hasta Ali. "Başka hekim" de Serdivan'da
/// çalışıyor; yani hekimin yetkisi şubeye değil randevunun kendisine bağlı.
/// </remarks>
public class AppointmentAuthorizationTests
{
    private readonly TestClinic _clinic = new();

    // ---- Onay / ret / sonuç ----

    public static TheoryData<string, bool> ApproveMatrix() => new()
    {
        { "Randevunun hastası", false },
        { "Başka hasta", false },
        { "Randevunun hekimi", true },
        { "Başka hekim", false },
        { "Şubenin asistanı", true },
        { "Başka şubenin asistanı", false },
        { "Şube yöneticisi", false },      // onay tıbbi karar (K27)
        { "Başka şubenin yöneticisi", false },
        { "Zincir yöneticisi", false },
    };

    [Theory]
    [MemberData(nameof(ApproveMatrix))]
    public void Onaylama_yetkisi_role_ve_randevuya_gore_belirlenir(string actorName, bool allowed)
    {
        var actor = _clinic.Actor(actorName);
        var appointment = _clinic.Book(_clinic.CanalTreatment);

        Assert.Equal(allowed, actor.CanApprove(appointment));

        if (allowed)
        {
            appointment.Approve(actor, _clinic.Now);
            Assert.Equal(AppointmentStatus.Confirmed, appointment.Status);
        }
        else
        {
            var ex = Assert.Throws<DomainException>(() => appointment.Approve(actor, _clinic.Now));
            Assert.Contains("yetkiniz yok", ex.Message);
            Assert.Equal(AppointmentStatus.PendingApproval, appointment.Status);
        }
    }

    [Theory]
    [MemberData(nameof(ApproveMatrix))]
    public void Reddetme_yetkisi_onaylama_yetkisiyle_aynidir(string actorName, bool allowed)
    {
        var actor = _clinic.Actor(actorName);
        var appointment = _clinic.Book(_clinic.CanalTreatment);

        if (allowed)
        {
            appointment.Reject(actor);
            Assert.Equal(AppointmentStatus.Rejected, appointment.Status);
        }
        else
        {
            Assert.Throws<DomainException>(() => appointment.Reject(actor));
            Assert.Equal(AppointmentStatus.PendingApproval, appointment.Status);
        }
    }

    [Theory]
    [MemberData(nameof(ApproveMatrix))]
    public void Sonucu_onaylama_yetkisi_olan_isler(string actorName, bool allowed)
    {
        // K27: tamamlandı/gelmedi için ayrı bir yetki metodu yok, CanApprove kullanılıyor.
        var actor = _clinic.Actor(actorName);
        var appointment = _clinic.Book(_clinic.Examination);
        _clinic.SetNow(TestClinic.AppointmentStart.AddMinutes(20));

        if (allowed)
        {
            appointment.Complete(actor, _clinic.Now);
            Assert.Equal(AppointmentStatus.Completed, appointment.Status);
        }
        else
        {
            Assert.Throws<DomainException>(() => appointment.MarkNoShow(actor, _clinic.Now));
            Assert.Equal(AppointmentStatus.Confirmed, appointment.Status);
        }
    }

    [Fact]
    public void Asistan_baska_subeye_tasininca_eski_subenin_randevusunu_onaylayamaz()
    {
        var appointment = _clinic.Book(_clinic.CanalTreatment);
        Assert.True(_clinic.SerdivanAssistant.CanApprove(appointment));

        _clinic.SerdivanAssistant.TransferTo(_clinic.Kadikoy);

        Assert.False(_clinic.SerdivanAssistant.CanApprove(appointment));
    }

    // ---- İptal ----

    public static TheoryData<string, bool> CancelMatrix() => new()
    {
        { "Randevunun hastası", true },    // randevuya 50 saat var
        { "Başka hasta", false },
        { "Randevunun hekimi", true },
        { "Başka hekim", false },
        { "Şubenin asistanı", true },
        { "Başka şubenin asistanı", false },
        { "Şube yöneticisi", false },      // yöneticiler randevuya dokunmuyor (K34)
        { "Başka şubenin yöneticisi", false },
        { "Zincir yöneticisi", false },
    };

    [Theory]
    [MemberData(nameof(CancelMatrix))]
    public void Iptal_yetkisi_role_ve_randevuya_gore_belirlenir(string actorName, bool allowed)
    {
        var actor = _clinic.Actor(actorName);
        var appointment = _clinic.Book(_clinic.Examination);

        Assert.Equal(allowed, actor.CanCancel(appointment, _clinic.Now));

        if (allowed)
        {
            appointment.Cancel(actor, _clinic.Now);
            Assert.Equal(AppointmentStatus.Cancelled, appointment.Status);
        }
        else
        {
            var ex = Assert.Throws<DomainException>(() => appointment.Cancel(actor, _clinic.Now));
            Assert.Equal("Bu randevuyu iptal etme yetkiniz yok.", ex.Message);
            Assert.Equal(AppointmentStatus.Confirmed, appointment.Status);
        }
    }

    // ---- 24 saat kuralı (K4) ----

    [Theory]
    [InlineData(48 * 60, true)]
    [InlineData(24 * 60 + 1, true)]   // 24 saat 1 dakika: hâlâ iptal edebilir
    [InlineData(24 * 60, false)]      // tam 24 saat: "24 saatten fazla" değil
    [InlineData(24 * 60 - 1, false)]
    [InlineData(60, false)]
    public void Hasta_randevuya_24_saatten_fazla_varken_iptal_edebilir(int minutesBeforeStart, bool allowed)
    {
        var appointment = _clinic.Book(_clinic.Examination);
        _clinic.SetNow(TestClinic.AppointmentStart.AddMinutes(-minutesBeforeStart));

        Assert.Equal(allowed, _clinic.Ali.CanCancel(appointment, _clinic.Now));

        if (allowed)
        {
            appointment.Cancel(_clinic.Ali, _clinic.Now);
            Assert.Equal(AppointmentStatus.Cancelled, appointment.Status);
        }
        else
        {
            var ex = Assert.Throws<DomainException>(() => appointment.Cancel(_clinic.Ali, _clinic.Now));
            Assert.Contains("24 saatten az kaldığı için", ex.Message);
            Assert.Equal(AppointmentStatus.Confirmed, appointment.Status);
        }
    }

    [Fact]
    public void Saat_ilerledikce_hastanin_iptal_hakki_kalkar_ama_hekim_iptal_edebilir()
    {
        var appointment = _clinic.Book(_clinic.Examination);
        _clinic.SetNow(TestClinic.AppointmentStart.AddHours(-26));
        Assert.True(_clinic.Ali.CanCancel(appointment, _clinic.Now));

        _clinic.Clock.Advance(TimeSpan.FromHours(2));   // tam 24 saat kaldı

        Assert.False(_clinic.Ali.CanCancel(appointment, _clinic.Now));
        Assert.True(_clinic.Ayse.CanCancel(appointment, _clinic.Now));
        appointment.Cancel(_clinic.Ayse, _clinic.Now);
        Assert.Equal(AppointmentStatus.Cancelled, appointment.Status);
    }

    [Fact]
    public void Yirmi_dort_saat_kurali_onay_bekleyen_randevuda_da_gecerli()
    {
        // K29: bekleyen randevu da saati ve üniti tutuyor (K5), son dakika iptali yine şubenin işi.
        var appointment = _clinic.Book(_clinic.CanalTreatment);
        _clinic.SetNow(TestClinic.AppointmentStart.AddHours(-23));

        Assert.Throws<DomainException>(() => appointment.Cancel(_clinic.Ali, _clinic.Now));
        Assert.Equal(AppointmentStatus.PendingApproval, appointment.Status);
    }

    [Theory]
    [InlineData("Randevunun hekimi")]
    [InlineData("Şubenin asistanı")]
    public void Personel_son_24_saatte_de_iptal_edebilir(string actorName)
    {
        var appointment = _clinic.Book(_clinic.Examination);
        _clinic.SetNow(TestClinic.AppointmentStart.AddMinutes(-30));

        appointment.Cancel(_clinic.Actor(actorName), _clinic.Now);

        Assert.Equal(AppointmentStatus.Cancelled, appointment.Status);
    }
}

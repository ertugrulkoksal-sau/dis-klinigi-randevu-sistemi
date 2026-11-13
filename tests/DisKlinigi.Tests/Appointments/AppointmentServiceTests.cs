using DisKlinigi.Core.Appointments;
using DisKlinigi.Core.Domain.Appointments;
using DisKlinigi.Tests.Booking;
using DisKlinigi.Tests.Domain.Appointments;

namespace DisKlinigi.Tests.Appointments;

/// <summary>
/// Randevu işlemleri uygulama katmanında: randevu Id'siyle bulunuyor, kullanıcının ilgisi kontrol
/// ediliyor, iş randevunun kendisine bırakılıyor; hata olursa hiçbir şey kaydedilmiyor. Randevu 10 Kasım 11:00, saat 8 Kasım 09:00'dan başlıyor.
/// </summary>
public class AppointmentServiceTests
{
    private readonly TestClinic _clinic = new();
    private readonly InMemoryAppointmentRepository _repository;
    private readonly Appointment _appointment;
    private readonly AppointmentService _service;

    public AppointmentServiceTests()
    {
        _appointment = _clinic.Book(_clinic.CanalTreatment);
        _repository = new InMemoryAppointmentRepository(_appointment);
        _service = new AppointmentService(_repository, _clinic.Clock);
    }

    [Fact]
    public async Task Hasta_24_saatten_fazla_kala_randevusunu_iptal_eder()
    {
        _clinic.SetNow(TestClinic.AppointmentStart.AddHours(-24).AddMinutes(-1));

        var result = await _service.CancelAsync(_clinic.Ali, _appointment.Id);

        Assert.True(result.Succeeded);
        Assert.Equal(AppointmentStatus.Cancelled, _appointment.Status);
        Assert.Equal(1, _repository.SaveCount);
    }

    [Fact]
    public async Task Tam_24_saat_kala_hasta_iptal_edemez_ve_hicbir_sey_kaydedilmez()
    {
        _clinic.SetNow(TestClinic.AppointmentStart.AddHours(-24));

        var result = await _service.CancelAsync(_clinic.Ali, _appointment.Id);

        Assert.Equal(AppointmentActionOutcome.Refused, result.Outcome);
        Assert.Contains("24 saatten az kaldığı için", result.Error);
        Assert.Equal(AppointmentStatus.PendingApproval, _appointment.Status);
        Assert.Equal(0, _repository.SaveCount);
    }

    [Fact]
    public async Task Son_24_saatte_sube_asistani_iptal_edebilir()
    {
        // Aynı servis metodu; farkı kullanıcı sınıfının CanCancel cevabı yapıyor (çok biçimlilik).
        _clinic.SetNow(TestClinic.AppointmentStart.AddHours(-2));

        var byPatient = await _service.CancelAsync(_clinic.Ali, _appointment.Id);
        var byAssistant = await _service.CancelAsync(_clinic.SerdivanAssistant, _appointment.Id);

        Assert.False(byPatient.Succeeded);
        Assert.True(byAssistant.Succeeded);
        Assert.Equal(AppointmentStatus.Cancelled, _appointment.Status);
    }

    [Theory]
    [InlineData("Başka hasta")]
    [InlineData("Başka hekim")]
    [InlineData("Başka şubenin asistanı")]
    [InlineData("Şube yöneticisi")]
    [InlineData("Zincir yöneticisi")]
    public async Task Randevuyla_ilgisi_olmayan_kullanici_icin_randevu_bulunamaz(string actor)
    {
        // "Yetkiniz yok" denseydi randevunun var olduğu anlaşılırdı. Yöneticiler randevuya dokunmuyor (K34).
        var result = await _service.CancelAsync(_clinic.Actor(actor), _appointment.Id);

        Assert.Equal(AppointmentActionOutcome.NotFound, result.Outcome);
        Assert.Null(result.Appointment);
        Assert.Equal(AppointmentStatus.PendingApproval, _appointment.Status);
        Assert.Equal(0, _repository.SaveCount);
    }

    [Fact]
    public async Task Olmayan_randevu_bulunamaz()
    {
        var result = await _service.CancelAsync(_clinic.Ali, _appointment.Id + 100);

        Assert.Equal(AppointmentActionOutcome.NotFound, result.Outcome);
    }

    [Fact]
    public async Task Iptal_edilmis_randevu_tekrar_iptal_edilemez()
    {
        await _service.CancelAsync(_clinic.Ali, _appointment.Id);

        var again = await _service.CancelAsync(_clinic.Ali, _appointment.Id);

        Assert.Equal(AppointmentActionOutcome.Refused, again.Outcome);
        Assert.Equal("Durumu “İptal edildi” olan randevu iptal edilemez.", again.Error);
        Assert.Equal(1, _repository.SaveCount);
    }

    [Fact]
    public async Task Saati_gelmis_randevu_iptal_edilemez()
    {
        _clinic.SetNow(TestClinic.AppointmentStart);

        var result = await _service.CancelAsync(_clinic.SerdivanAssistant, _appointment.Id);

        Assert.Equal("Saati gelmiş randevu iptal edilemez.", result.Error);
    }

    [Fact]
    public async Task Hastanin_listesinde_sadece_kendi_randevulari_baslangica_gore_sirali_gelir()
    {
        var later = new Appointment(_clinic.Ali, _clinic.Ayse, _clinic.Examination, _clinic.Serdivan,
            TestClinic.AppointmentStart.AddDays(7), createdAt: TestClinic.ClockStart);
        var earlier = new Appointment(_clinic.Ali, _clinic.Ayse, _clinic.Examination, _clinic.Serdivan,
            TestClinic.AppointmentStart.AddDays(-7), createdAt: TestClinic.ClockStart.AddDays(-7));
        var someoneElse = new Appointment(_clinic.Zeynep, _clinic.Ayse, _clinic.Examination, _clinic.Serdivan,
            TestClinic.AppointmentStart.AddHours(2), createdAt: TestClinic.ClockStart);
        var service = new AppointmentService(
            new InMemoryAppointmentRepository(later, _appointment, someoneElse, earlier), _clinic.Clock);

        var list = await service.GetForPatientAsync(_clinic.Ali);

        Assert.Equal([earlier, _appointment, later], list);
    }

    // ---- Personel işlemleri (13 Kasım) ----

    [Fact]
    public async Task Hekim_bekleyen_randevuyu_onaylar_ve_kaydedilir()
    {
        var result = await _service.ApproveAsync(_clinic.Ayse, _appointment.Id);

        Assert.True(result.Succeeded);
        Assert.Equal(AppointmentStatus.Confirmed, _appointment.Status);
        Assert.Equal(1, _repository.SaveCount);
    }

    [Fact]
    public async Task Onaylanmis_randevu_tekrar_onaylanamaz()
    {
        await _service.ApproveAsync(_clinic.Ayse, _appointment.Id);

        var again = await _service.ApproveAsync(_clinic.SerdivanAssistant, _appointment.Id);

        Assert.Equal(AppointmentActionOutcome.Refused, again.Outcome);
        Assert.Equal("Durumu “Onaylandı” olan randevu onaylanamaz.", again.Error);
        Assert.Equal(1, _repository.SaveCount);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("   ")]
    public async Task Ret_gerekcesiz_yapilamaz(string? reason)
    {
        var result = await _service.RejectAsync(_clinic.SerdivanAssistant, _appointment.Id, reason);

        Assert.Equal(AppointmentActionOutcome.Refused, result.Outcome);
        Assert.Equal("Ret gerekçesi yazılmalıdır; hastaya gösterilecek.", result.Error);
        Assert.Equal(AppointmentStatus.PendingApproval, _appointment.Status);
        Assert.Equal(0, _repository.SaveCount);
    }

    [Fact]
    public async Task Ret_gerekcesi_randevuya_yazilir()
    {
        var result = await _service.RejectAsync(_clinic.SerdivanAssistant, _appointment.Id, " Önce muayene randevusu alın. ");

        Assert.True(result.Succeeded);
        Assert.Equal(AppointmentStatus.Rejected, _appointment.Status);
        Assert.Equal("Önce muayene randevusu alın.", _appointment.RejectionReason);
    }

    [Fact]
    public async Task Cok_uzun_ret_gerekcesi_kabul_edilmez()
    {
        var result = await _service.RejectAsync(_clinic.Ayse, _appointment.Id, new string('a', Appointment.MaxRejectionReasonLength + 1));

        Assert.Equal("Ret gerekçesi en fazla 500 karakter olabilir.", result.Error);
        Assert.Equal(AppointmentStatus.PendingApproval, _appointment.Status);
    }

    [Fact]
    public async Task Saati_gecmis_bekleyen_randevu_onaylanamaz_ama_reddedilebilir()
    {
        _clinic.SetNow(TestClinic.AppointmentStart.AddMinutes(5));

        var approve = await _service.ApproveAsync(_clinic.Ayse, _appointment.Id);
        var reject = await _service.RejectAsync(_clinic.Ayse, _appointment.Id, "Saati geçti.");

        Assert.Equal("Saati gelmiş randevu onaylanamaz; reddedilerek kapatılmalıdır.", approve.Error);
        Assert.True(reject.Succeeded);
    }

    [Fact]
    public async Task Sonuc_saat_gelmeden_islenemez_saat_gelince_islenir()
    {
        var appointment = _clinic.Book(_clinic.Examination);
        var repository = new InMemoryAppointmentRepository(appointment);
        var service = new AppointmentService(repository, _clinic.Clock);

        var early = await service.CompleteAsync(_clinic.Ayse, appointment.Id);
        _clinic.SetNow(TestClinic.AppointmentStart);
        var noShow = await service.MarkNoShowAsync(_clinic.SerdivanAssistant, appointment.Id);

        Assert.Equal("Randevu saati gelmeden sonucu işlenemez.", early.Error);
        Assert.True(noShow.Succeeded);
        Assert.Equal(AppointmentStatus.NoShow, appointment.Status);
        Assert.Equal(1, repository.SaveCount);
    }

    [Theory]
    [InlineData("Randevunun hastası")]
    [InlineData("Başka hekim")]
    [InlineData("Başka şubenin asistanı")]
    [InlineData("Şube yöneticisi")]
    public async Task Onay_yetkisi_olmayana_randevu_bulunamaz_ya_da_islem_reddedilir(string actor)
    {
        // Hasta randevuyla ilgili (kendisinin) ama onaylayamıyor: reddedilir. Diğerleri randevuyu hiç göremez.
        var result = await _service.ApproveAsync(_clinic.Actor(actor), _appointment.Id);

        Assert.False(result.Succeeded);
        Assert.Equal(actor == "Randevunun hastası" ? AppointmentActionOutcome.Refused : AppointmentActionOutcome.NotFound, result.Outcome);
        Assert.Equal(AppointmentStatus.PendingApproval, _appointment.Status);
    }

    // ---- Onay kuyruğu ----

    [Fact]
    public async Task Kuyrukta_hekim_kendi_asistan_subesinin_bekleyen_randevularini_gorur()
    {
        _clinic.Mehmet.OfferService(_clinic.CanalTreatment);
        _clinic.Ayse.AssignToBranch(_clinic.Kadikoy);
        var mehmetsPending = new Appointment(_clinic.Zeynep, _clinic.Mehmet, _clinic.CanalTreatment, _clinic.Serdivan,
            TestClinic.AppointmentStart.AddHours(-2), createdAt: TestClinic.ClockStart);
        var aysesKadikoy = new Appointment(_clinic.Zeynep, _clinic.Ayse, _clinic.CanalTreatment, _clinic.Kadikoy,
            TestClinic.AppointmentStart.AddDays(1), createdAt: TestClinic.ClockStart);
        var confirmed = _clinic.Book(_clinic.Examination);
        var service = new AppointmentService(
            new InMemoryAppointmentRepository(aysesKadikoy, _appointment, confirmed, mehmetsPending), _clinic.Clock);

        Assert.Equal([_appointment, aysesKadikoy], await service.GetApprovalQueueAsync(_clinic.Ayse));
        Assert.Equal([mehmetsPending, _appointment], await service.GetApprovalQueueAsync(_clinic.SerdivanAssistant));
        Assert.Equal([aysesKadikoy], await service.GetApprovalQueueAsync(_clinic.KadikoyAssistant));
        Assert.Empty(await service.GetApprovalQueueAsync(_clinic.Admin));
        Assert.Empty(await service.GetApprovalQueueAsync(_clinic.Ali));
    }

    [Fact]
    public void Simdi_zaman_saglayicisindan_okunur()
    {
        Assert.Equal(TestClinic.ClockStart, _service.Now);
    }
}

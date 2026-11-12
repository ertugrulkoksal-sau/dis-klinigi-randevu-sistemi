using DisKlinigi.Core.Appointments;
using DisKlinigi.Core.Domain.Appointments;
using DisKlinigi.Tests.Booking;
using DisKlinigi.Tests.Domain.Appointments;

namespace DisKlinigi.Tests.Appointments;

/// <summary>
/// Randevu iptali uygulama katmanında: randevu Id'siyle bulunuyor, kullanıcının ilgisi kontrol
/// ediliyor, iş randevunun kendisine bırakılıyor. Randevu 10 Kasım 11:00, saat 8 Kasım 09:00'dan başlıyor.
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

    [Fact]
    public void Simdi_zaman_saglayicisindan_okunur()
    {
        Assert.Equal(TestClinic.ClockStart, _service.Now);
    }
}

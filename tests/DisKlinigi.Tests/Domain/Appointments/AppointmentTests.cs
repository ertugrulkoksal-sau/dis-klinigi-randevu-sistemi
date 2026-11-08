using DisKlinigi.Core.Domain;
using DisKlinigi.Core.Domain.Appointments;

namespace DisKlinigi.Tests.Domain.Appointments;

/// <summary>Randevunun oluşturulması, kopyalanan hizmet bilgileri ve zamana bağlı geçiş koşulları.</summary>
public class AppointmentTests
{
    private readonly TestClinic _clinic = new();

    // ---- Oluşturma ----

    [Fact]
    public void Onay_gerektiren_hizmette_randevu_onay_bekliyor_olarak_olusur()
    {
        var appointment = _clinic.Book(_clinic.CanalTreatment);

        Assert.Equal(AppointmentStatus.PendingApproval, appointment.Status);
        Assert.True(appointment.IsActive);
    }

    [Fact]
    public void Otomatik_onayli_hizmette_randevu_onaylandi_olarak_olusur()
    {
        var appointment = _clinic.Book(_clinic.Examination);

        Assert.Equal(AppointmentStatus.Confirmed, appointment.Status);
    }

    [Fact]
    public void Bitis_hizmet_suresine_gore_hesaplanir()
    {
        var appointment = _clinic.Book(_clinic.CanalTreatment);

        Assert.Equal(TestClinic.AppointmentStart, appointment.Period.Start);
        Assert.Equal(TestClinic.AppointmentStart.AddMinutes(60), appointment.Period.End);
        Assert.Equal(TimeSpan.FromMinutes(60), appointment.Duration);
    }

    [Fact]
    public void Hizmet_sonradan_degisse_de_randevudaki_ad_ucret_ve_sure_degismez()
    {
        // K9: randevu alındığı andaki değerler randevuya kopyalanıyor.
        var appointment = _clinic.Book(_clinic.CanalTreatment);

        _clinic.CanalTreatment.ChangePrice(6500m);
        _clinic.CanalTreatment.ChangeDuration(TimeSpan.FromMinutes(90));
        _clinic.CanalTreatment.Rename("Kanal tedavisi (tek seans)");

        Assert.Equal(5000m, appointment.PriceAtBooking);
        Assert.Equal("Kanal tedavisi", appointment.ServiceNameAtBooking);
        Assert.Equal(TimeSpan.FromMinutes(60), appointment.Duration);
    }

    [Fact]
    public void Hasta_hekim_hizmet_ve_sube_olmadan_randevu_olusturulamaz()
    {
        var c = _clinic;
        var start = TestClinic.AppointmentStart;

        Assert.Throws<DomainException>(() => new Appointment(null!, c.Ayse, c.Examination, c.Serdivan, start));
        Assert.Throws<DomainException>(() => new Appointment(c.Ali, null!, c.Examination, c.Serdivan, start));
        Assert.Throws<DomainException>(() => new Appointment(c.Ali, c.Ayse, null!, c.Serdivan, start));
        Assert.Throws<DomainException>(() => new Appointment(c.Ali, c.Ayse, c.Examination, null!, start));
    }

    [Fact]
    public void Islemi_yapan_kullanici_belirtilmeden_durum_degismez()
    {
        var appointment = _clinic.Book(_clinic.CanalTreatment);

        Assert.Throws<DomainException>(() => appointment.Approve(null!, _clinic.Now));
        Assert.Equal(AppointmentStatus.PendingApproval, appointment.Status);
    }

    // ---- Ret gerekçesi ----

    [Fact]
    public void Ret_gerekcesi_bosluklari_temizlenerek_saklanir()
    {
        var appointment = _clinic.Book(_clinic.CanalTreatment);

        appointment.Reject(_clinic.Ayse, "  Önce muayene randevusu alınmalı.  ");

        Assert.Equal(AppointmentStatus.Rejected, appointment.Status);
        Assert.Equal("Önce muayene randevusu alınmalı.", appointment.RejectionReason);
        Assert.False(appointment.IsActive);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Bos_ret_gerekcesi_saklanmaz(string? reason)
    {
        var appointment = _clinic.Book(_clinic.CanalTreatment);

        appointment.Reject(_clinic.Ayse, reason);

        Assert.Null(appointment.RejectionReason);
    }

    [Fact]
    public void Yetkisiz_ret_denemesi_randevuda_iz_birakmaz()
    {
        var appointment = _clinic.Book(_clinic.CanalTreatment);

        Assert.Throws<DomainException>(() => appointment.Reject(_clinic.Mehmet, "Benim hastam değil."));

        Assert.Equal(AppointmentStatus.PendingApproval, appointment.Status);
        Assert.Null(appointment.RejectionReason);
    }

    // ---- Zamana bağlı koşullar ----

    [Fact]
    public void Saati_gelmis_bekleyen_randevu_onaylanamaz_ama_reddedilebilir()
    {
        var appointment = _clinic.Book(_clinic.CanalTreatment);
        _clinic.SetNow(TestClinic.AppointmentStart);

        var ex = Assert.Throws<DomainException>(() => appointment.Approve(_clinic.Ayse, _clinic.Now));
        Assert.Contains("Saati gelmiş", ex.Message);
        Assert.Equal(AppointmentStatus.PendingApproval, appointment.Status);

        appointment.Reject(_clinic.Ayse, "Onaylanmadan saati geçti.");
        Assert.Equal(AppointmentStatus.Rejected, appointment.Status);
    }

    [Fact]
    public void Bekleyen_randevu_baslamadan_bir_dakika_once_onaylanabilir()
    {
        var appointment = _clinic.Book(_clinic.CanalTreatment);
        _clinic.SetNow(TestClinic.AppointmentStart.AddMinutes(-1));

        appointment.Approve(_clinic.Ayse, _clinic.Now);

        Assert.Equal(AppointmentStatus.Confirmed, appointment.Status);
    }

    [Theory]
    [InlineData(-60)]
    [InlineData(-1)]
    public void Saati_gelmemis_randevu_tamamlandi_ya_da_gelmedi_yapilamaz(int minutesFromStart)
    {
        var appointment = _clinic.Book(_clinic.Examination);
        _clinic.SetNow(TestClinic.AppointmentStart.AddMinutes(minutesFromStart));

        Assert.Throws<DomainException>(() => appointment.Complete(_clinic.Ayse, _clinic.Now));
        Assert.Throws<DomainException>(() => appointment.MarkNoShow(_clinic.Ayse, _clinic.Now));
        Assert.Equal(AppointmentStatus.Confirmed, appointment.Status);
    }

    [Fact]
    public void Randevu_tam_baslama_aninda_tamamlandi_yapilabilir()
    {
        // Başlama anı dahil: hasta koltuğa oturduğu an sonuç işlenebilir.
        var appointment = _clinic.Book(_clinic.Examination);
        _clinic.SetNow(TestClinic.AppointmentStart);

        appointment.Complete(_clinic.Ayse, _clinic.Now);

        Assert.Equal(AppointmentStatus.Completed, appointment.Status);
        Assert.False(appointment.IsActive);
    }

    [Fact]
    public void Randevu_gunler_sonra_da_gelmedi_olarak_isaretlenebilir()
    {
        var appointment = _clinic.Book(_clinic.Examination);
        _clinic.SetNow(TestClinic.AppointmentStart.AddDays(3));

        appointment.MarkNoShow(_clinic.Ayse, _clinic.Now);

        Assert.Equal(AppointmentStatus.NoShow, appointment.Status);
    }

    [Theory]
    [InlineData("Randevunun hekimi")]
    [InlineData("Şubenin asistanı")]
    [InlineData("Zincir yöneticisi")]
    public void Saati_gelmis_randevuyu_kimse_iptal_edemez(string actorName)
    {
        // Saati gelen randevu iptal edilmiyor; tamamlandı/gelmedi olarak kapatılıyor (K28).
        var appointment = _clinic.Book(_clinic.Examination);
        _clinic.SetNow(TestClinic.AppointmentStart);

        var ex = Assert.Throws<DomainException>(() => appointment.Cancel(_clinic.Actor(actorName), _clinic.Now));

        Assert.Contains("Saati gelmiş", ex.Message);
        Assert.Equal(AppointmentStatus.Confirmed, appointment.Status);
    }

    [Fact]
    public void Reddedilen_ve_iptal_edilen_randevu_saati_birakir()
    {
        var rejected = _clinic.Book(_clinic.CanalTreatment);
        var cancelled = _clinic.Book(_clinic.Examination);

        rejected.Reject(_clinic.SerdivanAssistant);
        cancelled.Cancel(_clinic.Ali, _clinic.Now);

        Assert.False(rejected.IsActive);
        Assert.False(cancelled.IsActive);
    }
}

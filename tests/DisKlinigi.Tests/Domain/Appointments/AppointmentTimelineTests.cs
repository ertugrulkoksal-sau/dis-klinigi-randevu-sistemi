namespace DisKlinigi.Tests.Domain.Appointments;

/// <summary>
/// "Randevularım" ekranının ayrımı: yaklaşan mı, saati geçmiş bekleyen mi, kullanıcıyla ilgili mi?
/// Randevu 10 Kasım 11:00.
/// </summary>
public class AppointmentTimelineTests
{
    private readonly TestClinic _clinic = new();
    private static readonly DateTime Start = TestClinic.AppointmentStart;

    [Fact]
    public void Baslamamis_aktif_randevu_yaklasandir_baslama_aninda_degildir()
    {
        var appointment = _clinic.Book(_clinic.Examination);

        Assert.True(appointment.IsUpcoming(Start.AddMinutes(-1)));
        Assert.False(appointment.IsUpcoming(Start));
    }

    [Fact]
    public void Iptal_edilen_randevu_ileri_tarihte_olsa_da_yaklasan_degildir()
    {
        var appointment = _clinic.Book(_clinic.Examination);
        appointment.Cancel(_clinic.Ali, TestClinic.ClockStart);

        Assert.False(appointment.IsUpcoming(TestClinic.ClockStart));
    }

    [Fact]
    public void Saati_gelen_bekleyen_randevu_saati_gecmis_sayilir()
    {
        var appointment = _clinic.Book(_clinic.CanalTreatment);

        Assert.False(appointment.IsOverdue(Start.AddMinutes(-1)));
        Assert.True(appointment.IsOverdue(Start));
    }

    [Fact]
    public void Onaylanmis_randevunun_saati_gecse_de_saati_gecmis_sayilmaz()
    {
        // Onaylı randevu tamamlandı/gelmedi ile kapanıyor; "Saati geçti" sadece onaylanmamışlar için.
        var appointment = _clinic.Book(_clinic.Examination);

        Assert.False(appointment.IsOverdue(Start.AddHours(1)));
    }

    [Theory]
    [InlineData("Randevunun hastası", true)]
    [InlineData("Randevunun hekimi", true)]
    [InlineData("Şubenin asistanı", true)]
    [InlineData("Başka hasta", false)]
    [InlineData("Başka hekim", false)]
    [InlineData("Başka şubenin asistanı", false)]
    [InlineData("Şube yöneticisi", false)]
    [InlineData("Zincir yöneticisi", false)]
    public void Randevuyla_ilgili_olanlar_hastasi_ve_onaylayabilecek_personeldir(string actor, bool expected)
    {
        var appointment = _clinic.Book(_clinic.CanalTreatment);

        Assert.Equal(expected, appointment.Involves(_clinic.Actor(actor)));
    }
}

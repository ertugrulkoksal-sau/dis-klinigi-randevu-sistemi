using DisKlinigi.Core.Domain;
using DisKlinigi.Tests.Booking;

namespace DisKlinigi.Tests.Domain;

/// <summary>
/// Randevu ekranındaki hekim listesi ve gün seçimi: hangi hekim bu şubede bu hizmeti verebilir,
/// hangi günler çalışıyor? (BookingClinic: Ayşe Serdivan ve Kadıköy'de Salı; Kadıköy'de Periodontoloji yok.)
/// </summary>
public class DentistBookingOptionsTests
{
    private readonly BookingClinic _clinic = new();

    [Fact]
    public void Subede_calisan_hizmeti_veren_ve_musaitligi_olan_hekim_listelenir()
    {
        Assert.True(_clinic.Ayse.CanServe(_clinic.Serdivan, _clinic.CanalTreatment));
        Assert.True(_clinic.Ayse.CanServe(_clinic.Kadikoy, _clinic.CanalTreatment));
    }

    [Fact]
    public void Hizmeti_vermeyen_hekim_listelenmez()
    {
        Assert.False(_clinic.Mehmet.CanServe(_clinic.Serdivan, _clinic.CanalTreatment));
    }

    [Fact]
    public void Subeye_atanmamis_hekim_listelenmez()
    {
        Assert.False(_clinic.Mehmet.CanServe(_clinic.Kadikoy, _clinic.Examination));
    }

    [Fact]
    public void Sube_hizmetin_bolumunu_barindirmiyorsa_hekim_listelenmez()
    {
        // Ayşe temizlik veriyor ve Kadıköy'de çalışıyor; ama Kadıköy'de Periodontoloji yok (K33).
        Assert.True(_clinic.Ayse.Offers(_clinic.Cleaning));
        Assert.False(_clinic.Ayse.CanServe(_clinic.Kadikoy, _clinic.Cleaning));
    }

    [Fact]
    public void Subede_musaitlik_araligi_olmayan_hekim_listelenmez()
    {
        // Şubeye atanmış ama o şubede hiç çalışma aralığı yok: listede görünse boş saati hiç çıkmazdı.
        _clinic.Mehmet.AssignToBranch(_clinic.Kadikoy);

        Assert.False(_clinic.Mehmet.CanServe(_clinic.Kadikoy, _clinic.Examination));
    }

    [Fact]
    public void Calisilan_gunler_sadece_o_subedeki_aralik_gunleridir()
    {
        // Ayşe Kadıköy'de Perşembe de çalışsın; Serdivan listesine Perşembe gelmemeli.
        _clinic.Kadikoy.SetWorkingHours(DayOfWeek.Thursday, TimeOfDayRange.Of(9, 0, 19, 0));
        _clinic.Ayse.AddAvailability(_clinic.Kadikoy, DayOfWeek.Thursday, TimeOfDayRange.Of(9, 0, 12, 0));
        var monday = new DateOnly(2026, 11, 9);

        var serdivan = _clinic.Ayse.WorkingDaysAt(_clinic.Serdivan, monday, 14);
        var kadikoy = _clinic.Ayse.WorkingDaysAt(_clinic.Kadikoy, monday, 14);

        Assert.Equal([new DateOnly(2026, 11, 10), new DateOnly(2026, 11, 17)], serdivan);
        Assert.Equal(
            [new DateOnly(2026, 11, 10), new DateOnly(2026, 11, 12), new DateOnly(2026, 11, 17), new DateOnly(2026, 11, 19)],
            kadikoy);
    }

    [Fact]
    public void Calisilan_gunlere_baslangic_gunu_dahildir_son_gun_dahil_degildir()
    {
        var tuesday = new DateOnly(2026, 11, 10);

        Assert.Equal([tuesday], _clinic.Ayse.WorkingDaysAt(_clinic.Serdivan, tuesday, 7));
        Assert.Empty(_clinic.Ayse.WorkingDaysAt(_clinic.Serdivan, tuesday.AddDays(1), 6));
    }
}

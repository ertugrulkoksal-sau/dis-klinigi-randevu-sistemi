using DisKlinigi.Core.Booking.Rules;
using static DisKlinigi.Tests.Booking.BookingClinic;

namespace DisKlinigi.Tests.Booking;

/// <summary>Kural 1 ve 2: geçmişe randevu yok, hekim hizmeti veriyor, şube bölümü barındırıyor.</summary>
public class BasicRuleTests
{
    private readonly BookingClinic _clinic = new();

    // ---- Kural 1: geçmişe randevu yok ----

    [Fact]
    public void Gelecekteki_saate_randevu_kurali_gecer()
    {
        var request = _clinic.Request(_clinic.Ali, _clinic.Ayse, _clinic.Examination, _clinic.Serdivan, At(10));

        Assert.False(new FutureStartRule().Check(request, _clinic.Context()).IsViolated);
    }

    [Fact]
    public void Tam_su_ana_randevu_alinamaz()
    {
        _clinic.SetNow(At(10));
        var request = _clinic.Request(_clinic.Ali, _clinic.Ayse, _clinic.Examination, _clinic.Serdivan, At(10));

        var result = new FutureStartRule().Check(request, _clinic.Context());

        Assert.True(result.IsViolated);
        Assert.Equal("Geçmiş bir saate randevu alınamaz.", result.Message);
    }

    [Fact]
    public void Bir_dakika_once_baslayan_saate_randevu_alinamaz()
    {
        _clinic.SetNow(At(10, 1));
        var request = _clinic.Request(_clinic.Ali, _clinic.Ayse, _clinic.Examination, _clinic.Serdivan, At(10));

        Assert.True(new FutureStartRule().Check(request, _clinic.Context()).IsViolated);
    }

    [Fact]
    public void Bir_dakika_sonraya_randevu_alinabilir()
    {
        _clinic.SetNow(At(9, 59));
        var request = _clinic.Request(_clinic.Ali, _clinic.Ayse, _clinic.Examination, _clinic.Serdivan, At(10));

        Assert.False(new FutureStartRule().Check(request, _clinic.Context()).IsViolated);
    }

    // ---- Kural 2a: hekim bu hizmeti veriyor ----

    [Fact]
    public void Hekimin_verdigi_hizmet_kurali_gecer()
    {
        var request = _clinic.Request(_clinic.Ali, _clinic.Ayse, _clinic.CanalTreatment, _clinic.Serdivan, At(10));

        Assert.False(new DentistOffersServiceRule().Check(request, _clinic.Context()).IsViolated);
    }

    [Fact]
    public void Hekimin_vermedigi_hizmete_randevu_alinamaz()
    {
        // Mehmet Bey genel diş hekimi; kanal tedavisi yapmıyor.
        var request = _clinic.Request(_clinic.Ali, _clinic.Mehmet, _clinic.CanalTreatment, _clinic.Serdivan, At(10));

        var result = new DentistOffersServiceRule().Check(request, _clinic.Context());

        Assert.True(result.IsViolated);
        Assert.Contains("Kanal tedavisi", result.Message);
        Assert.Contains("Dt. Mehmet Demir", result.Message);
    }

    // ---- Kural 2b: şube bu bölümü barındırıyor ----

    [Fact]
    public void Subede_bulunan_bolumun_hizmeti_kurali_gecer()
    {
        var request = _clinic.Request(_clinic.Ali, _clinic.Ayse, _clinic.Cleaning, _clinic.Serdivan, At(10));

        Assert.False(new BranchHostsDepartmentRule().Check(request, _clinic.Context()).IsViolated);
    }

    [Fact]
    public void Subede_bulunmayan_bolumun_hizmetine_randevu_alinamaz()
    {
        // Ayşe Hanım diş taşı temizliği yapıyor ama Kadıköy'de Periodontoloji bölümü yok.
        var request = _clinic.Request(_clinic.Ali, _clinic.Ayse, _clinic.Cleaning, _clinic.Kadikoy, At(15));

        var result = new BranchHostsDepartmentRule().Check(request, _clinic.Context());

        Assert.True(result.IsViolated);
        Assert.Equal("Periodontoloji bölümü bu şubede bulunmuyor (Kadıköy Şubesi).", result.Message);
    }
}

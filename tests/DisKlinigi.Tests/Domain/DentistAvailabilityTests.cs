using DisKlinigi.Core.Domain;
using DisKlinigi.Core.Domain.Users;

namespace DisKlinigi.Tests.Domain;

public class DentistAvailabilityTests
{
    // 10 Kasım 2026 Salı, 11 Kasım 2026 Çarşamba.
    private static readonly DateOnly Tuesday = new(2026, 11, 10);
    private static readonly DateOnly Wednesday = new(2026, 11, 11);

    private readonly Branch _serdivan;
    private readonly Branch _kadikoy;
    private readonly Dentist _dentist;

    public DentistAvailabilityTests()
    {
        _serdivan = new Branch("Serdivan Şubesi", "Sakarya", "", chairCount: 4);
        _serdivan.SetWorkingHours(DayOfWeek.Tuesday, TimeOfDayRange.Of(9, 0, 18, 0));
        _serdivan.SetWorkingHours(DayOfWeek.Saturday, TimeOfDayRange.Of(9, 0, 14, 0));

        _kadikoy = new Branch("Kadıköy Şubesi", "İstanbul", "", chairCount: 5);
        _kadikoy.SetWorkingHours(DayOfWeek.Tuesday, TimeOfDayRange.Of(9, 0, 19, 0));

        _dentist = new Dentist("Dt. Ayşe Kaya", "ayse@ornek.com", "ozet", new Department("Endodonti"));
        _dentist.AssignToBranch(_serdivan);
        _dentist.AssignToBranch(_kadikoy);
    }

    // ---- Aralık ekleme kuralları ----

    [Fact]
    public void Calisma_saatine_sigan_aralik_eklenir()
    {
        var window = _dentist.AddAvailability(_serdivan, DayOfWeek.Tuesday, TimeOfDayRange.Of(9, 0, 13, 0));

        Assert.Single(_dentist.Availability);
        Assert.Same(_serdivan, window.Branch);
        Assert.Equal(DayOfWeek.Tuesday, window.Day);
    }

    [Fact]
    public void Calisma_saatinin_tamamini_kaplayan_aralik_eklenir()
    {
        // Sınırlar dahil: 09:00–18:00 aralığı 09:00–18:00 çalışma saatine sığar.
        _dentist.AddAvailability(_serdivan, DayOfWeek.Tuesday, TimeOfDayRange.Of(9, 0, 18, 0));

        Assert.Single(_dentist.Availability);
    }

    [Theory]
    [InlineData(8, 30, 12, 0)]   // açılıştan önce başlıyor
    [InlineData(14, 0, 18, 30)]  // kapanıştan sonra bitiyor
    [InlineData(7, 0, 20, 0)]    // iki uçtan da taşıyor
    public void Calisma_saatine_sigmayan_aralik_eklenmez(int sh, int sm, int eh, int em)
    {
        var ex = Assert.Throws<DomainException>(() =>
            _dentist.AddAvailability(_serdivan, DayOfWeek.Tuesday, TimeOfDayRange.Of(sh, sm, eh, em)));

        Assert.Contains("sığmıyor", ex.Message);
        Assert.Empty(_dentist.Availability);
    }

    [Fact]
    public void Cumartesi_kisa_calisma_saatine_gore_kontrol_edilir()
    {
        var ex = Assert.Throws<DomainException>(() =>
            _dentist.AddAvailability(_serdivan, DayOfWeek.Saturday, TimeOfDayRange.Of(10, 0, 16, 0)));

        Assert.Contains("Cumartesi", ex.Message);
        Assert.Contains("09:00–14:00", ex.Message);
    }

    [Fact]
    public void Subenin_kapali_oldugu_gune_aralik_eklenmez()
    {
        var ex = Assert.Throws<DomainException>(() =>
            _dentist.AddAvailability(_serdivan, DayOfWeek.Sunday, TimeOfDayRange.Of(10, 0, 12, 0)));

        Assert.Contains("kapalı", ex.Message);
    }

    [Fact]
    public void Calismadigi_subeye_aralik_eklenmez()
    {
        var adapazari = new Branch("Adapazarı Merkez Şubesi", "Sakarya", "", chairCount: 3);
        adapazari.SetWorkingHours(DayOfWeek.Tuesday, TimeOfDayRange.Of(8, 30, 17, 30));

        var ex = Assert.Throws<DomainException>(() =>
            _dentist.AddAvailability(adapazari, DayOfWeek.Tuesday, TimeOfDayRange.Of(9, 0, 12, 0)));

        Assert.Contains("çalışmıyor", ex.Message);
    }

    [Fact]
    public void Ayni_gun_ayni_subede_kesisen_aralik_eklenmez()
    {
        _dentist.AddAvailability(_serdivan, DayOfWeek.Tuesday, TimeOfDayRange.Of(9, 0, 13, 0));

        Assert.Throws<DomainException>(() =>
            _dentist.AddAvailability(_serdivan, DayOfWeek.Tuesday, TimeOfDayRange.Of(12, 0, 15, 0)));
    }

    [Fact]
    public void Ayni_gun_baska_subede_kesisen_aralik_eklenmez()
    {
        // Hekim aynı saatte iki şubede birden olamaz.
        _dentist.AddAvailability(_serdivan, DayOfWeek.Tuesday, TimeOfDayRange.Of(9, 0, 13, 0));

        var ex = Assert.Throws<DomainException>(() =>
            _dentist.AddAvailability(_kadikoy, DayOfWeek.Tuesday, TimeOfDayRange.Of(12, 30, 18, 0)));

        Assert.Contains("çakışıyor", ex.Message);
        Assert.Single(_dentist.Availability);
    }

    [Fact]
    public void Uc_uca_aralik_baska_subede_eklenebilir()
    {
        // 13:00'te biten aralıktan sonra 13:00'te başlayan aralık kesişmiyor (yarı açık aralık).
        _dentist.AddAvailability(_serdivan, DayOfWeek.Tuesday, TimeOfDayRange.Of(9, 0, 13, 0));
        _dentist.AddAvailability(_kadikoy, DayOfWeek.Tuesday, TimeOfDayRange.Of(13, 0, 18, 0));

        Assert.Equal(2, _dentist.Availability.Count);
    }

    [Fact]
    public void Farkli_gunlerdeki_ayni_saatler_birbirini_engellemez()
    {
        _serdivan.SetWorkingHours(DayOfWeek.Wednesday, TimeOfDayRange.Of(9, 0, 18, 0));

        _dentist.AddAvailability(_serdivan, DayOfWeek.Tuesday, TimeOfDayRange.Of(9, 0, 13, 0));
        _dentist.AddAvailability(_serdivan, DayOfWeek.Wednesday, TimeOfDayRange.Of(9, 0, 13, 0));

        Assert.Equal(2, _dentist.Availability.Count);
    }

    // ---- Takvimdeki bir aralığa müsait mi? ----

    [Theory]
    [InlineData(9, 0, 10, 0, true)]     // aralığın başına yaslı
    [InlineData(12, 0, 13, 0, true)]    // aralığın sonuna yaslı: 13:00'te biten randevu sığar
    [InlineData(12, 30, 13, 30, false)] // aralığın sonundan taşıyor
    [InlineData(8, 30, 9, 30, false)]   // aralığın başından taşıyor
    [InlineData(14, 0, 15, 0, false)]   // şube açık ama hekim müsait değil
    public void Musaitlik_sinirlari_dahil_kontrol_edilir(int sh, int sm, int eh, int em, bool expected)
    {
        _dentist.AddAvailability(_serdivan, DayOfWeek.Tuesday, TimeOfDayRange.Of(9, 0, 13, 0));

        var period = TimeOfDayRange.Of(sh, sm, eh, em).On(Tuesday);

        Assert.Equal(expected, _dentist.IsAvailable(_serdivan, period));
    }

    [Fact]
    public void Musaitlik_sadece_tanimlandigi_subede_gecerlidir()
    {
        _dentist.AddAvailability(_serdivan, DayOfWeek.Tuesday, TimeOfDayRange.Of(9, 0, 13, 0));

        var period = TimeOfDayRange.Of(10, 0, 11, 0).On(Tuesday);

        Assert.True(_dentist.IsAvailable(_serdivan, period));
        Assert.False(_dentist.IsAvailable(_kadikoy, period));
    }

    [Fact]
    public void Musaitlik_sadece_tanimlandigi_gunde_gecerlidir()
    {
        _dentist.AddAvailability(_serdivan, DayOfWeek.Tuesday, TimeOfDayRange.Of(9, 0, 13, 0));

        Assert.False(_dentist.IsAvailable(_serdivan, TimeOfDayRange.Of(10, 0, 11, 0).On(Wednesday)));
    }

    [Fact]
    public void Uc_uca_iki_araliga_bolunen_randevu_musait_sayilmaz()
    {
        _dentist.AddAvailability(_serdivan, DayOfWeek.Tuesday, TimeOfDayRange.Of(9, 0, 13, 0));
        _dentist.AddAvailability(_serdivan, DayOfWeek.Tuesday, TimeOfDayRange.Of(13, 0, 17, 0));

        Assert.False(_dentist.IsAvailable(_serdivan, TimeOfDayRange.Of(12, 30, 13, 30).On(Tuesday)));
    }

    [Fact]
    public void Gunun_araliklari_saate_gore_sirali_doner()
    {
        _dentist.AddAvailability(_serdivan, DayOfWeek.Tuesday, TimeOfDayRange.Of(14, 0, 17, 0));
        _dentist.AddAvailability(_serdivan, DayOfWeek.Tuesday, TimeOfDayRange.Of(9, 0, 12, 0));

        var windows = _dentist.AvailabilityAt(_serdivan, DayOfWeek.Tuesday);

        Assert.Equal([new TimeOnly(9, 0), new TimeOnly(14, 0)], windows.Select(w => w.Hours.Start));
    }

    // ---- Şube ve hizmet atamaları ----

    [Fact]
    public void Musaitligi_olan_subeden_hekim_cikarilamaz()
    {
        _dentist.AddAvailability(_serdivan, DayOfWeek.Tuesday, TimeOfDayRange.Of(9, 0, 13, 0));

        Assert.Throws<DomainException>(() => _dentist.RemoveFromBranch(_serdivan));
        Assert.True(_dentist.WorksAt(_serdivan));
    }

    [Fact]
    public void Aralik_silindikten_sonra_hekim_subeden_cikarilabilir()
    {
        var window = _dentist.AddAvailability(_serdivan, DayOfWeek.Tuesday, TimeOfDayRange.Of(9, 0, 13, 0));

        _dentist.RemoveAvailability(window);
        _dentist.RemoveFromBranch(_serdivan);

        Assert.False(_dentist.WorksAt(_serdivan));
    }

    [Fact]
    public void Ayni_sube_ve_hizmet_iki_kez_eklenmez()
    {
        var rootCanal = new Service("Kanal tedavisi", _dentist.Specialty, TimeSpan.FromMinutes(60), 5000m, true);

        _dentist.AssignToBranch(_serdivan);
        _dentist.OfferService(rootCanal);
        _dentist.OfferService(rootCanal);

        Assert.Equal(2, _dentist.Branches.Count);
        Assert.Single(_dentist.Services);
        Assert.True(_dentist.Offers(rootCanal));

        _dentist.StopOfferingService(rootCanal);
        Assert.False(_dentist.Offers(rootCanal));
    }
}

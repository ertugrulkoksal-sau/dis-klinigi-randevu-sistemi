using DisKlinigi.Core.Booking;
using DisKlinigi.Core.Booking.Rules;
using static DisKlinigi.Tests.Booking.BookingClinic;

namespace DisKlinigi.Tests.Booking;

/// <summary>Kural 3 ve 4: şube çalışma saati ve hekim müsaitliği. Sınırlar yarı açık aralığa göre.</summary>
public class ScheduleRuleTests
{
    private readonly BookingClinic _clinic = new();

    /// <summary>Mehmet Bey'den Serdivan'da 20 dakikalık muayene.</summary>
    private RuleResult WorkingHours(DateTime start) =>
        new WorkingHoursRule().Check(
            _clinic.Request(_clinic.Ali, _clinic.Mehmet, _clinic.Examination, _clinic.Serdivan, start),
            _clinic.Context());

    // ---- Kural 3: şube çalışma saati (Serdivan Salı 09:00–18:00) ----

    [Fact]
    public void Calisma_saati_icindeki_randevu_kurali_gecer()
    {
        Assert.False(WorkingHours(At(12)).IsViolated);
    }

    [Fact]
    public void Acilis_aninda_baslayan_randevu_kabul_edilir()
    {
        Assert.False(WorkingHours(At(9)).IsViolated);
    }

    [Fact]
    public void Kapanis_aninda_biten_randevu_kabul_edilir()
    {
        // 17:40 + 20 dk = 18:00; bitiş anı aralığa dahil değil, kapanışa tam denk geliyor.
        Assert.False(WorkingHours(At(17, 40)).IsViolated);
    }

    [Fact]
    public void Kapanistan_bir_dakika_tasan_randevu_reddedilir()
    {
        var result = WorkingHours(At(17, 41));

        Assert.True(result.IsViolated);
        Assert.Contains("17:41–18:01", result.Message);
        Assert.Contains("09:00–18:00", result.Message);
    }

    [Fact]
    public void Acilistan_once_baslayan_randevu_reddedilir()
    {
        Assert.True(WorkingHours(At(8, 50)).IsViolated);
    }

    [Fact]
    public void Subenin_kapali_oldugu_gune_randevu_alinamaz()
    {
        // 11 Kasım Çarşamba: Serdivan'ın Çarşamba kaydı yok.
        var wednesday = new DateOnly(2026, 11, 11);
        var request = _clinic.Request(_clinic.Ali, _clinic.Mehmet, _clinic.Examination, _clinic.Serdivan,
            wednesday.ToDateTime(new TimeOnly(10, 0)));

        var result = new WorkingHoursRule().Check(request, new BookingContext(_clinic.Now, wednesday, []));

        Assert.True(result.IsViolated);
        Assert.Equal("Serdivan Şubesi Çarşamba günü kapalı.", result.Message);
    }

    // ---- Kural 4: hekim müsaitliği (Ayşe Salı: Serdivan 09:00–13:00, Kadıköy 14:00–18:00) ----

    /// <summary>Ayşe Hanım'dan 60 dakikalık kanal tedavisi; şube verilmezse Serdivan.</summary>
    private RuleResult Availability(DateTime start, Core.Domain.Branch? branch = null) =>
        new DentistAvailabilityRule().Check(
            _clinic.Request(_clinic.Ali, _clinic.Ayse, _clinic.CanalTreatment, branch ?? _clinic.Serdivan, start),
            _clinic.Context());

    [Fact]
    public void Musaitlik_araligindaki_randevu_kurali_gecer()
    {
        Assert.False(Availability(At(10)).IsViolated);
    }

    [Fact]
    public void Musaitlik_araliginin_sonuna_tam_biten_randevu_kabul_edilir()
    {
        Assert.False(Availability(At(12)).IsViolated);
    }

    [Fact]
    public void Musaitlik_araligindan_tasan_randevu_reddedilir()
    {
        // 12:30–13:30: şube açık ama Ayşe Hanım'ın Serdivan aralığı 13:00'te bitiyor.
        var result = Availability(At(12, 30));

        Assert.True(result.IsViolated);
        Assert.Contains("09:00–13:00", result.Message);
    }

    [Fact]
    public void Baska_subedeki_musaitlik_bu_sube_icin_sayilmaz()
    {
        // 15:00'te Ayşe Hanım müsait ama Kadıköy'de; Serdivan'a randevu verilmiyor.
        var result = Availability(At(15));

        Assert.True(result.IsViolated);
        Assert.False(Availability(At(15), _clinic.Kadikoy).IsViolated);
    }

    [Fact]
    public void Hekimin_o_gun_o_subede_hic_araligi_yoksa_mesaj_bunu_soyler()
    {
        // Mehmet Bey Kadıköy'de çalışmıyor; şube açık olsa da randevu verilmiyor.
        var request = _clinic.Request(_clinic.Ali, _clinic.Mehmet, _clinic.Examination, _clinic.Kadikoy, At(10));

        var result = new DentistAvailabilityRule().Check(request, _clinic.Context());

        Assert.True(result.IsViolated);
        Assert.Equal("Dt. Mehmet Demir Salı günleri bu şubede çalışmıyor (Kadıköy Şubesi).", result.Message);
    }
}

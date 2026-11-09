using DisKlinigi.Core.Booking;
using DisKlinigi.Core.Booking.Rules;
using DisKlinigi.Core.Domain;
using DisKlinigi.Core.Domain.Appointments;
using static DisKlinigi.Tests.Booking.BookingClinic;

namespace DisKlinigi.Tests.Booking;

/// <summary>
/// Kural 7: ünit kapasitesi. Serdivan 2 ünitli; istek her testte Ali'nin Ayşe Hanım'dan
/// 09:00–10:00 kanal tedavisi. Var olan randevular başka hekim ve hastalarda, böylece
/// sadece ünit kuralı devreye giriyor.
/// </summary>
public class ChairCapacityRuleTests
{
    private readonly BookingClinic _clinic = new();

    private RuleResult Check(params Appointment[] existing) =>
        new ChairCapacityRule().Check(
            _clinic.Request(_clinic.Ali, _clinic.Ayse, _clinic.CanalTreatment, _clinic.Serdivan, At(9)),
            _clinic.Context(existing));

    [Fact]
    public void Bos_subede_kural_gecer()
    {
        Assert.False(Check().IsViolated);
    }

    [Fact]
    public void Bir_unit_doluyken_ikinci_unite_randevu_verilir()
    {
        var one = Existing(_clinic.Zeynep, _clinic.Mehmet, _clinic.Cleaning, _clinic.Serdivan, At(9));

        Assert.False(Check(one).IsViolated);
    }

    [Fact]
    public void Iki_unit_de_ayni_anda_doluysa_reddedilir()
    {
        // 09:00–09:30 ve 09:15–09:45: 09:15–09:30 arasında iki ünit de dolu.
        var first = Existing(_clinic.Zeynep, _clinic.Mehmet, _clinic.Cleaning, _clinic.Serdivan, At(9));
        var second = Existing(_clinic.Can, _clinic.Burak, _clinic.Cleaning, _clinic.Serdivan, At(9, 15));

        var result = Check(first, second);

        Assert.True(result.IsViolated);
        Assert.Equal("Şubedeki 2 ünitin hepsi bu saat aralığının bir kısmında dolu (Serdivan Şubesi).", result.Message);
    }

    [Fact]
    public void Ardisik_iki_randevu_ayni_anda_tek_unit_kullanir()
    {
        // tasarim.md bölüm 3'teki örnek: 09:00–09:30 ve 09:30–10:00 randevuları isteğin ikisiyle
        // de kesişiyor ama aynı anda hep tek randevu yürüyor; bir ünit hep boş, istek kabul.
        // "Kesişen randevu sayısı < ünit sayısı" diye bakılsaydı bu istek yanlışlıkla reddedilirdi.
        var first = Existing(_clinic.Zeynep, _clinic.Mehmet, _clinic.Cleaning, _clinic.Serdivan, At(9));
        var second = Existing(_clinic.Can, _clinic.Burak, _clinic.Cleaning, _clinic.Serdivan, At(9, 30));

        Assert.False(Check(first, second).IsViolated);
    }

    [Fact]
    public void Istekten_once_yasanan_yogunluk_sayilmaz()
    {
        // İstek 10:00–11:00. Var olan iki randevu (09:30–10:00 ve 09:40–10:10) 09:40–10:00
        // arasında iki üniti de dolduruyor ama bu an istekten önce. İstek süresince sadece
        // 10:00–10:10 kısmı devam ediyor, yani bir ünit boş.
        var first = Existing(_clinic.Zeynep, _clinic.Mehmet, _clinic.Cleaning, _clinic.Serdivan, At(9, 30));
        var second = Existing(_clinic.Can, _clinic.Burak, _clinic.Cleaning, _clinic.Serdivan, At(9, 40));
        var request = _clinic.Request(_clinic.Ali, _clinic.Ayse, _clinic.CanalTreatment, _clinic.Serdivan, At(10));

        var result = new ChairCapacityRule().Check(request, _clinic.Context(first, second));

        Assert.False(result.IsViolated);
    }

    [Fact]
    public void Baska_subedeki_randevular_bu_subenin_unitini_doldurmaz()
    {
        var first = Existing(_clinic.Zeynep, _clinic.Mehmet, _clinic.Examination, _clinic.Kadikoy, At(9));
        var second = Existing(_clinic.Can, _clinic.Burak, _clinic.Examination, _clinic.Kadikoy, At(9));

        Assert.False(Check(first, second).IsViolated);
    }

    [Fact]
    public void Iptal_edilen_randevu_uniti_bosaltir()
    {
        var first = Existing(_clinic.Zeynep, _clinic.Mehmet, _clinic.Cleaning, _clinic.Serdivan, At(9));
        var second = Existing(_clinic.Can, _clinic.Burak, _clinic.Cleaning, _clinic.Serdivan, At(9));
        second.Cancel(_clinic.Admin, _clinic.Now);

        Assert.False(Check(first, second).IsViolated);
    }

    [Fact]
    public void Tek_unitli_subede_herhangi_bir_kesisme_yeterli()
    {
        _clinic.Serdivan.ChangeChairCount(1);
        var last = Existing(_clinic.Zeynep, _clinic.Mehmet, _clinic.Examination, _clinic.Serdivan, At(9, 50));

        Assert.True(Check(last).IsViolated);
    }

    // ---- Tarama: en yüksek eş zamanlı randevu sayısı ----

    private static TimeRange Range(int fromHour, int fromMinute, int toHour, int toMinute) =>
        new(At(fromHour, fromMinute), At(toHour, toMinute));

    private static readonly TimeRange NineToTen = Range(9, 0, 10, 0);

    [Fact]
    public void Tarama_randevu_yoksa_sifir()
    {
        Assert.Equal(0, ChairCapacityRule.PeakOccupancy([], NineToTen));
    }

    [Fact]
    public void Tarama_uc_uca_randevulari_ayni_anda_saymaz()
    {
        TimeRange[] periods = [Range(9, 0, 9, 20), Range(9, 20, 9, 40), Range(9, 40, 10, 0)];

        Assert.Equal(1, ChairCapacityRule.PeakOccupancy(periods, NineToTen));
    }

    [Fact]
    public void Tarama_ayni_anda_baslayan_randevulari_birlikte_sayar()
    {
        TimeRange[] periods = [Range(9, 0, 9, 30), Range(9, 0, 9, 15), Range(9, 0, 10, 0)];

        Assert.Equal(3, ChairCapacityRule.PeakOccupancy(periods, NineToTen));
    }

    [Fact]
    public void Tarama_merdiven_seklindeki_kesismelerde_en_yogun_ani_bulur()
    {
        // 09:00–09:40, 09:20–10:00, 09:30–09:50: 09:30–09:40 arasında üçü birlikte.
        // Herhangi iki randevunun kesişip kesişmediğine bakmak bunu göremezdi.
        TimeRange[] periods = [Range(9, 0, 9, 40), Range(9, 20, 10, 0), Range(9, 30, 9, 50)];

        Assert.Equal(3, ChairCapacityRule.PeakOccupancy(periods, NineToTen));
    }

    [Fact]
    public void Tarama_kesisen_ama_ayni_anda_olmayan_randevulari_ayirir()
    {
        // İkisi de 09:00–10:00 ile kesişiyor, birbirleriyle kesişmiyor.
        TimeRange[] periods = [Range(8, 30, 9, 10), Range(9, 50, 10, 30)];

        Assert.Equal(1, ChairCapacityRule.PeakOccupancy(periods, NineToTen));
    }

    [Fact]
    public void Tarama_sadece_verilen_aralik_icine_bakar()
    {
        // 08:00–09:00 arasında üç randevu birlikte yürüyor ama 09:00'dan önce bitiyorlar;
        // 09:00–10:00 içinde sadece biri (08:30–09:30) devam ediyor.
        TimeRange[] periods = [Range(8, 0, 9, 0), Range(8, 15, 9, 0), Range(8, 30, 9, 30)];

        Assert.Equal(1, ChairCapacityRule.PeakOccupancy(periods, NineToTen));
    }
}

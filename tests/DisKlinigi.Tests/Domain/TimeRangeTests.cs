using DisKlinigi.Core.Domain;

namespace DisKlinigi.Tests.Domain;

public class TimeRangeTests
{
    private static readonly DateOnly Day = new(2026, 11, 10);

    // Testlerin okunması kolay olsun diye "10:30" gibi metinden aralık üretiyorum.
    private static TimeRange R(string start, string end) =>
        new(Day.ToDateTime(TimeOnly.Parse(start)), Day.ToDateTime(TimeOnly.Parse(end)));

    [Theory]
    [InlineData("10:00", "10:00")]
    [InlineData("10:30", "10:00")]
    public void Bitis_baslangictan_sonra_degilse_aralik_olusturulamaz(string start, string end)
    {
        Assert.Throws<DomainException>(() => R(start, end));
    }

    [Fact]
    public void Sure_bitis_eksi_baslangictir()
    {
        Assert.Equal(TimeSpan.FromMinutes(45), R("09:15", "10:00").Duration);
    }

    [Fact]
    public void Sureden_olusturulan_aralik_dogru_bitis_verir()
    {
        var range = TimeRange.FromDuration(Day.ToDateTime(new TimeOnly(11, 0)), TimeSpan.FromMinutes(60));

        Assert.Equal(R("11:00", "12:00"), range);
    }

    [Theory]
    // Uç uca aralıklar: bitiş anı dahil olmadığı için kesişmiyor (arka arkaya randevu).
    [InlineData("10:00", "10:30", "10:30", "11:00", false)]
    [InlineData("10:30", "11:00", "10:00", "10:30", false)]
    // Tamamen ayrık.
    [InlineData("09:00", "09:30", "11:00", "12:00", false)]
    // Kısmi kesişme, iki yönden.
    [InlineData("10:00", "10:30", "10:15", "10:45", true)]
    [InlineData("10:15", "10:45", "10:00", "10:30", true)]
    // Biri diğerinin içinde.
    [InlineData("09:00", "12:00", "10:00", "10:30", true)]
    [InlineData("10:00", "10:30", "09:00", "12:00", true)]
    // Aynı aralık.
    [InlineData("10:00", "10:30", "10:00", "10:30", true)]
    // Aynı anda başlayan, farklı uzunlukta.
    [InlineData("10:00", "10:15", "10:00", "11:00", true)]
    // Bir dakikalık kesişme bile çakışmadır.
    [InlineData("10:00", "10:31", "10:30", "11:00", true)]
    public void Kesisme_yari_acik_araliga_gore_hesaplanir(string s1, string e1, string s2, string e2, bool expected)
    {
        Assert.Equal(expected, R(s1, e1).Overlaps(R(s2, e2)));
    }

    [Fact]
    public void Farkli_gunlerdeki_ayni_saatler_kesismez()
    {
        var tuesday = R("10:00", "11:00");
        var wednesday = new TimeRange(tuesday.Start.AddDays(1), tuesday.End.AddDays(1));

        Assert.False(tuesday.Overlaps(wednesday));
    }

    [Theory]
    [InlineData("09:00", "13:00", true)]   // sınırlar dahil: aralığın tamamı
    [InlineData("09:00", "09:20", true)]   // başa yaslı
    [InlineData("12:00", "13:00", true)]   // sona yaslı
    [InlineData("08:45", "09:15", false)]  // baştan taşıyor
    [InlineData("12:30", "13:30", false)]  // sondan taşıyor
    [InlineData("08:00", "14:00", false)]  // dışarıdan sarıyor
    public void Icerme_sinirlari_dahil_eder(string start, string end, bool expected)
    {
        var workingHours = R("09:00", "13:00");

        Assert.Equal(expected, workingHours.Contains(R(start, end)));
    }

    [Fact]
    public void Bitis_ani_araliga_dahil_degildir()
    {
        var range = R("10:00", "10:30");

        Assert.True(range.Contains(Day.ToDateTime(new TimeOnly(10, 0))));
        Assert.True(range.Contains(Day.ToDateTime(new TimeOnly(10, 29))));
        Assert.False(range.Contains(Day.ToDateTime(new TimeOnly(10, 30))));
    }

    [Fact]
    public void Ayni_baslangic_ve_bitise_sahip_araliklar_esittir()
    {
        Assert.Equal(R("10:00", "10:30"), R("10:00", "10:30"));
        Assert.NotEqual(R("10:00", "10:30"), R("10:00", "10:45"));
    }
}

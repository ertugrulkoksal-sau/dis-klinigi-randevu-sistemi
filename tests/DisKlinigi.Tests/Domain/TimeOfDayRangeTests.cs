using DisKlinigi.Core.Domain;

namespace DisKlinigi.Tests.Domain;

public class TimeOfDayRangeTests
{
    private static readonly DateOnly Tuesday = new(2026, 11, 10);

    [Fact]
    public void Bitis_baslangictan_sonra_degilse_olusturulamaz()
    {
        Assert.Throws<DomainException>(() => TimeOfDayRange.Of(13, 0, 9, 0));
        Assert.Throws<DomainException>(() => TimeOfDayRange.Of(9, 0, 9, 0));
    }

    [Fact]
    public void Verilen_gune_yerlestirilebilir()
    {
        var range = TimeOfDayRange.Of(9, 0, 13, 0).On(Tuesday);

        Assert.Equal(new DateTime(2026, 11, 10, 9, 0, 0), range.Start);
        Assert.Equal(new DateTime(2026, 11, 10, 13, 0, 0), range.End);
    }

    [Theory]
    [InlineData(9, 0, 10, 0, 10, 0, 11, 0, false)]   // uç uca
    [InlineData(9, 0, 10, 0, 9, 30, 11, 0, true)]    // kısmi
    [InlineData(9, 0, 18, 0, 12, 0, 13, 0, true)]    // içeride
    [InlineData(13, 0, 18, 0, 9, 0, 13, 0, false)]   // sabah–öğleden sonra, uç uca
    public void Kesisme_yari_acik_araliga_gore_hesaplanir(
        int s1h, int s1m, int e1h, int e1m, int s2h, int s2m, int e2h, int e2m, bool expected)
    {
        var first = TimeOfDayRange.Of(s1h, s1m, e1h, e1m);
        var second = TimeOfDayRange.Of(s2h, s2m, e2h, e2m);

        Assert.Equal(expected, first.Overlaps(second));
        Assert.Equal(expected, second.Overlaps(first));
    }

    [Fact]
    public void Takvimdeki_aralik_kalibin_icindeyse_kapsanir()
    {
        var hours = TimeOfDayRange.Of(9, 0, 13, 0);

        Assert.True(hours.Covers(TimeOfDayRange.Of(12, 0, 13, 0).On(Tuesday)));
        Assert.False(hours.Covers(TimeOfDayRange.Of(12, 30, 13, 30).On(Tuesday)));
    }

    [Fact]
    public void Ertesi_gune_tasan_aralik_kapsanmaz()
    {
        var allDay = TimeOfDayRange.Of(0, 0, 23, 59);
        var overnight = new TimeRange(Tuesday.ToDateTime(new TimeOnly(23, 0)), Tuesday.AddDays(1).ToDateTime(new TimeOnly(1, 0)));

        Assert.False(allDay.Covers(overnight));
    }
}

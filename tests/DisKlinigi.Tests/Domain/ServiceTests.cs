using DisKlinigi.Core.Domain;

namespace DisKlinigi.Tests.Domain;

public class ServiceTests
{
    private static readonly Department Endodontics = new("Endodonti");

    private static Service CreateRootCanal() =>
        new("Kanal tedavisi", Endodontics, TimeSpan.FromMinutes(60), 5000m, requiresApproval: true);

    [Fact]
    public void Gecerli_bilgilerle_olusturulur()
    {
        var service = CreateRootCanal();

        Assert.Equal("Kanal tedavisi", service.Name);
        Assert.Same(Endodontics, service.Department);
        Assert.Equal(TimeSpan.FromMinutes(60), service.Duration);
        Assert.Equal(5000m, service.Price);
        Assert.True(service.RequiresApproval);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-15)]
    [InlineData(3)]    // 5'ten kısa
    [InlineData(22)]   // 5'in katı değil
    [InlineData(245)]  // üst sınırı aşıyor
    public void Gecersiz_sure_kabul_edilmez(int minutes)
    {
        var service = CreateRootCanal();

        Assert.Throws<DomainException>(() => service.ChangeDuration(TimeSpan.FromMinutes(minutes)));
        Assert.Equal(TimeSpan.FromMinutes(60), service.Duration); // hata sonrası eski değer korunuyor
    }

    [Theory]
    [InlineData(5)]
    [InlineData(15)]
    [InlineData(240)]
    public void Sinirdaki_sureler_kabul_edilir(int minutes)
    {
        var service = CreateRootCanal();

        service.ChangeDuration(TimeSpan.FromMinutes(minutes));

        Assert.Equal(TimeSpan.FromMinutes(minutes), service.Duration);
    }

    [Fact]
    public void Ucret_negatif_olamaz()
    {
        var service = CreateRootCanal();

        Assert.Throws<DomainException>(() => service.ChangePrice(-1m));
    }

    [Fact]
    public void Ucretsiz_hizmet_tanimlanabilir()
    {
        var service = CreateRootCanal();

        service.ChangePrice(0m);

        Assert.Equal(0m, service.Price);
    }

    [Fact]
    public void Bolumsuz_hizmet_olusturulamaz()
    {
        Assert.Throws<DomainException>(() =>
            new Service("Muayene", null!, TimeSpan.FromMinutes(20), 750m, requiresApproval: false));
    }
}

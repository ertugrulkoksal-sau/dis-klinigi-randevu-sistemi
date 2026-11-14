using DisKlinigi.Core.Administration;

namespace DisKlinigi.Tests.Administration;

/// <summary>
/// Bölüm ve hizmet kataloğu (ekran 5b): sadece zincir yöneticisi değiştirir, aynı ad ikinci kez kullanılmaz,
/// alan sınıfının kuralı tutmazsa hizmet hiç değişmez, ücret değişikliği eski randevuya yansımaz (K9).
/// </summary>
public class CatalogServiceTests
{
    private readonly AdminClinic _a = new();

    private static ServiceDraft Canal(int minutes = 60, decimal price = 5000m) =>
        new("Kanal tedavisi", 1, minutes, price, RequiresApproval: true);

    [Fact]
    public async Task Sube_yoneticisi_katalogu_degistiremez()
    {
        var service = _a.CatalogService();

        var department = await service.AddDepartmentAsync(_a.Clinic.SerdivanManager, "Ortodonti");
        var price = await service.SaveServiceAsync(_a.Clinic.SerdivanManager, _a.Clinic.CanalTreatment.Id, Canal(price: 1m));

        Assert.Equal("Bölüm ve hizmet kataloğunu sadece zincir yöneticisi değiştirebilir.", department.Error);
        Assert.False(price.Succeeded);
        Assert.Equal(5000m, _a.Clinic.CanalTreatment.Price);
        Assert.Equal(0, _a.Catalog.SaveCount);
    }

    [Fact]
    public async Task Ayni_adli_bolum_harf_buyuklugu_farkiyla_da_eklenemez()
    {
        var result = await _a.CatalogService().AddDepartmentAsync(_a.Clinic.Admin, "  ENDODONTİ ");

        Assert.Equal("\"ENDODONTİ\" adında bir bölüm var.", result.Error);
        Assert.Equal(2, _a.Catalog.Departments.Count);
    }

    [Fact]
    public async Task Bolum_eklenir_ve_adi_degistirilir()
    {
        var service = _a.CatalogService();

        var added = await service.AddDepartmentAsync(_a.Clinic.Admin, "Ortodonti");
        var renamed = await service.RenameDepartmentAsync(_a.Clinic.Admin, added.Created!.Id, "Ortodonti ve Pedodonti");
        var clash = await service.RenameDepartmentAsync(_a.Clinic.Admin, added.Created.Id, "Endodonti");

        Assert.True(renamed.Succeeded);
        Assert.Equal("Ortodonti ve Pedodonti", added.Created.Name);
        Assert.Equal("\"Endodonti\" adında başka bir bölüm var.", clash.Error);
    }

    [Fact]
    public async Task Ucret_degisince_eski_randevu_kendi_ucretini_korur()
    {
        var appointment = _a.Clinic.Book(_a.Clinic.CanalTreatment);

        var result = await _a.CatalogService().SaveServiceAsync(_a.Clinic.Admin, _a.Clinic.CanalTreatment.Id, Canal(price: 5500m));

        Assert.True(result.Succeeded);
        Assert.Equal(5500m, _a.Clinic.CanalTreatment.Price);
        Assert.Equal(5000m, appointment.PriceAtBooking);
    }

    [Fact]
    public async Task Kurala_takilan_hizmet_hic_degismez_mesaj_alan_sinifindan()
    {
        // Süre 5'in katı değil; aynı formdaki yeni ücret de uygulanmamalı.
        var result = await _a.CatalogService().SaveServiceAsync(_a.Clinic.Admin, _a.Clinic.CanalTreatment.Id, Canal(minutes: 62, price: 9000m));

        Assert.Equal("Hizmet süresi 5 dakikanın katı olmalıdır.", result.Error);
        Assert.Equal(TimeSpan.FromMinutes(60), _a.Clinic.CanalTreatment.Duration);
        Assert.Equal(5000m, _a.Clinic.CanalTreatment.Price);
        Assert.Equal(0, _a.Catalog.SaveCount);
    }

    [Fact]
    public async Task Ayni_bolumde_ayni_adla_ikinci_hizmet_acilamaz_baska_bolumde_acilabilir()
    {
        var service = _a.CatalogService();

        var same = await service.SaveServiceAsync(_a.Clinic.Admin, null, new ServiceDraft("kanal tedavisi", _a.Endodontics.Id, 30, 100m, false));
        var other = await service.SaveServiceAsync(_a.Clinic.Admin, null, new ServiceDraft("Kanal tedavisi", _a.General.Id, 30, 100m, false));

        Assert.Equal("Endodonti bölümünde \"kanal tedavisi\" adında bir hizmet var.", same.Error);
        Assert.True(other.Succeeded);
        Assert.Equal(_a.General, other.Created!.Department);
    }
}

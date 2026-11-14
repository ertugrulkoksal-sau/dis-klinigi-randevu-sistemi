using DisKlinigi.Core.Domain;

namespace DisKlinigi.Tests.Administration;

/// <summary>
/// Şube ayarları (ekran 5a): şube yöneticisinin sınırı (K3), çalışma saati daraltılırken dışarıda kalan müsaitlik
/// kontrolü (K24) ve kaydın "ya hep ya hiç" olması. Dt. Ayşe Kaya'nın Serdivan'da Salı 09:00–13:00 aralığı var.
/// </summary>
public class BranchAdministrationServiceTests
{
    private readonly AdminClinic _a = new();

    private Branch Serdivan => _a.Clinic.Serdivan;
    private Branch Kadikoy => _a.Clinic.Kadikoy;

    [Fact]
    public async Task Sube_yoneticisi_sadece_kendi_subesini_gorur_zincir_yoneticisi_hepsini()
    {
        var service = _a.BranchService();

        Assert.Equal([Serdivan], await service.GetManageableAsync(_a.Clinic.SerdivanManager));
        Assert.Equal(2, (await service.GetManageableAsync(_a.Clinic.Admin)).Count);
        Assert.Empty(await service.GetManageableAsync(_a.Clinic.SerdivanAssistant));
    }

    [Fact]
    public async Task Sube_yoneticisi_baska_subeyi_degistiremez_sube_bulunamadi_olur()
    {
        var settings = AdminClinic.SettingsOf(Kadikoy) with { ChairCount = 1 };

        var result = await _a.BranchService().UpdateAsync(_a.Clinic.SerdivanManager, Kadikoy.Id, settings);

        Assert.True(result.NotFound);
        Assert.Equal(5, Kadikoy.ChairCount);
        Assert.Equal(0, _a.Branches.SaveCount);
    }

    [Fact]
    public async Task Calisma_saati_daraltilirken_disarida_kalan_musaitlik_varsa_kayit_reddedilir()
    {
        // Salı 10:00'da açılırsa Ayşe Hanım'ın 09:00–13:00 aralığının ilk saati dışarıda kalıyor.
        var settings = AdminClinic.WithDay(AdminClinic.SettingsOf(Serdivan), DayOfWeek.Tuesday, new TimeOnly(10, 0), new TimeOnly(18, 0))
            with { ChairCount = 6 };

        var result = await _a.BranchService().UpdateAsync(_a.Clinic.SerdivanManager, Serdivan.Id, settings);

        Assert.False(result.Succeeded);
        Assert.Equal(
            "Dt. Ayşe Kaya için Salı 09:00–13:00 aralığı yeni saatin (10:00–18:00) dışında kalıyor; " +
            "önce hekimin bu aralığı değiştirmesi ya da silmesi gerekiyor.", result.Error);
        // Hiçbir alan değişmedi: ne saat ne de aynı formdaki ünit sayısı.
        Assert.Equal(TimeOfDayRange.Of(9, 0, 18, 0), Serdivan.GetWorkingHours(DayOfWeek.Tuesday));
        Assert.Equal(4, Serdivan.ChairCount);
        Assert.Equal(0, _a.Branches.SaveCount);
    }

    [Fact]
    public async Task Musaitlik_araligi_olan_gun_kapatilamaz()
    {
        var settings = AdminClinic.WithDay(AdminClinic.SettingsOf(Serdivan), DayOfWeek.Tuesday, null, null);

        var result = await _a.BranchService().UpdateAsync(_a.Clinic.Admin, Serdivan.Id, settings);

        Assert.False(result.Succeeded);
        Assert.Contains("kapalı günün dışında kalıyor", result.Error);
        Assert.NotNull(Serdivan.GetWorkingHours(DayOfWeek.Tuesday));
    }

    [Fact]
    public async Task Araligi_tam_kapsayan_daraltma_kabul_edilir()
    {
        // Sınır: aralık 09:00–13:00, şube 09:00–13:00 olunca hâlâ tamamen içeride.
        var settings = AdminClinic.WithDay(AdminClinic.SettingsOf(Serdivan), DayOfWeek.Tuesday, new TimeOnly(9, 0), new TimeOnly(13, 0));

        var result = await _a.BranchService().UpdateAsync(_a.Clinic.SerdivanManager, Serdivan.Id, settings);

        Assert.True(result.Succeeded, result.Error);
        Assert.Equal(TimeOfDayRange.Of(9, 0, 13, 0), Serdivan.GetWorkingHours(DayOfWeek.Tuesday));
    }

    [Fact]
    public async Task Saatler_bolumler_ve_bilgiler_formdaki_haliyle_kaydedilir()
    {
        var settings = AdminClinic.SettingsOf(Serdivan);
        settings = AdminClinic.WithDay(settings, DayOfWeek.Saturday, null, null);
        settings = AdminClinic.WithDay(settings, DayOfWeek.Sunday, new TimeOnly(10, 0), new TimeOnly(14, 0));
        settings = settings with { ChairCount = 6, Address = "Kemalpaşa Mah. No:12", DepartmentIds = [_a.Endodontics.Id] };

        var result = await _a.BranchService().UpdateAsync(_a.Clinic.SerdivanManager, Serdivan.Id, settings);

        Assert.True(result.Succeeded, result.Error);
        Assert.Null(Serdivan.GetWorkingHours(DayOfWeek.Saturday));
        Assert.Equal(TimeOfDayRange.Of(10, 0, 14, 0), Serdivan.GetWorkingHours(DayOfWeek.Sunday));
        Assert.Equal(6, Serdivan.ChairCount);
        Assert.Equal("Kemalpaşa Mah. No:12", Serdivan.Address);
        Assert.True(Serdivan.HostsDepartment(_a.Endodontics));
        Assert.False(Serdivan.HostsDepartment(_a.General));
        Assert.Equal(1, _a.Branches.SaveCount);
    }

    [Fact]
    public async Task Bitisi_acilistan_once_olan_gun_gun_adiyla_reddedilir()
    {
        var settings = AdminClinic.WithDay(AdminClinic.SettingsOf(Serdivan), DayOfWeek.Monday, new TimeOnly(18, 0), new TimeOnly(9, 0));

        var result = await _a.BranchService().UpdateAsync(_a.Clinic.Admin, Serdivan.Id, settings);

        Assert.Equal("Pazartesi: Bitiş saati başlangıç saatinden sonra olmalıdır.", result.Error);
    }

    [Fact]
    public async Task Ayni_ad_baska_subede_kullanilamaz_harf_buyuklugu_fark_etmez()
    {
        var update = await _a.BranchService().UpdateAsync(
            _a.Clinic.Admin, Serdivan.Id, AdminClinic.SettingsOf(Serdivan) with { Name = "KADIKÖY ŞUBESİ" });
        var create = await _a.BranchService().CreateAsync(_a.Clinic.Admin, "kadıköy şubesi", "İstanbul", null, 2);

        Assert.Equal("\"KADIKÖY ŞUBESİ\" adında başka bir şube var.", update.Error);
        Assert.False(create.Succeeded);
        Assert.Equal("Serdivan Şubesi", Serdivan.Name);
    }

    [Fact]
    public async Task Yeni_subeyi_sadece_zincir_yoneticisi_acar_sube_kapali_ve_bolumsuz_baslar()
    {
        var service = _a.BranchService();

        var byManager = await service.CreateAsync(_a.Clinic.SerdivanManager, "Hendek Şubesi", "Sakarya", null, 2);
        var byAdmin = await service.CreateAsync(_a.Clinic.Admin, "Hendek Şubesi", "Sakarya", null, 2);

        Assert.Equal("Yeni şubeyi sadece zincir yöneticisi açabilir.", byManager.Error);
        Assert.True(byAdmin.Succeeded);
        Assert.NotEqual(0, byAdmin.Created!.Id);
        Assert.Empty(byAdmin.Created.WorkingHours);
        Assert.Empty(byAdmin.Created.Departments);
    }

    [Fact]
    public async Task Unit_sayisi_sifir_olamaz_mesaj_alan_sinifindan()
    {
        var result = await _a.BranchService().CreateAsync(_a.Clinic.Admin, "Hendek Şubesi", "Sakarya", null, 0);

        Assert.Equal("Şubede en az bir ünit (diş koltuğu) bulunmalıdır.", result.Error);
    }
}

using DisKlinigi.Core.Administration;
using DisKlinigi.Core.Domain.Users;

namespace DisKlinigi.Tests.Administration;

/// <summary>
/// Şube personeli (5c) ve hekim kaydı (5d). Şubeye atama ve asistan hesabı <c>CanManageBranch</c>'e, hekimin ortak
/// bilgileri <c>CanManageChain</c>'e bağlı (K34). Dt. Ayşe Kaya'nın Serdivan'da müsaitlik aralığı var, Dt. Mehmet
/// Demir'in yok.
/// </summary>
public class StaffServiceTests
{
    private readonly AdminClinic _a = new();

    private DentistDraft AyseDraft(int[] branchIds, string name = "Dt. Ayşe Kaya") =>
        new(name, "ayse@ornek.com", _a.Endodontics.Id, [_a.Clinic.CanalTreatment.Id], branchIds);

    [Fact]
    public async Task Sube_yoneticisi_baska_subenin_personelini_goremez()
    {
        var service = _a.StaffService();

        Assert.Null(await service.GetBranchStaffAsync(_a.Clinic.SerdivanManager, _a.Clinic.Kadikoy.Id));

        var own = await service.GetBranchStaffAsync(_a.Clinic.SerdivanManager, _a.Clinic.Serdivan.Id);
        Assert.Equal([_a.Clinic.Ayse, _a.Clinic.Mehmet], own!.Dentists);
        Assert.Equal([_a.Clinic.SerdivanAssistant], own.Assistants);
        Assert.Empty(own.Assignable);
    }

    [Fact]
    public async Task Sube_yoneticisi_kendi_subesine_hekim_atar_baska_subeye_atayamaz()
    {
        var service = _a.StaffService();

        var own = await service.AssignDentistAsync(_a.Clinic.KadikoyManager, _a.Clinic.Kadikoy.Id, _a.Clinic.Ayse.Id);
        var other = await service.AssignDentistAsync(_a.Clinic.SerdivanManager, _a.Clinic.Kadikoy.Id, _a.Clinic.Mehmet.Id);

        Assert.True(own.Succeeded);
        Assert.True(_a.Clinic.Ayse.WorksAt(_a.Clinic.Kadikoy));
        Assert.True(other.NotFound);
        Assert.False(_a.Clinic.Mehmet.WorksAt(_a.Clinic.Kadikoy));
    }

    [Fact]
    public async Task Musaitlik_araligi_olan_hekim_subeden_cikarilamaz_araligi_olmayan_cikarilir()
    {
        var service = _a.StaffService();

        var ayse = await service.RemoveDentistAsync(_a.Clinic.SerdivanManager, _a.Clinic.Serdivan.Id, _a.Clinic.Ayse.Id);
        var mehmet = await service.RemoveDentistAsync(_a.Clinic.SerdivanManager, _a.Clinic.Serdivan.Id, _a.Clinic.Mehmet.Id);

        Assert.Equal(
            "Dt. Ayşe Kaya için bu şubede tanımlı müsaitlik aralıkları var (Serdivan Şubesi); önce bu aralıklar silinmelidir.",
            ayse.Error);
        Assert.True(_a.Clinic.Ayse.WorksAt(_a.Clinic.Serdivan));
        Assert.True(mehmet.Succeeded);
        Assert.False(_a.Clinic.Mehmet.WorksAt(_a.Clinic.Serdivan));
    }

    [Fact]
    public async Task Asistan_hesabi_gecici_parolayla_acilir_parola_sadece_ozet_olarak_saklanir()
    {
        var result = await _a.StaffService().CreateAssistantAsync(
            _a.Clinic.SerdivanManager, _a.Clinic.Serdivan.Id, "Gizem Kurt", "Gizem.Kurt@ornek.com ");

        Assert.True(result.Succeeded, result.Error);
        var account = result.Created!;
        var assistant = Assert.IsType<Assistant>(account.User);
        Assert.Equal(TemporaryPassword.Length, account.TemporaryPassword.Length);
        Assert.NotEqual(account.TemporaryPassword, assistant.PasswordHash);
        Assert.True(_a.Hasher.Verify(assistant.PasswordHash, account.TemporaryPassword));
        Assert.Equal("gizem.kurt@ornek.com", assistant.Email);
        Assert.Same(_a.Clinic.Serdivan, assistant.Branch);
        Assert.Contains(assistant, _a.Users.Users);
    }

    [Fact]
    public void Gecici_parolalar_her_seferinde_farkli()
    {
        var passwords = Enumerable.Range(0, 20).Select(_ => TemporaryPassword.Create()).ToHashSet();

        Assert.Equal(20, passwords.Count);
    }

    [Fact]
    public async Task Ayni_e_postayla_ikinci_hesap_acilamaz_baska_subeye_asistan_acilamaz()
    {
        var service = _a.StaffService();

        var duplicate = await service.CreateAssistantAsync(_a.Clinic.SerdivanManager, _a.Clinic.Serdivan.Id, "Elif Şahin", "elif@ornek.com");
        var otherBranch = await service.CreateAssistantAsync(_a.Clinic.SerdivanManager, _a.Clinic.Kadikoy.Id, "Gizem Kurt", "gizem@ornek.com");

        Assert.Equal("Bu e-posta adresiyle açılmış bir hesap var.", duplicate.Error);
        Assert.Equal("Şube bulunamadı.", otherBranch.Error);
    }

    [Fact]
    public async Task Hekim_kaydini_sube_yoneticisi_acamaz_zincir_yoneticisi_acar()
    {
        var draft = new DentistDraft("Dt. Burcu Aksu", "burcu@ornek.com", _a.General.Id,
            [_a.Clinic.Examination.Id], [_a.Clinic.Serdivan.Id, _a.Clinic.Kadikoy.Id]);
        var service = _a.StaffService();

        var byManager = await service.RegisterDentistAsync(_a.Clinic.SerdivanManager, draft);
        var byAdmin = await service.RegisterDentistAsync(_a.Clinic.Admin, draft);

        Assert.Equal("Hekim kaydını sadece zincir yöneticisi değiştirebilir.", byManager.Error);
        var dentist = Assert.IsType<Dentist>(byAdmin.Created!.User);
        Assert.Same(_a.General, dentist.Specialty);
        Assert.True(dentist.Offers(_a.Clinic.Examination));
        Assert.True(dentist.WorksAt(_a.Clinic.Serdivan) && dentist.WorksAt(_a.Clinic.Kadikoy));
        Assert.Empty(dentist.Availability); // aralıkları hekim kendisi giriyor
        Assert.True(_a.Hasher.Verify(dentist.PasswordHash, byAdmin.Created.TemporaryPassword));
    }

    [Fact]
    public async Task Hekim_kaydi_duzenlenirken_araligi_olan_subeden_cikarilamaz_ve_hekim_hic_degismez()
    {
        // Ayşe Hanım Kadıköy'e de atanıp formda sadece Kadıköy bırakılıyor; Serdivan'da aralığı var.
        _a.Clinic.Ayse.AssignToBranch(_a.Clinic.Kadikoy);

        var result = await _a.StaffService().UpdateDentistAsync(
            _a.Clinic.Admin, _a.Clinic.Ayse.Id, AyseDraft([_a.Clinic.Kadikoy.Id], name: "Dt. Ayşe Kaya Demir"));

        Assert.Contains("önce bu aralıklar silinmelidir", result.Error);
        Assert.Equal("Dt. Ayşe Kaya", _a.Clinic.Ayse.FullName);
        Assert.True(_a.Clinic.Ayse.WorksAt(_a.Clinic.Serdivan) && _a.Clinic.Ayse.WorksAt(_a.Clinic.Kadikoy));
        Assert.Equal(0, _a.Dentists.SaveCount);
    }

    [Fact]
    public async Task Hekimin_ortak_bilgileri_duzenlenir_baskasinin_e_postasi_alinamaz()
    {
        var service = _a.StaffService();
        var draft = AyseDraft([_a.Clinic.Serdivan.Id, _a.Clinic.Kadikoy.Id]) with
        {
            SpecialtyId = _a.General.Id,
            ServiceIds = [_a.Clinic.CanalTreatment.Id, _a.Clinic.Examination.Id]
        };

        var taken = await service.UpdateDentistAsync(_a.Clinic.Admin, _a.Clinic.Ayse.Id, draft with { Email = "mehmet@ornek.com" });
        var saved = await service.UpdateDentistAsync(_a.Clinic.Admin, _a.Clinic.Ayse.Id, draft);

        Assert.Equal("Bu e-posta adresiyle açılmış bir hesap var.", taken.Error);
        Assert.True(saved.Succeeded, saved.Error);
        Assert.Same(_a.General, _a.Clinic.Ayse.Specialty);
        Assert.True(_a.Clinic.Ayse.Offers(_a.Clinic.Examination));
        Assert.True(_a.Clinic.Ayse.WorksAt(_a.Clinic.Kadikoy));
        Assert.Equal(1, _a.Dentists.SaveCount);
    }
}

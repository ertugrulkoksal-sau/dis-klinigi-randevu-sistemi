using DisKlinigi.Core.Domain;

namespace DisKlinigi.Tests.Domain;

public class BranchWorkingHoursTests
{
    // 10 Kasım 2026 Salı, 14 Kasım 2026 Cumartesi, 15 Kasım 2026 Pazar.
    private static readonly DateOnly Tuesday = new(2026, 11, 10);
    private static readonly DateOnly Saturday = new(2026, 11, 14);
    private static readonly DateOnly Sunday = new(2026, 11, 15);

    private static Branch CreateBranch()
    {
        var branch = new Branch("Serdivan Şubesi", "Sakarya", "Üniversite Cad.", chairCount: 4);
        branch.SetWorkingHours(DayOfWeek.Tuesday, TimeOfDayRange.Of(9, 0, 18, 0));
        branch.SetWorkingHours(DayOfWeek.Saturday, TimeOfDayRange.Of(9, 0, 14, 0));
        return branch;
    }

    [Fact]
    public void Ayni_gune_ikinci_kez_saat_verilirse_eskisi_degisir()
    {
        var branch = CreateBranch();

        branch.SetWorkingHours(DayOfWeek.Tuesday, TimeOfDayRange.Of(10, 0, 17, 0));

        Assert.Single(branch.WorkingHours, w => w.Day == DayOfWeek.Tuesday);
        Assert.Equal(TimeOfDayRange.Of(10, 0, 17, 0), branch.GetWorkingHours(DayOfWeek.Tuesday));
    }

    [Fact]
    public void Kaydi_olmayan_gun_kapali_sayilir()
    {
        var branch = CreateBranch();

        Assert.Null(branch.GetWorkingHours(DayOfWeek.Sunday));
        Assert.False(branch.IsOpenDuring(TimeOfDayRange.Of(10, 0, 11, 0).On(Sunday)));
    }

    [Fact]
    public void Kapatilan_gun_artik_acik_degildir()
    {
        var branch = CreateBranch();

        branch.CloseOn(DayOfWeek.Saturday);

        Assert.Null(branch.GetWorkingHours(DayOfWeek.Saturday));
        Assert.False(branch.IsOpenDuring(TimeOfDayRange.Of(10, 0, 11, 0).On(Saturday)));
    }

    [Theory]
    [InlineData(9, 0, 9, 30, true)]     // açılışa yaslı
    [InlineData(17, 0, 18, 0, true)]    // kapanışa yaslı: 18:00'de biten randevu sığar
    [InlineData(8, 45, 9, 15, false)]   // açılıştan önce başlıyor
    [InlineData(17, 30, 18, 30, false)] // kapanıştan sonra bitiyor
    public void Sali_icin_calisma_saati_sinirlari(int sh, int sm, int eh, int em, bool expected)
    {
        var branch = CreateBranch();

        Assert.Equal(expected, branch.IsOpenDuring(TimeOfDayRange.Of(sh, sm, eh, em).On(Tuesday)));
    }

    [Fact]
    public void Her_gun_kendi_saatine_gore_degerlendirilir()
    {
        var branch = CreateBranch();
        var lateAfternoon = TimeOfDayRange.Of(15, 0, 16, 0);

        Assert.True(branch.IsOpenDuring(lateAfternoon.On(Tuesday)));
        Assert.False(branch.IsOpenDuring(lateAfternoon.On(Saturday))); // Cumartesi 14:00'te kapanıyor
    }

    [Fact]
    public void Bolum_iki_kez_eklenmez_ve_cikarilabilir()
    {
        var branch = CreateBranch();
        var endodontics = new Department("Endodonti");
        var orthodontics = new Department("Ortodonti");

        branch.AddDepartment(endodontics);
        branch.AddDepartment(endodontics);

        Assert.Single(branch.Departments);
        Assert.True(branch.HostsDepartment(endodontics));
        Assert.False(branch.HostsDepartment(orthodontics));

        branch.RemoveDepartment(endodontics);
        Assert.False(branch.HostsDepartment(endodontics));
    }

    [Fact]
    public void Disaridan_alinan_liste_uzerinden_calisma_saati_eklenemez()
    {
        var branch = CreateBranch();

        // Okunur liste ICollection'a çevrilse bile değiştirilemiyor (kapsülleme).
        var asCollection = (ICollection<DailyWorkingHours>)branch.WorkingHours;
        Assert.True(asCollection.IsReadOnly);
    }
}

using DisKlinigi.Core.Domain;
using DisKlinigi.Core.Domain.Users;

namespace DisKlinigi.Tests.Domain;

public class UserTests
{
    private const string Hash = "AQAAAAIAAYagAAAAE-ornek-ozet";

    private static readonly Branch Serdivan = new("Serdivan Şubesi", "Sakarya", "", chairCount: 4);
    private static readonly Branch Kadikoy = new("Kadıköy Şubesi", "İstanbul", "", chairCount: 5);
    private static readonly Department Endodontics = new("Endodonti");

    // Her rolden bir kullanıcı; yetki tablosu testleri bunlar üzerinden.
    public static TheoryData<string, User, bool, bool, bool> RoleMatrix() => new()
    {
        // rol adı, kullanıcı, Serdivan'ı yönetir mi, Kadıköy'ü yönetir mi, zincir geneli kayıtları (katalog, şube açma, hekim kaydı) yönetir mi
        { "Hasta", new Patient("Ali Yıldız", "ali@ornek.com", Hash), false, false, false },
        { "Hekim", new Dentist("Ayşe Kaya", "ayse@ornek.com", Hash, Endodontics), false, false, false },
        { "Asistan", new Assistant("Elif Şahin", "elif@ornek.com", Hash, Serdivan), false, false, false },
        { "Şube yöneticisi", new BranchManager("Murat Çelik", "murat@ornek.com", Hash, Serdivan), true, false, false },
        { "Zincir yöneticisi", new ChainAdministrator("Selin Aydın", "selin@ornek.com", Hash), true, true, true },
    };

    [Theory]
    [MemberData(nameof(RoleMatrix))]
    public void Her_rol_yetki_sorularini_kendi_kuraliyla_cevaplar(
        string roleName, User user, bool managesSerdivan, bool managesKadikoy, bool managesChain)
    {
        Assert.True(managesSerdivan == user.CanManageBranch(Serdivan), $"{roleName} / Serdivan");
        Assert.True(managesKadikoy == user.CanManageBranch(Kadikoy), $"{roleName} / Kadıköy");
        Assert.True(managesChain == user.CanManageChain, $"{roleName} / zincir");
    }

    [Fact]
    public void Sube_yoneticisi_baska_subeye_tasininca_eski_subeyi_yonetemez()
    {
        var manager = new BranchManager("Murat Çelik", "murat@ornek.com", Hash, Serdivan);

        manager.TransferTo(Kadikoy);

        Assert.False(manager.CanManageBranch(Serdivan));
        Assert.True(manager.CanManageBranch(Kadikoy));
    }

    [Fact]
    public void Her_alt_sinif_kendi_rolunu_bildirir()
    {
        Assert.Equal(UserRole.Patient, new Patient("A B", "a@b.com", Hash).Role);
        Assert.Equal(UserRole.Dentist, new Dentist("A B", "a@b.com", Hash, Endodontics).Role);
        Assert.Equal(UserRole.Assistant, new Assistant("A B", "a@b.com", Hash, Serdivan).Role);
        Assert.Equal(UserRole.BranchManager, new BranchManager("A B", "a@b.com", Hash, Serdivan).Role);
        Assert.Equal(UserRole.ChainAdministrator, new ChainAdministrator("A B", "a@b.com", Hash).Role);
    }

    [Fact]
    public void Eposta_kucuk_harfe_cevrilip_saklanir()
    {
        var patient = new Patient("Ali Yıldız", "  Ali.Yildiz@Ornek.COM ", Hash);

        Assert.Equal("ali.yildiz@ornek.com", patient.Email);
    }

    [Theory]
    [InlineData("")]
    [InlineData("ali")]
    [InlineData("ali@")]
    [InlineData("@ornek.com")]
    [InlineData("Ali <ali@ornek.com>")]
    public void Gecersiz_eposta_kabul_edilmez(string email)
    {
        Assert.Throws<DomainException>(() => new Patient("Ali Yıldız", email, Hash));
    }

    [Fact]
    public void Parola_ozeti_bos_olamaz()
    {
        Assert.Throws<DomainException>(() => new Patient("Ali Yıldız", "ali@ornek.com", " "));
    }

    [Fact]
    public void Asistan_ve_sube_yoneticisi_subesiz_olusturulamaz()
    {
        Assert.Throws<DomainException>(() => new Assistant("Elif Şahin", "elif@ornek.com", Hash, null!));
        Assert.Throws<DomainException>(() => new BranchManager("Murat Çelik", "murat@ornek.com", Hash, null!));
    }

    [Fact]
    public void Hastanin_bos_telefonu_null_olarak_saklanir()
    {
        var patient = new Patient("Ali Yıldız", "ali@ornek.com", Hash, phoneNumber: "   ");

        Assert.Null(patient.PhoneNumber);
    }

    [Fact]
    public void Sadece_asistan_ve_sube_yoneticisinin_bagli_oldugu_tek_sube_var()
    {
        var dentist = new Dentist("Ayşe Kaya", "ayse@ornek.com", Hash, Endodontics);
        dentist.AssignToBranch(Serdivan);

        Assert.Same(Serdivan, new Assistant("Elif Şahin", "elif@ornek.com", Hash, Serdivan).AssignedBranch);
        Assert.Same(Serdivan, new BranchManager("Murat Çelik", "murat@ornek.com", Hash, Serdivan).AssignedBranch);
        // Hekim birden fazla şubede çalışabildiği için "bağlı olduğu tek şube" yok.
        Assert.Null(dentist.AssignedBranch);
        Assert.Null(new Patient("Ali Yıldız", "ali@ornek.com", Hash).AssignedBranch);
        Assert.Null(new ChainAdministrator("Selin Aydın", "selin@ornek.com", Hash).AssignedBranch);
    }

    [Theory]
    [InlineData("  Ali.Yildiz@Ornek.COM ", "ali.yildiz@ornek.com")]
    [InlineData("ali.yildiz@ornek.com", "ali.yildiz@ornek.com")]
    [InlineData(null, "")]
    public void Eposta_giriste_ve_kayitta_ayni_bicime_getirilir(string? input, string expected)
    {
        Assert.Equal(expected, User.NormalizeEmail(input));
    }
}

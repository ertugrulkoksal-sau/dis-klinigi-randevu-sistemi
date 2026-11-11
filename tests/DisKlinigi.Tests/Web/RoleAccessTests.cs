using System.Net;
using DisKlinigi.Core.Domain.Users;
using DisKlinigi.Web.Navigation;

namespace DisKlinigi.Tests.Web;

/// <summary>
/// Her rol, her sayfa: açılması gereken açılıyor, açılmaması gereken "yetki yok" sayfasına gidiyor.
/// Beklenen tablo menüden türetilmiyor, elle yazıldı; menü bir sayfayı yanlış role gösterse
/// ya da bir politika yanlış rol listesi alsa test yakalıyor.
/// </summary>
[Collection(WebCollection.Name)]
public class RoleAccessTests
{
    private static readonly string[] AllPages =
    [
        "/Patient/Book", "/Patient/Appointments",
        "/Clinic/Calendar", "/Clinic/Queue", "/Clinic/Availability",
        "/Admin/Branches", "/Admin/Catalog", "/Admin/Staff", "/Admin/Dentists"
    ];

    public static TheoryData<UserRole, string, string[]> Matrix() => new()
    {
        { UserRole.Patient, "ali.yildiz@ornek.test", ["/Patient/Book", "/Patient/Appointments"] },
        { UserRole.Dentist, "ayse.kaya@disklinigi.test", ["/Clinic/Calendar", "/Clinic/Queue", "/Clinic/Availability"] },
        { UserRole.Assistant, "elif.sahin@disklinigi.test", ["/Clinic/Calendar", "/Clinic/Queue"] },
        { UserRole.BranchManager, "murat.celik@disklinigi.test", ["/Admin/Branches", "/Admin/Catalog", "/Admin/Staff"] },
        { UserRole.ChainAdministrator, "selin.aydin@disklinigi.test",
            ["/Admin/Branches", "/Admin/Catalog", "/Admin/Staff", "/Admin/Dentists"] },
    };

    private readonly ClinicWebFactory _factory;

    public RoleAccessTests(ClinicWebFactory factory)
    {
        _factory = factory;
    }

    [Theory]
    [MemberData(nameof(Matrix))]
    public async Task Her_rol_sadece_kendi_sayfalarini_acar(UserRole role, string email, string[] allowed)
    {
        var browser = _factory.CreateBrowser();
        var login = await browser.LoginAsync(email);
        Assert.Equal(Path(MainMenu.HomeFor(role)), login.RedirectTarget());

        foreach (var page in AllPages)
        {
            var response = await browser.GetAsync(page);
            if (allowed.Contains(page))
                Assert.True(response.StatusCode == HttpStatusCode.OK, $"{role} {page} sayfasını açabilmeli ({response.StatusCode}).");
            else
                Assert.StartsWith("/Account/AccessDenied", response.RedirectTarget());
        }
    }

    [Theory]
    [MemberData(nameof(Matrix))]
    public void Menude_sadece_rolun_acabildigi_sayfalar_var(UserRole role, string email, string[] allowed)
    {
        _ = email;
        Assert.All(MainMenu.For(role), item => Assert.Contains(Path(item), allowed));
    }

    [Fact]
    public async Task Yetki_yok_sayfasi_kullaniciyi_kendi_ana_sayfasina_yonlendiren_baglanti_icerir()
    {
        var browser = _factory.CreateBrowser();
        await browser.LoginAsync("ali.yildiz@ornek.test");

        var denied = await browser.GetAsync((await browser.GetAsync("/Admin/Branches")).RedirectTarget());

        Assert.Equal(HttpStatusCode.OK, denied.StatusCode);
        var html = await denied.Content.ReadAsStringAsync();
        Assert.Contains("Bu sayfaya erişiminiz yok", html);
        Assert.Contains("href=\"/Patient/Book\"", html);
    }

    private static string Path(MenuItem item) => $"/{item.Controller}/{item.Action}";
}

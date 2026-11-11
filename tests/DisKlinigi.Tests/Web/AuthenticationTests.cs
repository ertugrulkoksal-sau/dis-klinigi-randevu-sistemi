using System.Net;
using DisKlinigi.Core.Domain.Users;
using DisKlinigi.Web.Security;
using Microsoft.EntityFrameworkCore;

namespace DisKlinigi.Tests.Web;

/// <summary>Giriş, kayıt ve çıkış; uygulamanın kendisi üzerinden, tarayıcının göndereceği isteklerle.</summary>
[Collection(WebCollection.Name)]
public class AuthenticationTests
{
    private const string LoginFailed = "E-posta veya parola hatalı.";

    private readonly ClinicWebFactory _factory;

    public AuthenticationTests(ClinicWebFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Girissiz_kullanici_korumali_sayfadan_giris_sayfasina_yonlenir()
    {
        var browser = _factory.CreateBrowser();

        var response = await browser.GetAsync("/Patient/Book");

        Assert.Equal("/Account/Login?ReturnUrl=%2FPatient%2FBook", response.RedirectTarget());
    }

    [Fact]
    public async Task Kok_adres_girissiz_kullaniciyi_girise_girisliyi_kendi_ana_sayfasina_goturur()
    {
        var browser = _factory.CreateBrowser();
        Assert.Equal("/Account/Login", (await browser.GetAsync("/")).RedirectTarget());

        await browser.LoginAsync("elif.sahin@disklinigi.test");

        Assert.Equal("/Clinic/Calendar", (await browser.GetAsync("/")).RedirectTarget());
    }

    [Fact]
    public async Task Giris_sayfasi_ve_stil_dosyasi_girissiz_acilir()
    {
        var browser = _factory.CreateBrowser();

        Assert.Equal(HttpStatusCode.OK, (await browser.GetAsync("/Account/Login")).StatusCode);
        // Varsayılan politika "giriş zorunlu"; statik dosyalar ayrıca açılmasaydı burada 302 dönerdi.
        Assert.Equal(HttpStatusCode.OK, (await browser.GetAsync("/css/site.css")).StatusCode);
    }

    [Fact]
    public async Task Dogru_parolayla_giris_yapan_hasta_kendi_ana_sayfasina_yonlenir()
    {
        var browser = _factory.CreateBrowser();

        var login = await browser.LoginAsync("Ali.Yildiz@ornek.test");

        Assert.Equal("/Patient/Book", login.RedirectTarget());
        var page = await browser.GetStringAsync("/Patient/Book");
        Assert.Contains("Ali Yıldız", page);
        Assert.Contains("· Hasta", page);
        Assert.Contains("Randevularım", page); // hasta menüsü
        Assert.DoesNotContain("Onay kuyruğu", page);
    }

    [Fact]
    public async Task Asistanin_menusunde_bagli_oldugu_sube_gorunur()
    {
        var browser = _factory.CreateBrowser();
        await browser.LoginAsync("elif.sahin@disklinigi.test");

        var page = await browser.GetStringAsync("/Clinic/Calendar");

        Assert.Contains("Asistan, Serdivan Şubesi", page);
        Assert.DoesNotContain(">Müsaitlik<", page); // müsaitlik sadece hekim menüsünde
    }

    [Theory]
    [InlineData("ali.yildiz@ornek.test", "yanlis-parola")]
    [InlineData("kayitli-degil@ornek.test", BrowserExtensions.DemoPassword)]
    public async Task Yanlis_parola_ve_kayitsiz_eposta_ayni_mesaji_alir_ve_oturum_acilmaz(string email, string password)
    {
        var browser = _factory.CreateBrowser();

        var response = await browser.LoginAsync(email, password);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains(LoginFailed, html);
        Assert.DoesNotContain(password, html); // parola sayfaya geri yazılmıyor
        Assert.False(response.Headers.TryGetValues("Set-Cookie", out var cookies)
            && cookies.Any(c => c.StartsWith("DisKlinigi.Oturum=", StringComparison.Ordinal)));
        Assert.StartsWith("/Account/Login", (await browser.GetAsync("/Patient/Book")).RedirectTarget());
    }

    [Fact]
    public async Task Girisle_birlikte_istenen_site_ici_sayfaya_donulur()
    {
        var browser = _factory.CreateBrowser();

        var login = await browser.LoginAsync("ali.yildiz@ornek.test", returnUrl: "/Patient/Appointments");

        Assert.Equal("/Patient/Appointments", login.RedirectTarget());
    }

    [Theory]
    [InlineData("https://baska-site.example/giris")]
    [InlineData("//baska-site.example")]
    public async Task Disari_yonlendiren_donus_adresi_dikkate_alinmaz(string returnUrl)
    {
        var browser = _factory.CreateBrowser();

        var login = await browser.LoginAsync("ali.yildiz@ornek.test", returnUrl: returnUrl);

        Assert.Equal("/Patient/Book", login.RedirectTarget());
    }

    [Fact]
    public async Task Form_belirteci_olmadan_giris_istegi_reddedilir()
    {
        var browser = _factory.CreateBrowser();

        var response = await browser.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Login.Email"] = "ali.yildiz@ornek.test",
            ["Login.Password"] = BrowserExtensions.DemoPassword
        }));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Kayit_olan_hasta_giris_yapmis_olur_ve_parolasi_veritabaninda_ozetli_durur()
    {
        var browser = _factory.CreateBrowser();
        const string password = "Yeni-Hasta-2026";

        var response = await Register(browser, "Deniz Er", "deniz.er@ornek.test", password);

        Assert.Equal("/Patient/Book", response.RedirectTarget());
        Assert.Contains("Kaydınız oluşturuldu", await browser.GetStringAsync("/Patient/Book"));

        await using var db = _factory.CreateDbContext();
        var patient = await db.Users.OfType<Patient>().SingleAsync(p => p.Email == "deniz.er@ornek.test");
        Assert.NotEqual(password, patient.PasswordHash);
        Assert.DoesNotContain(password, patient.PasswordHash);
        Assert.True(new AspNetPasswordHasher().Verify(patient.PasswordHash, password));
    }

    [Fact]
    public async Task Kayitli_eposta_ile_ikinci_hesap_acilmaz()
    {
        var browser = _factory.CreateBrowser();

        var response = await Register(browser, "Başka Biri", "ALI.YILDIZ@ornek.test", "Baska-Parola-1");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Bu e-posta adresiyle açılmış bir hesap var", await response.Content.ReadAsStringAsync());
        await using var db = _factory.CreateDbContext();
        Assert.Equal(1, await db.Users.CountAsync(u => u.Email == "ali.yildiz@ornek.test"));
    }

    [Fact]
    public async Task Kisa_parola_ve_eslesmeyen_tekrar_kayitta_reddedilir()
    {
        var browser = _factory.CreateBrowser();

        var shortPassword = await Register(browser, "Kısa Parola", "kisa@ornek.test", "1234567");
        var mismatch = await Register(browser, "Farklı Parola", "farkli@ornek.test", "Uzun-Parola-1", confirm: "Uzun-Parola-2");

        Assert.Contains("Parola en az 8", await shortPassword.Content.ReadAsStringAsync());
        Assert.Contains("Parolalar aynı değil.", await mismatch.Content.ReadAsStringAsync());
        await using var db = _factory.CreateDbContext();
        Assert.False(await db.Users.AnyAsync(u => u.Email == "kisa@ornek.test" || u.Email == "farkli@ornek.test"));
    }

    [Fact]
    public async Task Cikis_yapinca_korumali_sayfa_acilmaz()
    {
        var browser = _factory.CreateBrowser();
        await browser.LoginAsync("ali.yildiz@ornek.test");

        var logout = await browser.PostFormAsync("/Patient/Book", "/Account/Logout", new Dictionary<string, string>());

        Assert.Equal("/Account/Login", logout.RedirectTarget());
        Assert.StartsWith("/Account/Login", (await browser.GetAsync("/Patient/Book")).RedirectTarget());
    }

    /// <summary>
    /// Çerez hâlâ geçerli ama kullanıcı artık yok (ör. veritabanı silinip yeniden oluşturuldu):
    /// oturum bir sonraki istekte kapanıyor (K42).
    /// </summary>
    [Fact]
    public async Task Veritabaninda_artik_olmayan_kullanicinin_cerezi_gecersiz_sayilir()
    {
        var browser = _factory.CreateBrowser();
        await Register(browser, "Silinecek Hasta", "silinecek@ornek.test", "Silinecek-123");
        Assert.Equal(HttpStatusCode.OK, (await browser.GetAsync("/Patient/Book")).StatusCode);

        await using (var db = _factory.CreateDbContext())
            await db.Users.Where(u => u.Email == "silinecek@ornek.test").ExecuteDeleteAsync();

        Assert.StartsWith("/Account/Login", (await browser.GetAsync("/Patient/Book")).RedirectTarget());
    }

    private static Task<HttpResponseMessage> Register(
        HttpClient browser, string fullName, string email, string password, string? confirm = null) =>
        browser.PostFormAsync("/Account/Login", "/Account/Register", new Dictionary<string, string>
        {
            ["Register.FullName"] = fullName,
            ["Register.Email"] = email,
            ["Register.Password"] = password,
            ["Register.ConfirmPassword"] = confirm ?? password
        });
}

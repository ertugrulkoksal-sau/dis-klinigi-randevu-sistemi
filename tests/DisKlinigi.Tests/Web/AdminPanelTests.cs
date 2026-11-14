using System.Net;
using System.Text.RegularExpressions;
using DisKlinigi.Core.Domain;
using DisKlinigi.Core.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace DisKlinigi.Tests.Web;

/// <summary>
/// Yönetici paneli uygulamanın kendisi üzerinden: şube yöneticisinin sadece kendi şubesini görmesi ve
/// değiştirebilmesi (K3), kataloğu salt okunur görmesi, geçici parolayla asistan hesabı ve çalışma saati
/// daraltılırken müsaitlik kontrolü (K24). Testler örnek veriyi diğer testleri etkileyecek şekilde değiştirmiyor:
/// reddedilen işlemler hiçbir şey kaydetmiyor, açılan hesaplar yeni e-postalarla.
/// </summary>
[Collection(WebCollection.Name)]
public partial class AdminPanelTests
{
    private const string SerdivanManager = "murat.celik@disklinigi.test";
    private const string ChainAdmin = "selin.aydin@disklinigi.test";

    private readonly ClinicWebFactory _factory;

    public AdminPanelTests(ClinicWebFactory factory)
    {
        _factory = factory;
        _ = factory.Server;
    }

    [Fact]
    public async Task Sube_yoneticisi_sadece_kendi_subesini_gorur_baska_subenin_numarasi_bulunamadi_olur()
    {
        var kadikoy = await BranchIdAsync("Kadıköy Şubesi");
        var browser = await LoginAsync(SerdivanManager);

        var page = await browser.GetStringAsync("/Admin/Branches");
        Assert.Contains("Şubem", page);
        Assert.Contains("value=\"Serdivan Şubesi\"", page);
        Assert.DoesNotContain("Kadıköy Şubesi", page);
        Assert.DoesNotContain("Adapazarı Merkez Şubesi", page);
        Assert.DoesNotContain("Yeni şube", page);

        var other = await browser.GetAsync($"/Admin/Branches/{kadikoy}");
        Assert.Equal("/Admin/Branches", other.RedirectTarget());
        var staff = await browser.GetAsync($"/Admin/Staff?branchId={kadikoy}");
        Assert.Equal("/Admin/Staff", staff.RedirectTarget());
    }

    [Fact]
    public async Task Sube_yoneticisi_baska_subenin_formunu_gonderemez()
    {
        var kadikoy = await BranchIdAsync("Kadıköy Şubesi");
        var browser = await LoginAsync(SerdivanManager);

        var post = await browser.PostFormAsync("/Admin/Branches", $"/Admin/SaveBranch/{kadikoy}", new Dictionary<string, string>
        {
            ["Form.Name"] = "Ele geçirilmiş şube",
            ["Form.City"] = "İstanbul",
            ["Form.ChairCount"] = "1"
        });

        Assert.Equal("/Admin/Branches", post.RedirectTarget());
        await using var db = _factory.CreateDbContext();
        var branch = await db.Branches.SingleAsync(b => b.Id == kadikoy);
        Assert.Equal("Kadıköy Şubesi", branch.Name);
        Assert.Equal(5, branch.ChairCount);
    }

    [Fact]
    public async Task Calisma_saati_daraltilirken_disarida_kalan_musaitlik_varsa_kaydedilmez()
    {
        var serdivan = await BranchIdAsync("Serdivan Şubesi");
        var browser = await LoginAsync(ChainAdmin);
        var page = await browser.GetStringAsync($"/Admin/Branches/{serdivan}");

        // Formu sayfadaki haliyle gönderip sadece Salı açılışını 12:00'ye çekiyoruz.
        var form = FormFields(page);
        form["Form.Days[1].Opens"] = "12:00";
        var post = await browser.PostFormAsync($"/Admin/Branches/{serdivan}", $"/Admin/SaveBranch/{serdivan}", form);

        Assert.Equal(HttpStatusCode.OK, post.StatusCode);
        var html = WebUtility.HtmlDecode(await post.Content.ReadAsStringAsync());
        Assert.Contains("Salı 09:00–13:00 aralığı yeni saatin (12:00–18:00) dışında kalıyor", html);

        await using var db = _factory.CreateDbContext();
        var branch = await db.Branches.Include(b => b.WorkingHours).SingleAsync(b => b.Id == serdivan);
        Assert.Equal(TimeOfDayRange.Of(9, 0, 18, 0), branch.GetWorkingHours(DayOfWeek.Tuesday));
    }

    [Fact]
    public async Task Sube_yoneticisi_katalogu_salt_okunur_gorur_ve_degistiremez()
    {
        var browser = await LoginAsync(SerdivanManager);

        var page = await browser.GetStringAsync("/Admin/Catalog");
        Assert.Contains("Kanal tedavisi", page);
        Assert.DoesNotContain("Yeni hizmet", page);
        Assert.DoesNotContain("Adı değiştir", page);
        Assert.DoesNotContain("/Admin/SaveService", page);

        // Sayfada form yok; adres elle yazılsa da politika (sadece zincir yöneticisi) kapıyı kapatıyor.
        var post = await browser.PostFormAsync("/Admin/Branches", "/Admin/AddDepartment",
            new Dictionary<string, string> { ["name"] = "Ağız Kokusu" });
        Assert.StartsWith("/Account/AccessDenied", post.RedirectTarget());
    }

    [Fact]
    public async Task Sube_yoneticisi_asistan_acar_gecici_parola_bir_kez_gorunur_ve_girise_yarar()
    {
        var serdivan = await BranchIdAsync("Serdivan Şubesi");
        var browser = await LoginAsync(SerdivanManager);

        var post = await browser.PostFormAsync("/Admin/Staff", $"/Admin/CreateAssistant?branchId={serdivan}", new Dictionary<string, string>
        {
            ["Form.FullName"] = "Gizem Kurt",
            ["Form.Email"] = "gizem.kurt@disklinigi.test"
        });

        Assert.Equal(HttpStatusCode.OK, post.StatusCode);
        var html = await post.Content.ReadAsStringAsync();
        var password = TempPasswordPattern().Match(html);
        Assert.True(password.Success, "Geçici parola sayfada yok.");

        await using (var db = _factory.CreateDbContext())
        {
            var assistant = await db.Users.OfType<Assistant>().Include(a => a.Branch).SingleAsync(a => a.Email == "gizem.kurt@disklinigi.test");
            Assert.Equal(serdivan, assistant.Branch.Id);
            Assert.DoesNotContain(password.Groups[1].Value, assistant.PasswordHash);
        }

        // Personel listesinde görünüyor, parola ise sayfa yeniden açılınca bir daha görünmüyor.
        var again = await browser.GetStringAsync("/Admin/Staff");
        Assert.Contains("Gizem Kurt", again);
        Assert.DoesNotContain(password.Groups[1].Value, again);

        var assistantBrowser = _factory.CreateBrowser();
        var login = await assistantBrowser.LoginAsync("gizem.kurt@disklinigi.test", password.Groups[1].Value);
        Assert.Equal("/Clinic/Calendar", login.RedirectTarget());
    }

    [Fact]
    public async Task Musaitlik_araligi_olan_hekim_subeden_cikarilamaz()
    {
        var serdivan = await BranchIdAsync("Serdivan Şubesi");
        int ayse;
        await using (var db = _factory.CreateDbContext())
            ayse = await db.Dentists.Where(d => d.Email == "ayse.kaya@disklinigi.test").Select(d => d.Id).SingleAsync();
        var browser = await LoginAsync(SerdivanManager);

        var post = await browser.PostFormAsync("/Admin/Staff", $"/Admin/RemoveDentist?branchId={serdivan}&dentistId={ayse}",
            new Dictionary<string, string>());

        Assert.Equal($"/Admin/Staff?branchId={serdivan}", post.RedirectTarget());
        var page = WebUtility.HtmlDecode(await browser.GetStringAsync(post.RedirectTarget()));
        Assert.Contains("önce bu aralıklar silinmelidir", page);
        await using var check = _factory.CreateDbContext();
        Assert.True(await check.Dentists.AnyAsync(d => d.Id == ayse && d.Branches.Any(b => b.Id == serdivan)));
    }

    // ---- Yardımcılar ----

    [GeneratedRegex("class=\"temp-password\">([^<]+)<")]
    private static partial Regex TempPasswordPattern();

    [GeneratedRegex("<input[^>]*name=\"(Form\\.[^\"]+)\"[^>]*>")]
    private static partial Regex InputPattern();

    /// <summary>
    /// Sayfadaki şube formunun alanları, tarayıcının göndereceği haliyle: metin ve saat alanları değerleriyle,
    /// işaretli kutular "true" ya da bölüm numarasıyla. Bölümler aynı adla birden fazla kez gönderildiği için
    /// her biri sırayla indeksli adla yazılıyor (Form.DepartmentIds[0]...).
    /// </summary>
    private static Dictionary<string, string> FormFields(string html)
    {
        var fields = new Dictionary<string, string>();
        var departments = 0;
        foreach (Match input in InputPattern().Matches(html))
        {
            var tag = input.Value;
            var name = input.Groups[1].Value;
            var value = Regex.Match(tag, "value=\"([^\"]*)\"").Groups[1].Value;
            if (tag.Contains("type=\"checkbox\""))
            {
                if (!tag.Contains("checked"))
                    continue;
                if (name == "Form.DepartmentIds")
                    name = $"Form.DepartmentIds[{departments++}]";
            }
            fields[name] = WebUtility.HtmlDecode(value);
        }
        return fields;
    }

    private async Task<int> BranchIdAsync(string name)
    {
        await using var db = _factory.CreateDbContext();
        return await db.Branches.Where(b => b.Name == name).Select(b => b.Id).SingleAsync();
    }

    private async Task<HttpClient> LoginAsync(string email)
    {
        var browser = _factory.CreateBrowser();
        await browser.LoginAsync(email);
        return browser;
    }
}

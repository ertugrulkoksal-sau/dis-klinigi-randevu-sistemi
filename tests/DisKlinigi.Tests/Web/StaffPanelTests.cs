using System.Net;
using DisKlinigi.Core.Domain;
using DisKlinigi.Core.Domain.Appointments;
using DisKlinigi.Core.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace DisKlinigi.Tests.Web;

/// <summary>
/// Personel paneli uygulamanın kendisi üzerinden: onay kuyruğu, takvimdeki butonlar, ret gerekçesi,
/// sonuç işleme ve müsaitlik aralığı silme. Uygulama gerçek saatle çalıştığı için testler kendi
/// randevularını şu anki zamana göre yazıyor; örnek verideki randevulara dokunmuyor.
/// </summary>
[Collection(WebCollection.Name)]
public class StaffPanelTests
{
    private const string Ayse = "ayse.kaya@disklinigi.test";
    private const string SerdivanAssistant = "elif.sahin@disklinigi.test";
    private const string KadikoyAssistant = "can.ozturk@disklinigi.test";

    private readonly ClinicWebFactory _factory;

    public StaffPanelTests(ClinicWebFactory factory)
    {
        _factory = factory;
        // Testler veritabanına sayfa açmadan önce yazıyor; şema ve örnek veri sunucu açılırken kuruluyor.
        _ = factory.Server;
    }

    [Fact]
    public async Task Hekim_bekleyen_randevuyu_onaylar_onayli_randevuda_onayla_butonu_olmaz()
    {
        var start = DateTime.Today.AddDays(3).AddHours(7);
        var id = await AddAppointmentAsync("Kanal tedavisi", start);
        var browser = await LoginAsync(Ayse);

        var queue = await browser.GetStringAsync("/Clinic/Queue");
        Assert.Contains($"/Clinic/Approve/{id}", queue);

        var approve = await browser.PostFormAsync("/Clinic/Queue", $"/Clinic/Approve/{id}",
            new Dictionary<string, string> { ["returnUrl"] = "/Clinic/Queue" });

        Assert.Equal("/Clinic/Queue", approve.RedirectTarget());
        Assert.Equal(AppointmentStatus.Confirmed, await StatusAsync(id));
        Assert.DoesNotContain($"/Clinic/Approve/{id}", await browser.GetStringAsync("/Clinic/Queue"));

        // Takvimde aynı randevu: onaylı ve başlamamış, yani sadece "İptal et…" var.
        var calendar = await browser.GetStringAsync($"/Clinic/Calendar?date={start:yyyy-MM-dd}");
        Assert.Contains($"id=\"randevu-{id}\"", calendar);
        Assert.DoesNotContain($"/Clinic/Approve/{id}", calendar);
        Assert.DoesNotContain($"reject={id}", calendar);
        Assert.Contains($"cancel={id}", calendar);
    }

    [Fact]
    public async Task Baska_subenin_asistani_randevuyu_onaylayamaz_ve_goremez()
    {
        var id = await AddAppointmentAsync("Kanal tedavisi", DateTime.Today.AddDays(4).AddHours(7));
        var browser = await LoginAsync(KadikoyAssistant);

        Assert.DoesNotContain($"randevu-{id}", await browser.GetStringAsync("/Clinic/Queue"));
        await browser.PostFormAsync("/Clinic/Queue", $"/Clinic/Approve/{id}", new Dictionary<string, string>());

        Assert.Contains("Randevu bulunamadı.", await browser.GetStringAsync("/Clinic/Queue"));
        Assert.Equal(AppointmentStatus.PendingApproval, await StatusAsync(id));
    }

    [Fact]
    public async Task Asistan_gerekce_yazmadan_reddedemez_gerekce_yazinca_hasta_gorur()
    {
        var id = await AddAppointmentAsync("Kanal tedavisi", DateTime.Today.AddDays(5).AddHours(7));
        var browser = await LoginAsync(SerdivanAssistant);

        var form = await browser.GetStringAsync($"/Clinic/Queue?reject={id}");
        Assert.Contains("Ret gerekçesi (hastaya gösterilir)", form);

        await browser.PostFormAsync("/Clinic/Queue", $"/Clinic/Reject/{id}", new Dictionary<string, string> { ["reason"] = " " });
        Assert.Contains("Ret gerekçesi yazılmalıdır; hastaya gösterilecek.", await browser.GetStringAsync("/Clinic/Queue"));
        Assert.Equal(AppointmentStatus.PendingApproval, await StatusAsync(id));

        await browser.PostFormAsync("/Clinic/Queue", $"/Clinic/Reject/{id}",
            new Dictionary<string, string> { ["reason"] = "Önce muayene randevusu alın." });

        await using var db = _factory.CreateDbContext();
        var saved = await db.Appointments.SingleAsync(a => a.Id == id);
        Assert.Equal(AppointmentStatus.Rejected, saved.Status);
        Assert.Equal("Önce muayene randevusu alın.", saved.RejectionReason);
    }

    [Fact]
    public async Task Saati_gelen_onayli_randevuda_tamamlandi_ve_gelmedi_butonlari_cikar()
    {
        var start = DateTime.Now.AddMinutes(-10);
        var id = await AddAppointmentAsync("Muayene", start);
        var browser = await LoginAsync(Ayse);

        var calendar = await browser.GetStringAsync($"/Clinic/Calendar?date={start:yyyy-MM-dd}");
        Assert.Contains($"/Clinic/Complete/{id}", calendar);
        Assert.Contains($"/Clinic/NoShow/{id}", calendar);
        Assert.DoesNotContain($"cancel={id}", calendar); // başlamış randevu iptal edilemez (K28)

        await browser.PostFormAsync($"/Clinic/Calendar?date={start:yyyy-MM-dd}", $"/Clinic/Complete/{id}", new Dictionary<string, string>());

        Assert.Equal(AppointmentStatus.Completed, await StatusAsync(id));
    }

    [Fact]
    public async Task Ileri_tarihte_randevusu_olan_musaitlik_araligi_silinemez()
    {
        var browser = await LoginAsync(Ayse);
        var serdivan = await BranchIdAsync("Serdivan Şubesi");

        // Cumartesi aralığı ekle (Serdivan Cumartesi 09:00–14:00 açık, Ayşe Hanım'ın o gün aralığı yok).
        var added = await browser.PostFormAsync("/Clinic/Availability", "/Clinic/AddAvailability", new Dictionary<string, string>
        {
            ["Form.BranchId"] = serdivan.ToString(),
            ["Form.Day"] = "Saturday",
            ["Form.Start"] = "09:00",
            ["Form.End"] = "10:00"
        });
        Assert.Equal("/Clinic/Availability", added.RedirectTarget());
        var windowId = await SaturdayWindowIdAsync();

        // O aralığa düşen ileri tarihli bir randevu varken silme reddediliyor.
        var nextSaturday = DateTime.Today.AddDays(((int)DayOfWeek.Saturday - (int)DateTime.Today.DayOfWeek + 7) % 7 + 7);
        var id = await AddAppointmentAsync("Muayene", nextSaturday.AddHours(9));
        Assert.Contains(DisKlinigi.Web.Models.Display.ShortDate(DateOnly.FromDateTime(nextSaturday)), await browser.GetStringAsync("/Clinic/Availability"));

        await browser.PostFormAsync("/Clinic/Availability", $"/Clinic/RemoveAvailability/{windowId}", new Dictionary<string, string>());
        Assert.Contains("ileri tarihte 1 randevu var", WebUtility.HtmlDecode(await browser.GetStringAsync("/Clinic/Availability")));
        Assert.NotNull(await SaturdayWindowIdAsync());

        // Randevu iptal edilince aralık silinebiliyor.
        await browser.PostFormAsync("/Clinic/Calendar", $"/Clinic/Cancel/{id}", new Dictionary<string, string>());
        await browser.PostFormAsync("/Clinic/Availability", $"/Clinic/RemoveAvailability/{windowId}", new Dictionary<string, string>());

        Assert.Null(await SaturdayWindowIdAsync());
    }

    [Fact]
    public async Task Calismadigi_saati_iceren_aralik_alan_sinifinin_mesajiyla_reddedilir()
    {
        var browser = await LoginAsync(Ayse);

        var response = await browser.PostFormAsync("/Clinic/Availability", "/Clinic/AddAvailability", new Dictionary<string, string>
        {
            ["Form.BranchId"] = (await BranchIdAsync("Serdivan Şubesi")).ToString(),
            ["Form.Day"] = "Saturday",
            ["Form.Start"] = "10:00",
            ["Form.End"] = "16:00"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("10:00–16:00 aralığı, Serdivan Şubesi Cumartesi çalışma saatine (09:00–14:00) sığmıyor.",
            WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync()));
    }

    // ---- Yardımcılar ----

    private async Task<HttpClient> LoginAsync(string email)
    {
        var browser = _factory.CreateBrowser();
        await browser.LoginAsync(email);
        return browser;
    }

    /// <summary>
    /// Ali Yıldız'a, kurallardan geçirmeden, Serdivan'da Dt. Ayşe Kaya'dan randevu yazar. Saatler sabah
    /// 07:00 civarı seçildi ki örnek verideki randevularla karışmasın; bu testler kuralları değil
    /// personel ekranını sınıyor.
    /// </summary>
    private async Task<int> AddAppointmentAsync(string serviceName, DateTime start)
    {
        await using var db = _factory.CreateDbContext();
        var patient = (Patient)await db.Users.SingleAsync(u => u.Email == "ali.yildiz@ornek.test");
        var dentist = await db.Dentists.SingleAsync(d => d.Email == Ayse);
        var branch = await db.Branches.SingleAsync(b => b.Name == "Serdivan Şubesi");
        var service = await db.Services.SingleAsync(s => s.Name == serviceName);
        var appointment = new Appointment(patient, dentist, service, branch, start, createdAt: start.AddDays(-1));
        db.Appointments.Add(appointment);
        await db.SaveChangesAsync();
        return appointment.Id;
    }

    private async Task<AppointmentStatus> StatusAsync(int id)
    {
        await using var db = _factory.CreateDbContext();
        return (await db.Appointments.SingleAsync(a => a.Id == id)).Status;
    }

    private async Task<int> BranchIdAsync(string name)
    {
        await using var db = _factory.CreateDbContext();
        return (await db.Branches.SingleAsync(b => b.Name == name)).Id;
    }

    private async Task<int?> SaturdayWindowIdAsync()
    {
        await using var db = _factory.CreateDbContext();
        var ayse = await db.Dentists.Include(d => d.Availability).ThenInclude(w => w.Branch).SingleAsync(d => d.Email == Ayse);
        return ayse.Availability.SingleOrDefault(w => w.Day == DayOfWeek.Saturday && w.Branch.Name == "Serdivan Şubesi")?.Id;
    }
}

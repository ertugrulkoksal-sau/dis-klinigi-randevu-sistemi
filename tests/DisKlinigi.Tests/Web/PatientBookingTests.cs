using System.Net;
using System.Text.RegularExpressions;
using DisKlinigi.Core.Domain;
using DisKlinigi.Core.Domain.Appointments;
using DisKlinigi.Core.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace DisKlinigi.Tests.Web;

/// <summary>
/// Hasta ekranları uygulamanın kendisi üzerinden: seçimlerin daralması, boş saatten randevu alma,
/// arada dolan saatin reddedilmesi ve iptal. Her test kendi hastasını kaydediyor; örnek verideki
/// hastaların randevularına dokunmuyor. Uygulama gerçek saatle çalıştığı için tarihler sabit yazılmıyor,
/// sayfanın gösterdiği gün ve saatlerden okunuyor.
/// </summary>
[Collection(WebCollection.Name)]
public partial class PatientBookingTests
{
    private readonly ClinicWebFactory _factory;

    public PatientBookingTests(ClinicWebFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Hekim_listesi_sadece_bu_subede_bu_hizmeti_veren_hekimleri_gosterir()
    {
        var browser = await NewPatientAsync("Liste Hasta");
        var canal = await IdsAsync("Serdivan Şubesi", "Kanal tedavisi");

        var page = await PageTextAsync(browser, $"/Patient/Book?branchId={canal.Branch}&departmentId={canal.Department}&serviceId={canal.Service}");

        Assert.Contains("Serdivan Şubesi'nde kanal tedavisi yapan hekimler", page);
        Assert.Contains("Dt. Ayşe Kaya", page);
        Assert.DoesNotContain("Dt. Mehmet Demir", page); // Serdivan'da çalışıyor ama kanal tedavisi vermiyor
        Assert.DoesNotContain("Dt. Selim Kurt", page);   // kanal tedavisi veriyor ama Kadıköy'de
    }

    [Fact]
    public async Task Hizmeti_veren_hekim_yoksa_bos_durum_metni_cikar()
    {
        var browser = await NewPatientAsync("Bos Durum Hasta");
        var cleaning = await IdsAsync("Kadıköy Şubesi", "Diş taşı temizliği");

        var page = await PageTextAsync(browser, $"/Patient/Book?branchId={cleaning.Branch}&departmentId={cleaning.Department}&serviceId={cleaning.Service}");

        Assert.Contains("Bu hizmeti Kadıköy Şubesi'nde şu an veren hekim yok.", page);
    }

    [Fact]
    public async Task Bos_saatten_randevu_alinir_alinan_saat_listeden_cikar_ve_ayni_saate_ikinci_kayit_reddedilir()
    {
        var first = await NewPatientAsync("Birinci Hasta");
        var second = await NewPatientAsync("Ikinci Hasta");
        var examination = await IdsAsync("Serdivan Şubesi", "Muayene");
        var mehmet = await DentistIdAsync("Dt. Mehmet Demir");
        var scheduleUrl = $"/Patient/Schedule?branchId={examination.Branch}&serviceId={examination.Service}&dentistId={mehmet}";

        // Boş saati olan ilk gün ve o günün son boş saati (bugün akşamsa bugünün saatleri bitmiş olabilir).
        var (date, slot) = await FirstFreeSlotAsync(first, scheduleUrl);
        var form = BookingForm(examination.Branch, examination.Service, mehmet, date, slot, "Diş eti kanaması var.");

        var booked = await first.PostFormAsync(scheduleUrl, "/Patient/Schedule", form);

        Assert.Equal("/Patient/Appointments", booked.RedirectTarget());
        var list = await first.GetStringAsync("/Patient/Appointments");
        Assert.Contains("Randevunuz oluşturuldu.", list);
        Assert.Contains("Muayene otomatik onaylandı", list);

        await using (var db = _factory.CreateDbContext())
        {
            var saved = await db.Appointments.Include(a => a.Patient).SingleAsync(a => a.Patient.FullName == "Birinci Hasta");
            Assert.Equal(AppointmentStatus.Confirmed, saved.Status);
            Assert.Equal("Diş eti kanaması var.", saved.PatientNote);
            Assert.Equal($"{date} {slot.Replace("%3A", ":")}", saved.Period.Start.ToString("yyyy-MM-dd HH:mm"));
        }

        // Başka bir hasta aynı günü açınca bu saat listede yok (hekim dolu).
        var secondPage = await second.GetStringAsync($"{scheduleUrl}&date={date}");
        Assert.DoesNotContain($"start={slot}\"", secondPage);

        // Eski sayfadan aynı saati göndermek: kurallar kayıtta yeniden çalışıyor, sayfa hatayla geri geliyor.
        var stale = await second.PostFormAsync(scheduleUrl, "/Patient/Schedule",
            BookingForm(examination.Branch, examination.Service, mehmet, date, slot, null));
        Assert.Equal(HttpStatusCode.OK, stale.StatusCode);
        var html = await stale.Content.ReadAsStringAsync();
        Assert.Contains("Dt. Mehmet Demir bu saat aralığında başka bir randevuda.", html);
        Assert.Contains("Lütfen listeden başka bir saat seçin.", html);

        await using (var db = _factory.CreateDbContext())
            Assert.False(await db.Appointments.AnyAsync(a => a.Patient.FullName == "Ikinci Hasta"));
    }

    [Fact]
    public async Task Hasta_24_saatten_fazla_kalan_randevuyu_iptal_eder_daha_yakini_edemez()
    {
        var browser = await NewPatientAsync("Iptal Hasta");
        var now = DateTime.Now;
        var far = await AddAppointmentAsync("Iptal Hasta", now.AddDays(3));
        var near = await AddAppointmentAsync("Iptal Hasta", now.AddHours(2));

        var list = await browser.GetStringAsync("/Patient/Appointments");
        Assert.Contains($"/Patient/Appointments?cancel={far}#randevu-{far}", list);
        Assert.DoesNotContain($"cancel={near}", list);
        Assert.Contains("24 saatten az kaldı; iptal için şubeyi arayın.", list);

        // Onay adımı açılmadan iptal butonu yok; açılınca "Evet, iptal et" formu çıkıyor.
        var confirm = await browser.GetStringAsync($"/Patient/Appointments?cancel={far}");
        Assert.Contains("Bu randevuyu iptal etmek istiyor musunuz?", confirm);

        var cancelFar = await browser.PostFormAsync($"/Patient/Appointments?cancel={far}", $"/Patient/Cancel/{far}", new Dictionary<string, string>());
        var cancelNear = await browser.PostFormAsync($"/Patient/Appointments?cancel={far}", $"/Patient/Cancel/{near}", new Dictionary<string, string>());

        Assert.Equal("/Patient/Appointments", cancelFar.RedirectTarget());
        Assert.Equal("/Patient/Appointments", cancelNear.RedirectTarget());
        Assert.Contains("24 saatten az kaldığı için iptal sadece şube tarafından yapılabilir", await browser.GetStringAsync("/Patient/Appointments"));
        Assert.Equal(AppointmentStatus.Cancelled, await StatusAsync(far));
        Assert.Equal(AppointmentStatus.Confirmed, await StatusAsync(near));
    }

    [Fact]
    public async Task Hasta_baskasinin_randevusunu_iptal_edemez_ve_varligini_ogrenemez()
    {
        await NewPatientAsync("Sahibi Hasta");
        var other = await NewPatientAsync("Meraklı Hasta");
        var id = await AddAppointmentAsync("Sahibi Hasta", DateTime.Now.AddDays(5));

        await other.PostFormAsync("/Patient/Appointments", $"/Patient/Cancel/{id}", new Dictionary<string, string>());

        Assert.Contains("Randevu bulunamadı.", await other.GetStringAsync("/Patient/Appointments"));
        Assert.Equal(AppointmentStatus.Confirmed, await StatusAsync(id));
    }

    [Fact]
    public async Task Randevusu_olmayan_hasta_bos_durum_metnini_gorur()
    {
        var browser = await NewPatientAsync("Yeni Hasta");

        var page = await browser.GetStringAsync("/Patient/Appointments");

        Assert.Contains("Henüz randevunuz yok.", page);
    }

    // ---- Yardımcılar ----

    [GeneratedRegex("date=([0-9]{4}-[0-9]{2}-[0-9]{2})\"")]
    private static partial Regex DayLink();

    private static async Task<(string Date, string Slot)> FirstFreeSlotAsync(HttpClient browser, string scheduleUrl)
    {
        var days = DayLink().Matches(await browser.GetStringAsync(scheduleUrl)).Select(m => m.Groups[1].Value).Distinct();
        foreach (var day in days)
        {
            var slots = SlotParameter().Matches(await browser.GetStringAsync($"{scheduleUrl}&date={day}"));
            if (slots.Count > 0)
                return (day, slots[^1].Groups[1].Value);
        }
        throw new InvalidOperationException("İki hafta içinde boş saat bulunamadı.");
    }

    [GeneratedRegex("start=([0-9]{2}%3A[0-9]{2})\"")]
    private static partial Regex SlotParameter();

    /// <summary>Sayfa HTML'i, "Şubesi&amp;#x27;nde" gibi kodlanmış karakterler çözülmüş olarak.</summary>
    private static async Task<string> PageTextAsync(HttpClient browser, string url) =>
        WebUtility.HtmlDecode(await browser.GetStringAsync(url));

    private async Task<HttpClient> NewPatientAsync(string fullName)
    {
        var browser = _factory.CreateBrowser();
        var email = $"{Guid.NewGuid():N}@ornek.test";
        var response = await browser.PostFormAsync("/Account/Login", "/Account/Register", new Dictionary<string, string>
        {
            ["Register.FullName"] = fullName,
            ["Register.Email"] = email,
            ["Register.Password"] = "Hasta-Parola-1",
            ["Register.ConfirmPassword"] = "Hasta-Parola-1"
        });
        Assert.Equal("/Patient/Book", response.RedirectTarget());
        return browser;
    }

    private async Task<(int Branch, int Service, int Department)> IdsAsync(string branchName, string serviceName)
    {
        await using var db = _factory.CreateDbContext();
        var branch = await db.Branches.SingleAsync(b => b.Name == branchName);
        var service = await db.Services.Include(s => s.Department).SingleAsync(s => s.Name == serviceName);
        return (branch.Id, service.Id, service.Department.Id);
    }

    private async Task<int> DentistIdAsync(string fullName)
    {
        await using var db = _factory.CreateDbContext();
        return (await db.Dentists.SingleAsync(d => d.FullName == fullName)).Id;
    }

    /// <summary>
    /// Hastaya, kurallardan geçirmeden, Serdivan'da Dt. Mehmet Demir'den muayene randevusu yazar.
    /// İptal testi kuralları değil 24 saat sınırını sınadığı için saatin müsaitliğe uyması gerekmiyor.
    /// </summary>
    private async Task<int> AddAppointmentAsync(string patientName, DateTime start)
    {
        await using var db = _factory.CreateDbContext();
        var patient = (Patient)await db.Users.SingleAsync(u => u.FullName == patientName);
        var dentist = await db.Dentists.SingleAsync(d => d.FullName == "Dt. Mehmet Demir");
        var branch = await db.Branches.SingleAsync(b => b.Name == "Serdivan Şubesi");
        var service = await db.Services.SingleAsync(s => s.Name == "Muayene");
        var appointment = new Appointment(patient, dentist, service, branch, start, createdAt: DateTime.Now.AddMinutes(-5));
        db.Appointments.Add(appointment);
        await db.SaveChangesAsync();
        return appointment.Id;
    }

    private async Task<AppointmentStatus> StatusAsync(int id)
    {
        await using var db = _factory.CreateDbContext();
        return (await db.Appointments.SingleAsync(a => a.Id == id)).Status;
    }

    private static Dictionary<string, string> BookingForm(int branchId, int serviceId, int dentistId, string date, string slot, string? note) => new()
    {
        ["BranchId"] = branchId.ToString(),
        ["ServiceId"] = serviceId.ToString(),
        ["DentistId"] = dentistId.ToString(),
        ["Date"] = date,
        ["Start"] = WebUtility.UrlDecode(slot),
        ["Note"] = note ?? ""
    };
}

using DisKlinigi.Core.Booking;
using DisKlinigi.Core.Domain;
using DisKlinigi.Core.Domain.Appointments;
using static DisKlinigi.Tests.Booking.BookingClinic;

namespace DisKlinigi.Tests.Booking;

/// <summary>
/// Planlayıcı: kuralların hepsini birlikte çalıştırma, otomatik onay, kayıt ve boş saat üretimi.
/// </summary>
public class AppointmentSchedulerTests
{
    private readonly BookingClinic _clinic = new();

    // ---- Randevu alma ----

    [Fact]
    public async Task Uygun_istek_kaydedilir()
    {
        var repository = new InMemoryAppointmentRepository();
        var request = _clinic.Request(_clinic.Ali, _clinic.Ayse, _clinic.Examination, _clinic.Serdivan, At(10));

        var result = await _clinic.Scheduler(repository).BookAsync(request);

        Assert.True(result.Succeeded);
        Assert.Empty(result.Violations);
        Assert.Same(result.Appointment, Assert.Single(repository.Saved));
        Assert.Equal(1, repository.CommitCount);
        Assert.Equal(request.Period, result.Appointment.Period);
    }

    [Fact]
    public async Task Kaydedilen_randevuda_hasta_notu_ve_alindigi_an_var()
    {
        // K35: oluşturulma zamanı kuralların kontrol edildiği an, yani TimeProvider'ın saati.
        var request = new BookingRequest(
            _clinic.Ali, _clinic.Ayse, _clinic.CanalTreatment, _clinic.Serdivan, At(10), " Dolgum düştü. ");

        var result = await _clinic.Scheduler(new InMemoryAppointmentRepository()).BookAsync(request);

        Assert.Equal(ClockStart, result.Appointment!.CreatedAt);
        Assert.Equal("Dolgum düştü.", result.Appointment.PatientNote);
    }

    [Fact]
    public void Cok_uzun_not_kurallar_calismadan_istekte_reddedilir()
    {
        Assert.Throws<DomainException>(() => new BookingRequest(
            _clinic.Ali, _clinic.Ayse, _clinic.Examination, _clinic.Serdivan, At(10), new string('x', 501)));
    }

    [Fact]
    public async Task Otomatik_onayli_hizmette_randevu_onaylandi_olarak_kaydedilir()
    {
        var result = await _clinic.Scheduler(new InMemoryAppointmentRepository()).BookAsync(
            _clinic.Request(_clinic.Ali, _clinic.Ayse, _clinic.Examination, _clinic.Serdivan, At(10)));

        Assert.Equal(AppointmentStatus.Confirmed, result.Appointment!.Status);
    }

    [Fact]
    public async Task Onay_gerektiren_hizmette_randevu_onay_bekliyor_olarak_kaydedilir()
    {
        var result = await _clinic.Scheduler(new InMemoryAppointmentRepository()).BookAsync(
            _clinic.Request(_clinic.Ali, _clinic.Ayse, _clinic.CanalTreatment, _clinic.Serdivan, At(10)));

        Assert.Equal(AppointmentStatus.PendingApproval, result.Appointment!.Status);
    }

    [Fact]
    public async Task Cignenen_kurallarin_hepsi_birlikte_ve_sirayla_dondurulur()
    {
        // Mehmet Bey kanal tedavisi yapmıyor, Kadıköy'de çalışmıyor ve şube 08:00'de kapalı.
        // Şube Endodonti'yi barındırıyor ve çakışma yok; bu kurallar mesaj üretmemeli.
        var repository = new InMemoryAppointmentRepository();
        var request = _clinic.Request(_clinic.Ali, _clinic.Mehmet, _clinic.CanalTreatment, _clinic.Kadikoy, At(8));

        var result = await _clinic.Scheduler(repository).BookAsync(request);

        Assert.False(result.Succeeded);
        Assert.Null(result.Appointment);
        Assert.Collection(result.Violations,
            m => Assert.Contains("hizmetini vermiyor", m),
            m => Assert.Contains("çalışma saatine", m),
            m => Assert.Contains("bu şubede çalışmıyor", m));
    }

    [Fact]
    public async Task Kural_cignenirse_hicbir_sey_kaydedilmez()
    {
        var repository = new InMemoryAppointmentRepository();
        var past = _clinic.Request(_clinic.Ali, _clinic.Ayse, _clinic.Examination, _clinic.Serdivan, At(10));
        _clinic.SetNow(At(11));

        var result = await _clinic.Scheduler(repository).BookAsync(past);

        Assert.Equal(["Geçmiş bir saate randevu alınamaz."], result.Violations);
        Assert.Empty(repository.Saved);
        Assert.Equal(0, repository.CommitCount);
    }

    [Fact]
    public async Task Kaydedilen_randevu_ayni_saate_ikinci_randevuyu_engeller()
    {
        var scheduler = _clinic.Scheduler(new InMemoryAppointmentRepository());
        await scheduler.BookAsync(_clinic.Request(_clinic.Ali, _clinic.Ayse, _clinic.CanalTreatment, _clinic.Serdivan, At(10)));

        var second = await scheduler.BookAsync(
            _clinic.Request(_clinic.Zeynep, _clinic.Ayse, _clinic.Examination, _clinic.Serdivan, At(10, 30)));

        Assert.False(second.Succeeded);
        Assert.Equal(["Dt. Ayşe Kaya bu saat aralığında başka bir randevuda."], second.Violations);
    }

    [Fact]
    public async Task Iptal_edilen_randevunun_saatine_yeniden_randevu_alinabilir()
    {
        var scheduler = _clinic.Scheduler(new InMemoryAppointmentRepository());
        var first = await scheduler.BookAsync(
            _clinic.Request(_clinic.Ali, _clinic.Ayse, _clinic.CanalTreatment, _clinic.Serdivan, At(10)));
        first.Appointment!.Cancel(_clinic.Ali, _clinic.Now);

        var second = await scheduler.BookAsync(
            _clinic.Request(_clinic.Zeynep, _clinic.Ayse, _clinic.CanalTreatment, _clinic.Serdivan, At(10)));

        Assert.True(second.Succeeded);
    }

    // ---- Eş zamanlı istekler (K19) ----

    [Fact]
    public async Task Ayni_saate_ayni_anda_gelen_iki_istekten_sadece_biri_kaydedilir()
    {
        var repository = new InMemoryAppointmentRepository();
        var scheduler = _clinic.Scheduler(repository);

        var results = await Task.WhenAll(
            Task.Run(() => scheduler.BookAsync(_clinic.Request(_clinic.Ali, _clinic.Ayse, _clinic.CanalTreatment, _clinic.Serdivan, At(10)))),
            Task.Run(() => scheduler.BookAsync(_clinic.Request(_clinic.Zeynep, _clinic.Ayse, _clinic.CanalTreatment, _clinic.Serdivan, At(10)))));

        Assert.Single(results, r => r.Succeeded);
        var loser = Assert.Single(results, r => !r.Succeeded);
        Assert.Contains("bu saat aralığında başka bir randevuda", Assert.Single(loser.Violations));
        Assert.Single(repository.Saved);
    }

    [Fact]
    public async Task Son_unit_icin_ayni_anda_gelen_iki_istekten_sadece_biri_kaydedilir()
    {
        // Serdivan 2 ünitli, biri Mehmet Bey'de dolu. Ayşe Hanım ve Burak Bey farklı hastalar için
        // aynı anda son üniti istiyor; hekim ve hasta kuralları ikisine de izin veriyor,
        // aralarındaki yarışı sadece işlem kilidi çözüyor.
        var repository = new InMemoryAppointmentRepository(
            Existing(_clinic.Can, _clinic.Mehmet, _clinic.Cleaning, _clinic.Serdivan, At(10)));
        var scheduler = _clinic.Scheduler(repository);

        var results = await Task.WhenAll(
            Task.Run(() => scheduler.BookAsync(_clinic.Request(_clinic.Ali, _clinic.Ayse, _clinic.Examination, _clinic.Serdivan, At(10)))),
            Task.Run(() => scheduler.BookAsync(_clinic.Request(_clinic.Zeynep, _clinic.Burak, _clinic.Examination, _clinic.Serdivan, At(10)))));

        Assert.Single(results, r => r.Succeeded);
        var loser = Assert.Single(results, r => !r.Succeeded);
        Assert.Contains("ünitin hepsi", Assert.Single(loser.Violations));
        Assert.Equal(2, repository.Saved.Count);
    }

    // ---- Boş saatler ----

    private static IEnumerable<string> Starts(IReadOnlyList<Core.Domain.TimeRange> slots) =>
        slots.Select(s => s.Start.ToString("HH:mm"));

    [Fact]
    public async Task Bos_saatler_dolu_randevunun_cevresinde_ceyrek_saatlik_adimlarla_uretilir()
    {
        // Ayşe Hanım Serdivan'da 09:00–13:00; 10:00–11:00 dolu. 20 dakikalık muayene için
        // 09:45 dolu randevuya taşıyor, 12:45 ise aralığın sonuna sığmıyor.
        var repository = new InMemoryAppointmentRepository(
            Existing(_clinic.Zeynep, _clinic.Ayse, _clinic.CanalTreatment, _clinic.Serdivan, At(10)));

        var slots = await _clinic.Scheduler(repository).GetFreeSlotsAsync(
            _clinic.Ali, _clinic.Ayse, _clinic.Examination, _clinic.Serdivan, Tuesday);

        Assert.Equal(
            ["09:00", "09:15", "09:30", "11:00", "11:15", "11:30", "11:45", "12:00", "12:15", "12:30"],
            Starts(slots));
        Assert.All(slots, s => Assert.Equal(TimeSpan.FromMinutes(20), s.Duration));
    }

    [Fact]
    public async Task Gecmis_saatler_bos_saat_listesinde_yer_almaz()
    {
        _clinic.SetNow(At(11, 10));

        var slots = await _clinic.Scheduler(new InMemoryAppointmentRepository()).GetFreeSlotsAsync(
            _clinic.Ali, _clinic.Ayse, _clinic.Examination, _clinic.Serdivan, Tuesday);

        Assert.Equal("11:15", Starts(slots).First());
    }

    [Fact]
    public async Task Hastanin_kendi_baska_randevusuyla_cakisan_saat_listede_yer_almaz()
    {
        // Ali'nin 12:00–12:20'de Mehmet Bey'le muayenesi var.
        var repository = new InMemoryAppointmentRepository(
            Existing(_clinic.Ali, _clinic.Mehmet, _clinic.Examination, _clinic.Serdivan, At(12)));

        var slots = await _clinic.Scheduler(repository).GetFreeSlotsAsync(
            _clinic.Ali, _clinic.Ayse, _clinic.Examination, _clinic.Serdivan, Tuesday);

        Assert.DoesNotContain("11:45", Starts(slots));
        Assert.DoesNotContain("12:00", Starts(slots));
        Assert.DoesNotContain("12:15", Starts(slots));
        Assert.Contains("11:30", Starts(slots));
        Assert.Contains("12:30", Starts(slots));
    }

    [Fact]
    public async Task Uniteler_doluysa_bos_hekime_de_saat_gosterilmez()
    {
        // 09:00–09:30 arasında iki ünit de Mehmet ve Burak Bey'lerde. Ayşe Hanım boş olsa da
        // 09:00 ve 09:15 muayeneleri bu aralığa denk geliyor.
        var repository = new InMemoryAppointmentRepository(
            Existing(_clinic.Zeynep, _clinic.Mehmet, _clinic.Cleaning, _clinic.Serdivan, At(9)),
            Existing(_clinic.Can, _clinic.Burak, _clinic.Cleaning, _clinic.Serdivan, At(9)));

        var slots = await _clinic.Scheduler(repository).GetFreeSlotsAsync(
            _clinic.Ali, _clinic.Ayse, _clinic.Examination, _clinic.Serdivan, Tuesday);

        Assert.Equal("09:30", Starts(slots).First());
    }

    [Fact]
    public async Task Hekimin_o_gun_o_subede_araligi_yoksa_liste_bos()
    {
        var slots = await _clinic.Scheduler(new InMemoryAppointmentRepository()).GetFreeSlotsAsync(
            _clinic.Ali, _clinic.Mehmet, _clinic.Examination, _clinic.Kadikoy, Tuesday);

        Assert.Empty(slots);
    }

    [Fact]
    public async Task Bos_saatler_sadece_secilen_subedeki_araliktan_uretilir()
    {
        // Ayşe Hanım Salı öğleden sonra Kadıköy'de: 14:00–18:00, 60 dakikalık kanal için 14:00–17:00.
        var slots = await _clinic.Scheduler(new InMemoryAppointmentRepository()).GetFreeSlotsAsync(
            _clinic.Ali, _clinic.Ayse, _clinic.CanalTreatment, _clinic.Kadikoy, Tuesday);

        Assert.Equal("14:00", Starts(slots).First());
        Assert.Equal("17:00", Starts(slots).Last());
        Assert.Equal(13, slots.Count);
    }

    [Fact]
    public async Task Listedeki_saat_alinabiliyor_listede_olmayan_alinamiyor()
    {
        // "Ekranda boş görünen saat" ile "kaydederken kabul edilen saat" aynı koddan çıkıyor.
        var repository = new InMemoryAppointmentRepository(
            Existing(_clinic.Zeynep, _clinic.Ayse, _clinic.CanalTreatment, _clinic.Serdivan, At(10)));
        var scheduler = _clinic.Scheduler(repository);
        var slots = await scheduler.GetFreeSlotsAsync(_clinic.Ali, _clinic.Ayse, _clinic.Examination, _clinic.Serdivan, Tuesday);

        var listed = await scheduler.BookAsync(
            _clinic.Request(_clinic.Ali, _clinic.Ayse, _clinic.Examination, _clinic.Serdivan, slots[0].Start));
        var notListed = await scheduler.BookAsync(
            _clinic.Request(_clinic.Ali, _clinic.Ayse, _clinic.Examination, _clinic.Serdivan, At(9, 45)));

        Assert.True(listed.Succeeded);
        Assert.False(notListed.Succeeded);

        var after = await scheduler.GetFreeSlotsAsync(_clinic.Ali, _clinic.Ayse, _clinic.Examination, _clinic.Serdivan, Tuesday);
        Assert.DoesNotContain(after, s => s.Start == slots[0].Start);
    }
}

using DisKlinigi.Core.Availability;
using DisKlinigi.Core.Domain;
using DisKlinigi.Core.Domain.Appointments;
using DisKlinigi.Core.Domain.Users;
using DisKlinigi.Tests.Booking;
using DisKlinigi.Tests.Domain.Appointments;

namespace DisKlinigi.Tests.Availability;

/// <summary>
/// Hekimin müsaitlik aralıkları (ekran 4c). Ekleme kuralları hekim sınıfında (K24) ve orada ayrıca
/// test ediliyor; burada servisin onları ekrana doğru taşıdığı ve silmedeki yeni kural sınanıyor:
/// ileri tarihte aktif randevusu olan aralık silinemez (K52). Saat 8 Kasım 09:00, randevular 10 Kasım Salı.
/// </summary>
public class AvailabilityServiceTests
{
    private readonly TestClinic _clinic = new();
    private readonly AvailabilityWindow _tuesday;
    private readonly InMemoryDentistRepository _dentists;

    public AvailabilityServiceTests()
    {
        // Ekran şubeyi Id'siyle gönderiyor; kaydedilmemiş iki şubenin Id'si 0 olurdu.
        var id = typeof(Entity).GetProperty(nameof(Entity.Id))!;
        id.SetValue(_clinic.Serdivan, 1);
        id.SetValue(_clinic.Kadikoy, 2);
        _clinic.Serdivan.SetWorkingHours(DayOfWeek.Tuesday, TimeOfDayRange.Of(9, 0, 18, 0));
        _clinic.Serdivan.SetWorkingHours(DayOfWeek.Saturday, TimeOfDayRange.Of(9, 0, 14, 0));
        _tuesday = _clinic.Ayse.AddAvailability(_clinic.Serdivan, DayOfWeek.Tuesday, TimeOfDayRange.Of(9, 0, 13, 0));
        _dentists = new InMemoryDentistRepository(_clinic.Ayse, _clinic.Mehmet);
    }

    private AvailabilityService Service(params Appointment[] appointments) =>
        new(_dentists, new InMemoryAppointmentRepository(appointments), _clinic.Clock);

    // ---- Silme ----

    [Fact]
    public async Task Ileri_tarihte_randevusu_olan_aralik_silinemez()
    {
        var service = Service(_clinic.Book(_clinic.CanalTreatment));

        var result = await service.RemoveAsync(_clinic.Ayse, _tuesday.Id);

        Assert.False(result.Succeeded);
        Assert.Equal("Salı 09:00–13:00 aralığında (Serdivan Şubesi) ileri tarihte 1 randevu var; aralık silinmeden önce bu randevuların iptal edilmesi gerekiyor.", result.Error);
        Assert.Contains(_tuesday, _clinic.Ayse.Availability);
        Assert.Equal(0, _dentists.SaveCount);
    }

    [Fact]
    public async Task Iptal_edilmis_randevu_silmeyi_engellemez()
    {
        var cancelled = _clinic.Book(_clinic.Examination);
        cancelled.Cancel(_clinic.Ayse, _clinic.Now);

        var result = await Service(cancelled).RemoveAsync(_clinic.Ayse, _tuesday.Id);

        Assert.True(result.Succeeded);
        Assert.Empty(_clinic.Ayse.Availability);
        Assert.Equal(1, _dentists.SaveCount);
    }

    [Fact]
    public async Task Baslamis_randevu_silmeyi_engellemez()
    {
        // "İleri tarih" = henüz başlamamış. Başlamış randevu aralık silinse de geçmişte kalıyor.
        var service = Service(_clinic.Book(_clinic.Examination));
        _clinic.SetNow(TestClinic.AppointmentStart);

        Assert.True((await service.RemoveAsync(_clinic.Ayse, _tuesday.Id)).Succeeded);
    }

    [Fact]
    public async Task Baska_subedeki_ayni_gun_randevusu_bu_araligi_engellemez()
    {
        // Aynı gün Kadıköy'de öğleden sonra çalışıyor; o randevu Serdivan aralığına düşmüyor.
        _clinic.Kadikoy.SetWorkingHours(DayOfWeek.Tuesday, TimeOfDayRange.Of(9, 0, 18, 0));
        _clinic.Ayse.AssignToBranch(_clinic.Kadikoy);
        _clinic.Ayse.AddAvailability(_clinic.Kadikoy, DayOfWeek.Tuesday, TimeOfDayRange.Of(14, 0, 18, 0));
        var kadikoy = new Appointment(_clinic.Ali, _clinic.Ayse, _clinic.Examination, _clinic.Kadikoy,
            TestClinic.AppointmentStart.AddHours(4), createdAt: TestClinic.ClockStart);

        var result = await Service(kadikoy).RemoveAsync(_clinic.Ayse, _tuesday.Id);

        Assert.True(result.Succeeded);
        Assert.Single(_clinic.Ayse.Availability);
    }

    [Fact]
    public async Task Baska_hekimin_araligi_bulunamaz()
    {
        var result = await Service().RemoveAsync(_clinic.Mehmet, _tuesday.Id);

        Assert.True(result.NotFound);
        Assert.Contains(_tuesday, _clinic.Ayse.Availability);
    }

    [Fact]
    public async Task Aralik_listesi_randevu_sayisini_ve_silinebilirligi_verir()
    {
        _clinic.Ayse.AddAvailability(_clinic.Serdivan, DayOfWeek.Saturday, TimeOfDayRange.Of(9, 0, 12, 0));
        var later = new Appointment(_clinic.Zeynep, _clinic.Ayse, _clinic.Examination, _clinic.Serdivan,
            TestClinic.AppointmentStart.AddHours(1), createdAt: TestClinic.ClockStart);

        var usage = await Service(_clinic.Book(_clinic.Examination), later).GetUsageAsync(_clinic.Ayse);

        Assert.Collection(usage,
            tuesday => { Assert.Equal(DayOfWeek.Tuesday, tuesday.Window.Day); Assert.Equal(2, tuesday.UpcomingAppointments.Count); Assert.False(tuesday.CanRemove); },
            saturday => { Assert.Equal(DayOfWeek.Saturday, saturday.Window.Day); Assert.True(saturday.CanRemove); });
    }

    // ---- Ekleme ----

    [Fact]
    public async Task Gecerli_aralik_eklenir_ve_kaydedilir()
    {
        var result = await Service().AddAsync(_clinic.Ayse, _clinic.Serdivan.Id, DayOfWeek.Saturday, new(9, 0), new(12, 0));

        Assert.True(result.Succeeded);
        Assert.Equal(2, _clinic.Ayse.Availability.Count);
        Assert.Equal(1, _dentists.SaveCount);
    }

    [Theory]
    [InlineData(DayOfWeek.Saturday, 10, 16, "10:00–16:00 aralığı, Serdivan Şubesi Cumartesi çalışma saatine (09:00–14:00) sığmıyor.")]
    [InlineData(DayOfWeek.Tuesday, 12, 15, "12:00–15:00 aralığı, tanımlı Salı 09:00–13:00, Serdivan Şubesi aralığıyla çakışıyor.")]
    [InlineData(DayOfWeek.Sunday, 10, 12, "Serdivan Şubesi Pazar günü kapalı.")]
    [InlineData(DayOfWeek.Saturday, 12, 10, "Bitiş saati başlangıç saatinden sonra olmalıdır.")]
    public async Task Kurallara_uymayan_aralik_alan_sinifinin_mesajiyla_reddedilir(DayOfWeek day, int start, int end, string error)
    {
        var result = await Service().AddAsync(_clinic.Ayse, _clinic.Serdivan.Id, day, new(start, 0), new(end, 0));

        Assert.Equal(error, result.Error);
        Assert.Single(_clinic.Ayse.Availability);
        Assert.Equal(0, _dentists.SaveCount);
    }

    [Fact]
    public async Task Hekimin_calismadigi_subeye_aralik_eklenemez()
    {
        var result = await Service().AddAsync(_clinic.Ayse, _clinic.Kadikoy.Id, DayOfWeek.Saturday, new(9, 0), new(12, 0));

        Assert.Equal("Listeden çalıştığınız şubelerden birini seçin.", result.Error);
    }
}

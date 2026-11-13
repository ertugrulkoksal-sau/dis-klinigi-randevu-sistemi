using DisKlinigi.Core.Booking;
using DisKlinigi.Core.Domain.Appointments;
using DisKlinigi.Tests.Booking;
using DisKlinigi.Web.Data;
using DisKlinigi.Web.Data.Repositories;
using Microsoft.EntityFrameworkCore;
using static DisKlinigi.Tests.Booking.BookingClinic;

namespace DisKlinigi.Tests.Data;

/// <summary>
/// Randevu repository'si SQLite üzerinde: günün aktif randevuları sorgusu, randevu alma işlemi
/// ve iki ayrı bağlantıdan aynı anda gelen isteklerde yazma kilidi (K19).
/// </summary>
public class EfAppointmentRepositoryTests : IAsyncLifetime
{
    private readonly BookingClinic _clinic = new();
    private SqliteTestDatabase _db = null!;

    public async Task InitializeAsync()
    {
        _db = await SqliteTestDatabase.CreateAsync();
        await _db.SaveAsync(_clinic);
    }

    public async Task DisposeAsync() => await _db.DisposeAsync();

    private async Task SaveAppointmentsAsync(params Appointment[] appointments)
    {
        await using var context = _db.CreateContext();
        context.AttachRange(_clinic.Serdivan, _clinic.Kadikoy, _clinic.Ayse, _clinic.Mehmet, _clinic.Burak,
            _clinic.Ali, _clinic.Zeynep, _clinic.Can, _clinic.Deniz, _clinic.Admin);
        context.Appointments.AddRange(appointments);
        await context.SaveChangesAsync();
    }

    private AppointmentScheduler Scheduler(AppDbContext context) =>
        new(new EfAppointmentRepository(context), BookingRules.CreateDefault(), _clinic.Clock);

    // ---- Günün aktif randevuları ----

    [Fact]
    public async Task Gunun_aktif_randevulari_sadece_ilgili_hekim_hasta_ya_da_subeden_gelir()
    {
        var c = _clinic;
        var monday = new DateTime(2026, 11, 9, 0, 0, 0);
        var cancelled = Existing(c.Zeynep, c.Ayse, c.Examination, c.Serdivan, At(12));

        await SaveAppointmentsAsync(
            Existing(c.Ali, c.Ayse, c.Examination, c.Kadikoy, At(15)),          // hekim aynı, şube farklı
            Existing(c.Ali, c.Mehmet, c.Examination, c.Serdivan, At(16)),       // hasta aynı
            Existing(c.Can, c.Burak, c.Cleaning, c.Serdivan, At(10)),           // sadece şube aynı
            Existing(c.Deniz, c.Mehmet, c.Examination, c.Kadikoy, At(11)),      // hiçbiriyle ilgisi yok
            Existing(c.Zeynep, c.Ayse, c.Examination, c.Serdivan, At(9).AddDays(1)), // ertesi gün
            Existing(c.Zeynep, c.Ayse, c.Examination, c.Serdivan, monday.AddHours(23).AddMinutes(40)), // önceki gün, gece yarısı biter
            cancelled);

        await using (var context = _db.CreateContext())
        {
            var loaded = await context.Appointments.Include(a => a.Patient).SingleAsync(a => a.Id == cancelled.Id);
            loaded.Cancel(loaded.Patient, _clinic.Now);
            await context.SaveChangesAsync();
        }

        await using (var context = _db.CreateContext())
        {
            var clinic = await LoadedClinic.LoadAsync(context, c);
            var result = await new EfAppointmentRepository(context)
                .GetActiveOnDateAsync(clinic.Ayse, clinic.Ali, clinic.Serdivan, Tuesday);

            Assert.Equal(
                [At(10), At(15), At(16)],
                result.Select(a => a.Period.Start).OrderBy(s => s));

            // Kurallar diğer randevunun hekimine, hastasına ve şubesine bakıyor; hepsi yüklü olmalı.
            Assert.All(result, a =>
            {
                Assert.NotNull(a.Dentist);
                Assert.NotNull(a.Patient);
                Assert.NotNull(a.Branch.Name);
            });
        }
    }

    [Fact]
    public async Task Gece_yarisinda_biten_onceki_gun_randevusu_bu_gune_girmez_yari_acik_aralik()
    {
        // 23:40–00:00 randevusu Salı 00:00'da bitiyor; [başlangıç, bitiş) olduğu için Salı'ya dahil değil.
        var c = _clinic;
        await SaveAppointmentsAsync(Existing(c.Ali, c.Ayse, c.Examination, c.Serdivan, Tuesday.ToDateTime(TimeOnly.MinValue).AddMinutes(-20)));

        await using var context = _db.CreateContext();
        var clinic = await LoadedClinic.LoadAsync(context, c);
        var result = await new EfAppointmentRepository(context).GetActiveOnDateAsync(clinic.Ayse, clinic.Ali, clinic.Serdivan, Tuesday);

        Assert.Empty(result);
    }

    // ---- Personel paneli sorguları (13 Kasım) ----

    [Fact]
    public async Task Bekleyenler_ve_hekimin_gunu_dogru_randevulari_tarih_sirasiyla_getirir()
    {
        var c = _clinic;
        var pendingLater = Existing(c.Zeynep, c.Ayse, c.CanalTreatment, c.Serdivan, At(11));
        var pendingEarlier = Existing(c.Can, c.Ayse, c.CanalTreatment, c.Serdivan, At(9).AddDays(-7));
        var kadikoy = Existing(c.Ali, c.Ayse, c.Examination, c.Kadikoy, At(15));
        var otherDentist = Existing(c.Deniz, c.Mehmet, c.Examination, c.Serdivan, At(10));
        var nextDay = Existing(c.Ali, c.Ayse, c.Examination, c.Serdivan, At(9).AddDays(1));
        await SaveAppointmentsAsync(pendingLater, kadikoy, otherDentist, nextDay, pendingEarlier);

        await using var context = _db.CreateContext();
        var clinic = await LoadedClinic.LoadAsync(context, c);
        var repository = new EfAppointmentRepository(context);

        var pending = await repository.GetPendingAsync();
        var day = await repository.GetForDentistOnDateAsync(clinic.Ayse, Tuesday);
        var upcoming = await repository.GetUpcomingForDentistAsync(clinic.Ayse, At(12));

        Assert.Equal([pendingEarlier.Id, pendingLater.Id], pending.Select(a => a.Id));
        Assert.All(pending, a => Assert.NotNull(a.Patient.FullName));
        Assert.Equal([pendingLater.Id, kadikoy.Id], day.Select(a => a.Id));
        Assert.Equal([kadikoy.Id, nextDay.Id], upcoming.Select(a => a.Id));
    }

    // ---- Planlayıcı + EF Core ----

    [Fact]
    public async Task Planlayici_ile_alinan_randevu_veritabanina_kaydedilir()
    {
        int id;
        await using (var context = _db.CreateContext())
        {
            var clinic = await LoadedClinic.LoadAsync(context, _clinic);
            var result = await Scheduler(context).BookAsync(new BookingRequest(
                clinic.Ali, clinic.Ayse, clinic.CanalTreatment, clinic.Serdivan, At(10), "Dolgum düştü."));

            Assert.True(result.Succeeded);
            id = result.Appointment.Id;
        }

        await using (var context = _db.CreateContext())
        {
            var saved = await context.Appointments.SingleAsync();
            Assert.Equal(id, saved.Id);
            Assert.Equal(AppointmentStatus.PendingApproval, saved.Status);
            Assert.Equal(ClockStart, saved.CreatedAt);
            Assert.Equal("Dolgum düştü.", saved.PatientNote);
        }
    }

    [Fact]
    public async Task Kurallari_gecmeyen_istek_veritabaninda_iz_birakmaz()
    {
        var c = _clinic;
        await SaveAppointmentsAsync(Existing(c.Zeynep, c.Ayse, c.Examination, c.Serdivan, At(10)));

        await using (var context = _db.CreateContext())
        {
            var clinic = await LoadedClinic.LoadAsync(context, c);
            var result = await Scheduler(context).BookAsync(
                new BookingRequest(clinic.Ali, clinic.Ayse, clinic.Examination, clinic.Serdivan, At(10, 15)));

            Assert.False(result.Succeeded);
            Assert.Contains(result.Violations, m => m.Contains("başka bir randevuda"));

            // Aynı bağlamda sonradan yapılan bir kayıt da reddedilen randevuyu yazmamalı.
            await context.SaveChangesAsync();
        }

        await using (var context = _db.CreateContext())
            Assert.Equal(1, await context.Appointments.CountAsync());
    }

    [Fact]
    public async Task Onaylanmadan_kapatilan_islem_eklenen_randevuyu_geri_alir()
    {
        await using (var context = _db.CreateContext())
        {
            var clinic = await LoadedClinic.LoadAsync(context, _clinic);
            var repository = new EfAppointmentRepository(context);

            await using (await repository.BeginBookingTransactionAsync())
            {
                repository.Add(new Appointment(clinic.Ali, clinic.Ayse, clinic.Examination, clinic.Serdivan, At(10), ClockStart));
            }

            Assert.Empty(context.ChangeTracker.Entries<Appointment>());
            await context.SaveChangesAsync();
        }

        await using (var context = _db.CreateContext())
            Assert.Equal(0, await context.Appointments.CountAsync());
    }

    // ---- Eş zamanlı istekler (K19) ----

    /// <summary>
    /// İlk istek işlemi açıp randevusunu eklemişken ikinci istek aynı hekime aynı saati istiyor.
    /// İkinci istek ilk işlem bitene kadar beklemeli, sonra güncel veriyle "hekim dolu" cevabı almalı.
    /// İşlem BEGIN IMMEDIATE ile açılmasaydı ikinci istek okumayı hemen yapıp "boş" görürdü.
    /// </summary>
    [Fact]
    public async Task Ayni_saate_es_zamanli_ikinci_istek_ilk_islemi_bekler_ve_hekim_dolu_cevabi_alir()
    {
        await using var first = _db.CreateContext();
        await using var second = _db.CreateContext();
        var a = await LoadedClinic.LoadAsync(first, _clinic);
        var b = await LoadedClinic.LoadAsync(second, _clinic);
        var firstRepository = new EfAppointmentRepository(first);

        await using (var transaction = await firstRepository.BeginBookingTransactionAsync())
        {
            firstRepository.Add(new Appointment(a.Ali, a.Ayse, a.Examination, a.Serdivan, At(10), ClockStart));

            var secondBooking = Task.Run(() => Scheduler(second).BookAsync(
                new BookingRequest(b.Zeynep, b.Ayse, b.Examination, b.Serdivan, At(10))));

            await Task.Delay(TimeSpan.FromMilliseconds(500));
            Assert.False(secondBooking.IsCompleted, "İkinci istek ilk işlem açıkken ilerlememeli.");

            await transaction.CommitAsync();
            var result = await secondBooking;

            Assert.False(result.Succeeded);
            Assert.Single(result.Violations, m => m.Contains("Dt. Ayşe Kaya bu saat aralığında başka bir randevuda"));
        }

        await using var check = _db.CreateContext();
        Assert.Equal(1, await check.Appointments.CountAsync());
    }

    /// <summary>
    /// Ünit kapasitesi de aynı korumayla tutuluyor: 2 ünitli Serdivan'da 10:00'da zaten bir randevu
    /// varken iki farklı hekim için aynı anda gelen istekten sadece biri kabul edilmeli.
    /// </summary>
    [Fact]
    public async Task Son_unite_es_zamanli_iki_istekten_sadece_biri_kaydedilir()
    {
        var c = _clinic;
        await SaveAppointmentsAsync(Existing(c.Can, c.Burak, c.Cleaning, c.Serdivan, At(10)));

        await using var first = _db.CreateContext();
        await using var second = _db.CreateContext();
        var a = await LoadedClinic.LoadAsync(first, c);
        var b = await LoadedClinic.LoadAsync(second, c);
        var firstRepository = new EfAppointmentRepository(first);

        await using (var transaction = await firstRepository.BeginBookingTransactionAsync())
        {
            firstRepository.Add(new Appointment(a.Ali, a.Mehmet, a.Examination, a.Serdivan, At(10), ClockStart));

            var secondBooking = Task.Run(() => Scheduler(second).BookAsync(
                new BookingRequest(b.Zeynep, b.Ayse, b.Examination, b.Serdivan, At(10))));

            await Task.Delay(TimeSpan.FromMilliseconds(500));
            Assert.False(secondBooking.IsCompleted);

            await transaction.CommitAsync();
            var result = await secondBooking;

            Assert.False(result.Succeeded);
            Assert.Single(result.Violations, m => m.Contains("ünitin hepsi"));
        }

        await using var check = _db.CreateContext();
        Assert.Equal(2, await check.Appointments.CountAsync());
    }

    [Fact]
    public async Task Ilk_islem_bittikten_sonra_baska_saate_gelen_istek_beklemeden_kaydedilir()
    {
        // Kilit sadece işlem süresince; işlem kapanınca sıradaki istek normal çalışıyor.
        await using (var context = _db.CreateContext())
        {
            var clinic = await LoadedClinic.LoadAsync(context, _clinic);
            Assert.True((await Scheduler(context).BookAsync(
                new BookingRequest(clinic.Ali, clinic.Ayse, clinic.Examination, clinic.Serdivan, At(10)))).Succeeded);
        }

        await using (var context = _db.CreateContext())
        {
            var clinic = await LoadedClinic.LoadAsync(context, _clinic);
            Assert.True((await Scheduler(context).BookAsync(
                new BookingRequest(clinic.Zeynep, clinic.Ayse, clinic.Examination, clinic.Serdivan, At(10, 30)))).Succeeded);
        }
    }
}

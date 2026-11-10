using DisKlinigi.Core.Domain;
using DisKlinigi.Core.Domain.Appointments;
using DisKlinigi.Core.Domain.Users;
using DisKlinigi.Tests.Booking;
using DisKlinigi.Web.Data.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using static DisKlinigi.Tests.Booking.BookingClinic;

namespace DisKlinigi.Tests.Data;

/// <summary>
/// EF Core eşlemeleri: nesne bir bağlamda kaydedilip başka bir bağlamda okununca alan
/// kuralları aynı cevabı veriyor mu? Okuma her seferinde yeni bağlamla yapılıyor ki
/// sonuç EF Core'un bellekteki nesneyi geri vermesinden değil, veritabanından gelsin.
/// </summary>
public class MappingTests : IAsyncLifetime
{
    private readonly BookingClinic _clinic = new();
    private SqliteTestDatabase _db = null!;

    public async Task InitializeAsync()
    {
        _db = await SqliteTestDatabase.CreateAsync();
        await _db.SaveAsync(_clinic);
    }

    public async Task DisposeAsync() => await _db.DisposeAsync();

    // ---- Şube ----

    [Fact]
    public async Task Sube_calisma_saatleri_ve_bolumleriyle_geri_okunur()
    {
        await using var context = _db.CreateContext();
        var serdivan = (await new EfBranchRepository(context).GetByIdAsync(_clinic.Serdivan.Id))!;

        Assert.Equal(2, serdivan.ChairCount);
        Assert.Equal(TimeOfDayRange.Of(9, 0, 18, 0), serdivan.GetWorkingHours(DayOfWeek.Tuesday));
        Assert.Equal(TimeOfDayRange.Of(9, 0, 14, 0), serdivan.GetWorkingHours(DayOfWeek.Saturday));
        Assert.Null(serdivan.GetWorkingHours(DayOfWeek.Sunday));
        Assert.True(serdivan.IsOpenDuring(new TimeRange(At(17, 30), At(18))));
        Assert.False(serdivan.IsOpenDuring(new TimeRange(At(17, 30), At(18, 1))));

        Assert.Equal(3, serdivan.Departments.Count);
        Assert.True(serdivan.HostsDepartment(_clinic.Periodontics));
    }

    [Fact]
    public async Task Sube_saati_degistirilip_kaydedilince_ayni_gune_ikinci_kayit_olusmaz()
    {
        await using (var context = _db.CreateContext())
        {
            var serdivan = (await new EfBranchRepository(context).GetByIdAsync(_clinic.Serdivan.Id))!;
            serdivan.SetWorkingHours(DayOfWeek.Tuesday, TimeOfDayRange.Of(10, 0, 16, 0));
            serdivan.CloseOn(DayOfWeek.Saturday);
            await context.SaveChangesAsync();
        }

        await using (var context = _db.CreateContext())
        {
            var serdivan = (await new EfBranchRepository(context).GetByIdAsync(_clinic.Serdivan.Id))!;
            Assert.Equal(TimeOfDayRange.Of(10, 0, 16, 0), serdivan.GetWorkingHours(DayOfWeek.Tuesday));
            Assert.Null(serdivan.GetWorkingHours(DayOfWeek.Saturday));
            Assert.Single(serdivan.WorkingHours);
        }
    }

    [Fact]
    public async Task Veritabani_da_ayni_gune_iki_calisma_saati_kaydini_kabul_etmez()
    {
        // Alan modeli bunu zaten engelliyor; burada anahtarın (şube, gün) şemada da olduğu sınanıyor.
        await using var context = _db.CreateContext();

        var ex = await Assert.ThrowsAsync<SqliteException>(() => context.Database.ExecuteSqlRawAsync(
            "INSERT INTO BranchWorkingHours (BranchId, Day, Opens, Closes) VALUES ({0}, 'Tuesday', '08:00:00', '12:00:00')",
            _clinic.Serdivan.Id));
        Assert.Contains("UNIQUE", ex.Message);
    }

    // ---- Hekim ----

    [Fact]
    public async Task Hekim_hizmetleri_subeleri_ve_musaitlik_araliklariyla_geri_okunur()
    {
        await using var context = _db.CreateContext();
        var ayse = (await new EfDentistRepository(context).GetByIdAsync(_clinic.Ayse.Id))!;

        Assert.Equal("Endodonti", ayse.Specialty.Name);
        Assert.True(ayse.Offers(_clinic.CanalTreatment));
        Assert.True(ayse.WorksAt(_clinic.Kadikoy));
        Assert.Equal(2, ayse.Availability.Count);
        Assert.True(ayse.IsAvailable(_clinic.Serdivan, new TimeRange(At(12), At(13))));
        Assert.False(ayse.IsAvailable(_clinic.Serdivan, new TimeRange(At(14), At(15))));
        Assert.True(ayse.IsAvailable(_clinic.Kadikoy, new TimeRange(At(14), At(15))));

        // Aralığın şubesi, hekimin şube listesindeki nesnenin aynısı ve çalışma saatleri yüklü.
        var window = ayse.AvailabilityAt(_clinic.Kadikoy, DayOfWeek.Tuesday).Single();
        Assert.Same(ayse.Branches.Single(b => b.IsSameAs(_clinic.Kadikoy)), window.Branch);
        Assert.NotNull(window.Branch.GetWorkingHours(DayOfWeek.Tuesday));
    }

    [Fact]
    public async Task Yuklenen_hekime_musaitlik_eklenip_kaydedilebilir_kurallar_yine_calisir()
    {
        await using (var context = _db.CreateContext())
        {
            var ayse = (await new EfDentistRepository(context).GetByIdAsync(_clinic.Ayse.Id))!;
            var serdivan = ayse.Branches.Single(b => b.IsSameAs(_clinic.Serdivan));

            // Şube saatine sığmayan aralık, yüklenmiş nesnede de reddediliyor (şube saatleri yüklü).
            Assert.Throws<DomainException>(() => ayse.AddAvailability(serdivan, DayOfWeek.Saturday, TimeOfDayRange.Of(9, 0, 15, 0)));

            ayse.AddAvailability(serdivan, DayOfWeek.Saturday, TimeOfDayRange.Of(9, 0, 14, 0));
            await context.SaveChangesAsync();
        }

        await using (var context = _db.CreateContext())
        {
            var ayse = (await new EfDentistRepository(context).GetByIdAsync(_clinic.Ayse.Id))!;
            Assert.Equal(3, ayse.Availability.Count);
            Assert.Single(ayse.AvailabilityAt(_clinic.Serdivan, DayOfWeek.Saturday));
        }
    }

    // ---- Kullanıcılar (TPH) ----

    [Fact]
    public async Task Kullanici_turleri_tek_tablodan_kendi_siniflariyla_okunur()
    {
        var assistant = new Assistant("Elif Şahin", "elif@ornek.com", "ozet", _clinic.Serdivan);
        var manager = new BranchManager("Murat Çelik", "murat@ornek.com", "ozet", _clinic.Kadikoy);
        await using (var context = _db.CreateContext())
        {
            context.Attach(_clinic.Serdivan);
            context.Attach(_clinic.Kadikoy);
            context.AddRange(assistant, manager);
            await context.SaveChangesAsync();
        }

        await using (var context = _db.CreateContext())
        {
            var users = await context.Users.ToListAsync();
            var byType = users.GroupBy(u => u.GetType()).ToDictionary(g => g.Key, g => g.Count());
            Assert.Equal(3, byType[typeof(Dentist)]);
            Assert.Equal(4, byType[typeof(Patient)]);
            Assert.Equal(1, byType[typeof(ChainAdministrator)]);
            Assert.Equal(1, byType[typeof(Assistant)]);
            Assert.Equal(1, byType[typeof(BranchManager)]);
            Assert.All(users, u => Assert.Equal(u.GetType().Name, u.Role.ToString()));

            // Asistan ve şube yöneticisi aynı BranchId sütununu kullanıyor ama doğru şubeye bağlanıyor.
            var loadedAssistant = await context.Users.OfType<Assistant>().Include(a => a.Branch).SingleAsync();
            var loadedManager = await context.Users.OfType<BranchManager>().Include(m => m.Branch).SingleAsync();
            Assert.Equal("Serdivan Şubesi", loadedAssistant.Branch.Name);
            Assert.True(loadedManager.CanManageBranch(_clinic.Kadikoy));
            Assert.False(loadedManager.CanManageBranch(_clinic.Serdivan));

            var types = await context.Database
                .SqlQueryRaw<string>("SELECT UserType AS Value FROM Users WHERE Id = {0}", assistant.Id)
                .ToListAsync();
            Assert.Equal(["Assistant"], types);
        }
    }

    [Fact]
    public async Task Ayni_eposta_ikinci_kez_kaydedilemez_buyuk_harfle_yazilsa_bile()
    {
        await using var context = _db.CreateContext();
        context.Users.Add(new Patient("Ali Yıldız (2)", "ALI@ornek.com", "ozet"));

        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }

    // ---- Randevu ----

    [Fact]
    public async Task Randevu_butun_alanlariyla_geri_okunur_durum_metin_olarak_saklanir()
    {
        var created = new DateTime(2026, 11, 9, 8, 30, 0);
        var appointment = new Appointment(
            _clinic.Ali, _clinic.Ayse, _clinic.CanalTreatment, _clinic.Serdivan, At(10), created, "Gece ağrısı var.");
        await using (var context = _db.CreateContext())
        {
            context.AttachRange(_clinic.Ali, _clinic.Ayse, _clinic.Serdivan);
            context.Appointments.Add(appointment);
            await context.SaveChangesAsync();
        }

        await using (var context = _db.CreateContext())
        {
            var loaded = await context.Appointments.SingleAsync(a => a.Id == appointment.Id);
            Assert.Equal(new TimeRange(At(10), At(11)), loaded.Period);
            Assert.Equal(AppointmentStatus.PendingApproval, loaded.Status);
            Assert.True(loaded.IsActive);
            Assert.Equal("Kanal tedavisi", loaded.ServiceNameAtBooking);
            Assert.Equal(5000m, loaded.PriceAtBooking);
            Assert.Equal(created, loaded.CreatedAt);
            Assert.Equal("Gece ağrısı var.", loaded.PatientNote);

            var status = await context.Database
                .SqlQueryRaw<string>("SELECT Status AS Value FROM Appointments WHERE Id = {0}", appointment.Id)
                .SingleAsync();
            Assert.Equal("PendingApproval", status);
        }
    }

    [Fact]
    public async Task Okunan_randevunun_durum_nesnesi_kaydedilen_durumdan_secilir()
    {
        // K26: veritabanında sadece enum var; okunduktan sonra geçişler yine durum sınıfından geçiyor.
        var appointment = Existing(_clinic.Ali, _clinic.Ayse, _clinic.CanalTreatment, _clinic.Serdivan, At(10));
        await using (var context = _db.CreateContext())
        {
            context.AttachRange(_clinic.Ali, _clinic.Ayse, _clinic.Serdivan);
            context.Appointments.Add(appointment);
            await context.SaveChangesAsync();
        }

        await using (var context = _db.CreateContext())
        {
            var loaded = await context.Appointments
                .Include(a => a.Dentist).Include(a => a.Branch).Include(a => a.Patient)
                .SingleAsync(a => a.Id == appointment.Id);
            var ayse = loaded.Dentist;

            loaded.Reject(ayse, "Önce muayene gerekiyor.");
            await context.SaveChangesAsync();
            Assert.Throws<DomainException>(() => loaded.Approve(ayse, _clinic.Now));
        }

        await using (var context = _db.CreateContext())
        {
            var loaded = await context.Appointments.SingleAsync(a => a.Id == appointment.Id);
            Assert.Equal(AppointmentStatus.Rejected, loaded.Status);
            Assert.False(loaded.IsActive);
            Assert.Equal("Önce muayene gerekiyor.", loaded.RejectionReason);
        }
    }
}

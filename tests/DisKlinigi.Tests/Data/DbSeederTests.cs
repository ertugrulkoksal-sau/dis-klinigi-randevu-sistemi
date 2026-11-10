using DisKlinigi.Core.Booking.Rules;
using DisKlinigi.Core.Domain;
using DisKlinigi.Core.Domain.Appointments;
using DisKlinigi.Core.Domain.Users;
using DisKlinigi.Web.Data;
using DisKlinigi.Web.Data.Repositories;
using DisKlinigi.Web.Security;
using Microsoft.EntityFrameworkCore;

namespace DisKlinigi.Tests.Data;

/// <summary>
/// Örnek veri: boş veritabanına bir kez yükleniyor, haftanın her gününde tutarlı ve
/// parolalar özetli. Tutarlılık kontrolleri kural sınıflarından bağımsız yazıldı
/// (doğrudan alan metotları ve aralık kesişimi); örnek veriyi üreten kodla aynı kodu
/// kullansaydı, bir hata ikisinde birden olur ve test yakalamazdı.
/// </summary>
public class DbSeederTests
{
    /// <summary>Salı 11:00; bu saatte bazı randevular bitmiş, bazıları sürüyor, bazıları ileride.</summary>
    private static readonly DateTime TuesdayMorning = new(2026, 11, 10, 11, 0, 0);

    [Fact]
    public async Task Bos_veritabanina_ornek_veri_bir_kez_yuklenir()
    {
        await using var db = await SqliteTestDatabase.CreateAsync();

        await using (var context = db.CreateContext())
            Assert.True(await DbSeeder.SeedAsync(context, new FakePasswordHasher(), TuesdayMorning));

        await using (var context = db.CreateContext())
        {
            Assert.Equal(3, await context.Branches.CountAsync());
            Assert.Equal(8, await context.Departments.CountAsync());
            Assert.Equal(10, await context.Services.CountAsync());
            Assert.Equal(8, await context.Users.OfType<Dentist>().CountAsync());
            Assert.Equal(8, await context.Users.OfType<Patient>().CountAsync());
            Assert.Equal(3, await context.Users.OfType<Assistant>().CountAsync());
            Assert.Equal(3, await context.Users.OfType<BranchManager>().CountAsync());
            Assert.Equal(1, await context.Users.OfType<ChainAdministrator>().CountAsync());

            // Ekranlarda gösterilecek her durum örnek veride var.
            var statuses = await context.Appointments.Select(a => a.Status).Distinct().ToListAsync();
            Assert.Equal(Enum.GetValues<AppointmentStatus>().OrderBy(s => s), statuses.OrderBy(s => s));

            var appointments = await context.Appointments.ToListAsync();
            Assert.Contains(appointments, a => a.PatientNote is not null);
            Assert.Contains(appointments, a => a.Status == AppointmentStatus.Rejected && a.RejectionReason is not null);
            // Onay kuyruğundaki "Saati geçti" örneği (K28).
            Assert.Contains(appointments, a => a.Status == AppointmentStatus.PendingApproval && a.HasStarted(TuesdayMorning));
            // Bugünün takvimi boş değil.
            Assert.Contains(appointments, a => DateOnly.FromDateTime(a.Period.Start) == DateOnly.FromDateTime(TuesdayMorning));
        }

        int count;
        await using (var context = db.CreateContext())
        {
            count = await context.Appointments.CountAsync();
            Assert.False(await DbSeeder.SeedAsync(context, new FakePasswordHasher(), TuesdayMorning));
        }

        await using (var context = db.CreateContext())
        {
            Assert.Equal(3, await context.Branches.CountAsync());
            Assert.Equal(count, await context.Appointments.CountAsync());
        }
    }

    /// <summary>
    /// Randevular bugüne göre yerleştiği için haftanın her günü ayrı denendi: Pazartesi sabahı
    /// (geçen haftanın randevuları geçmişte) ile Pazar akşamı (bugün kimse çalışmıyor) farklı veri üretiyor.
    /// </summary>
    [Theory]
    [InlineData("2026-11-09 08:00")] // Pazartesi, mesai başlamadan
    [InlineData("2026-11-10 11:00")] // Salı, randevuların bir kısmı geçmiş
    [InlineData("2026-11-11 13:10")] // Çarşamba, bir randevunun ortası
    [InlineData("2026-11-12 19:30")] // Perşembe, mesai bitmiş
    [InlineData("2026-11-13 09:00")] // Cuma
    [InlineData("2026-11-14 12:00")] // Cumartesi
    [InlineData("2026-11-15 20:00")] // Pazar
    public async Task Ornek_veri_haftanin_her_gununde_alan_kurallarina_uyar(string nowText)
    {
        var now = DateTime.Parse(nowText, System.Globalization.CultureInfo.InvariantCulture);
        await using var db = await SqliteTestDatabase.CreateAsync();
        await using (var seed = db.CreateContext())
            await DbSeeder.SeedAsync(seed, new FakePasswordHasher(), now);

        await using var context = db.CreateContext();
        var branches = await new EfBranchRepository(context).GetAllAsync();
        var dentistRepository = new EfDentistRepository(context);
        foreach (var id in await context.Users.OfType<Dentist>().Select(d => d.Id).ToListAsync())
            await dentistRepository.GetByIdAsync(id);
        var appointments = await context.Appointments
            .Include(a => a.Patient).Include(a => a.Service).ThenInclude(s => s.Department)
            .Include(a => a.Dentist).Include(a => a.Branch).ThenInclude(b => b.WorkingHours)
            .Include(a => a.Branch).ThenInclude(b => b.Departments)
            .AsSplitQuery()
            .ToListAsync();

        // Saati tutmuş randevular: iptal ve ret saati bırakıyor, diğerleri o saatte koltuktaydı.
        var held = appointments
            .Where(a => a.Status is not (AppointmentStatus.Cancelled or AppointmentStatus.Rejected))
            .ToList();

        foreach (var a in appointments)
        {
            Assert.True(a.CreatedAt < a.Period.Start, $"{a}: başlangıçtan sonra alınmış.");
            Assert.True(a.CreatedAt <= now, $"{a}: gelecekte alınmış.");
            Assert.True(a.Dentist.Offers(a.Service), $"{a}: hekim bu hizmeti vermiyor.");
            Assert.True(a.Branch.HostsDepartment(a.Service.Department), $"{a}: şubede bu bölüm yok.");
            Assert.True(a.Branch.IsOpenDuring(a.Period), $"{a}: şube kapalı.");
            Assert.True(a.Dentist.IsAvailable(a.Branch, a.Period), $"{a}: hekimin müsaitliği dışında.");

            // Sonuç sadece saati gelmiş randevuya işlenebilir; bitmiş randevu onaylı kalmamalı.
            if (a.Status is AppointmentStatus.Completed or AppointmentStatus.NoShow)
                Assert.True(a.HasStarted(now), $"{a}: saati gelmeden sonucu işlenmiş.");
            if (a.Status == AppointmentStatus.Confirmed)
                Assert.True(a.Period.End > now, $"{a}: bitmiş ama hâlâ onaylı.");
        }

        foreach (var (a, b) in Pairs(held))
        {
            if (!a.Period.Overlaps(b.Period))
                continue;
            Assert.False(a.Dentist.IsSameAs(b.Dentist), $"Hekim çakışması: {a} / {b}");
            Assert.False(a.Patient.IsSameAs(b.Patient), $"Hasta çakışması: {a} / {b}");
        }

        foreach (var branch in branches)
        {
            var periods = held.Where(a => a.Branch.Id == branch.Id).Select(a => a.Period).ToList();
            foreach (var period in periods)
                Assert.True(ChairCapacityRule.PeakOccupancy(periods, period) <= branch.ChairCount,
                    $"{branch.Name}: {period} sırasında ünit sayısı aşılıyor.");
        }
    }

    [Fact]
    public async Task Ornek_hesaplarin_parolalari_ozetli_saklanir_ve_ornek_parolayla_dogrulanir()
    {
        var hasher = new AspNetPasswordHasher();
        await using var db = await SqliteTestDatabase.CreateAsync();
        await using (var seed = db.CreateContext())
            await DbSeeder.SeedAsync(seed, hasher, TuesdayMorning);

        await using var context = db.CreateContext();
        var hashes = await context.Users.Select(u => u.PasswordHash).ToListAsync();

        Assert.Equal(23, hashes.Count);
        Assert.DoesNotContain(hashes, h => h.Contains(SampleData.DemoPassword));
        Assert.Equal(hashes.Count, hashes.Distinct().Count()); // her hesabın tuzu ayrı
        Assert.All(hashes, h => Assert.True(hasher.Verify(h, SampleData.DemoPassword)));
        Assert.All(hashes, h => Assert.False(hasher.Verify(h, "demo1234!")));
    }

    private static IEnumerable<(Appointment, Appointment)> Pairs(IReadOnlyList<Appointment> list)
    {
        for (var i = 0; i < list.Count; i++)
            for (var j = i + 1; j < list.Count; j++)
                yield return (list[i], list[j]);
    }
}

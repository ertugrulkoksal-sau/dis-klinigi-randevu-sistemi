using DisKlinigi.Core.Availability;
using DisKlinigi.Tests.Booking;
using DisKlinigi.Web.Data.Repositories;
using Microsoft.EntityFrameworkCore;
using static DisKlinigi.Tests.Booking.BookingClinic;

namespace DisKlinigi.Tests.Data;

/// <summary>
/// Müsaitlik aralığının eklenip silinmesi SQLite üzerinde: hekimin listesinden çıkan aralık tablodan
/// da siliniyor (sahipsiz kayıt, Cascade), ileri tarihte randevusu olan aralık ise yerinde kalıyor.
/// </summary>
public class EfAvailabilityTests : IAsyncLifetime
{
    private readonly BookingClinic _clinic = new();
    private SqliteTestDatabase _db = null!;

    public async Task InitializeAsync()
    {
        _db = await SqliteTestDatabase.CreateAsync();
        await _db.SaveAsync(_clinic);
    }

    public async Task DisposeAsync() => await _db.DisposeAsync();

    private async Task<int> WindowCountAsync()
    {
        await using var context = _db.CreateContext();
        return await context.Set<DisKlinigi.Core.Domain.Users.AvailabilityWindow>().CountAsync();
    }

    [Fact]
    public async Task Silinen_aralik_tablodan_da_silinir_eklenen_kaydedilir()
    {
        var before = await WindowCountAsync();
        var windowId = _clinic.Ayse.AvailabilityAt(_clinic.Kadikoy, DayOfWeek.Tuesday).Single().Id;

        await using (var context = _db.CreateContext())
        {
            var dentists = new EfDentistRepository(context);
            var service = new AvailabilityService(dentists, new EfAppointmentRepository(context), _clinic.Clock);
            var ayse = (await dentists.GetByIdAsync(_clinic.Ayse.Id))!;

            Assert.True((await service.RemoveAsync(ayse, windowId)).Succeeded);
            Assert.True((await service.AddAsync(ayse, _clinic.Serdivan.Id, DayOfWeek.Saturday, new(9, 0), new(12, 0))).Succeeded);
        }

        await using (var context = _db.CreateContext())
        {
            var ayse = (await new EfDentistRepository(context).GetByIdAsync(_clinic.Ayse.Id))!;
            Assert.Equal(before, await WindowCountAsync());
            Assert.Empty(ayse.AvailabilityAt(ayse.Branches.Single(b => b.Id == _clinic.Kadikoy.Id), DayOfWeek.Tuesday));
            Assert.Single(ayse.AvailabilityAt(ayse.Branches.Single(b => b.Id == _clinic.Serdivan.Id), DayOfWeek.Saturday));
        }
    }

    [Fact]
    public async Task Ileri_tarihte_randevusu_olan_aralik_veritabaninda_kalir()
    {
        var c = _clinic;
        await using (var context = _db.CreateContext())
        {
            context.AttachRange(c.Serdivan, c.Kadikoy, c.Ayse, c.Ali);
            context.Appointments.Add(Existing(c.Ali, c.Ayse, c.Examination, c.Kadikoy, At(15)));
            await context.SaveChangesAsync();
        }
        var before = await WindowCountAsync();
        var windowId = c.Ayse.AvailabilityAt(c.Kadikoy, DayOfWeek.Tuesday).Single().Id;

        await using (var context = _db.CreateContext())
        {
            var dentists = new EfDentistRepository(context);
            var service = new AvailabilityService(dentists, new EfAppointmentRepository(context), c.Clock);
            var result = await service.RemoveAsync((await dentists.GetByIdAsync(c.Ayse.Id))!, windowId);

            Assert.False(result.Succeeded);
        }

        Assert.Equal(before, await WindowCountAsync());
    }
}

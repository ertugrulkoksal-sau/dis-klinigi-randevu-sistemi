using System.Security.Cryptography;
using System.Text;
using DisKlinigi.Core.Abstractions;
using DisKlinigi.Core.Domain;
using DisKlinigi.Core.Domain.Users;
using DisKlinigi.Tests.Booking;
using DisKlinigi.Web.Data;
using DisKlinigi.Web.Data.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace DisKlinigi.Tests.Data;

/// <summary>
/// Her test için geçici klasörde ayrı bir SQLite dosyası; şema uygulamadaki gibi migration'larla kuruluyor.
/// </summary>
/// <remarks>
/// Bellek içi SQLite yerine dosya kullandım, çünkü eş zamanlılık testinde iki ayrı bağlantının
/// aynı veritabanını görmesi ve SQLite'ın dosya kilidinin gerçekten devreye girmesi gerekiyor.
/// Bağlantı havuzu kapalı; yoksa Windows'ta açık kalan bağlantı dosyanın silinmesini engelliyor.
/// </remarks>
internal sealed class SqliteTestDatabase : IAsyncDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"disklinigi-test-{Guid.NewGuid():N}.db");

    private SqliteTestDatabase()
    {
    }

    public static async Task<SqliteTestDatabase> CreateAsync()
    {
        var database = new SqliteTestDatabase();
        await using var context = database.CreateContext();
        await context.Database.MigrateAsync();
        return database;
    }

    public AppDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source={_path};Pooling=False")
            .Options);

    /// <summary>Bir bağlamda verilen nesneleri (ve bağlı oldukları her şeyi) kaydeder.</summary>
    public async Task SaveAsync(params object[] entities)
    {
        await using var context = CreateContext();
        context.AddRange(entities);
        await context.SaveChangesAsync();
    }

    /// <summary>Çakışma testlerindeki küçük kliniğin hepsini kaydeder; Id'ler nesnelere yazılıyor.</summary>
    public Task SaveAsync(BookingClinic c) =>
        SaveAsync(c.Serdivan, c.Kadikoy, c.Ayse, c.Mehmet, c.Burak, c.Ali, c.Zeynep, c.Can, c.Deniz, c.Admin);

    public ValueTask DisposeAsync()
    {
        SqliteConnection.ClearAllPools();
        foreach (var file in new[] { _path, _path + "-wal", _path + "-shm", _path + "-journal" })
        {
            if (File.Exists(file))
                File.Delete(file);
        }
        return ValueTask.CompletedTask;
    }
}

/// <summary>
/// <see cref="BookingClinic"/>'in kayıtlı halinin, verilen bağlamda uygulamanın yapacağı gibi
/// repository'lerle yeniden yüklenmiş hali. Aynı bağlamda yüklendikleri için hekimin şubesi ile
/// şube nesnesi aynı nesne.
/// </summary>
internal sealed record LoadedClinic(
    Dentist Ayse, Dentist Mehmet, Dentist Burak,
    Patient Ali, Patient Zeynep,
    Branch Serdivan, Branch Kadikoy,
    Service Examination, Service CanalTreatment)
{
    public static async Task<LoadedClinic> LoadAsync(AppDbContext context, BookingClinic c)
    {
        var dentists = new EfDentistRepository(context);
        var branches = new EfBranchRepository(context);

        async Task<Dentist> Dentist(Dentist d) => (await dentists.GetByIdAsync(d.Id))!;
        Task<Patient> Patient(Patient p) => context.Users.OfType<Patient>().SingleAsync(x => x.Id == p.Id);
        async Task<Branch> Branch(Branch b) => (await branches.GetByIdAsync(b.Id))!;
        Task<Service> Service(Service s) => context.Services.Include(x => x.Department).SingleAsync(x => x.Id == s.Id);

        return new LoadedClinic(
            await Dentist(c.Ayse), await Dentist(c.Mehmet), await Dentist(c.Burak),
            await Patient(c.Ali), await Patient(c.Zeynep),
            await Branch(c.Serdivan), await Branch(c.Kadikoy),
            await Service(c.Examination), await Service(c.CanalTreatment));
    }
}

/// <summary>
/// Testlerde PBKDF2'nin yavaşlığına (her özet onlarca milisaniye) gerek olmadığı için basit bir özetleyici.
/// Gerçek özetleyici <c>AspNetPasswordHasherTests</c> ve örnek veri parola testinde kullanılıyor.
/// </summary>
internal sealed class FakePasswordHasher : IPasswordHasher
{
    public string Hash(string password) =>
        "test:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(password)));

    public bool Verify(string passwordHash, string password) => passwordHash == Hash(password);
}

using DisKlinigi.Core.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace DisKlinigi.Web.Data;

/// <summary>
/// Uygulama açılırken veritabanını günceller (migration) ve boşsa örnek veriyi
/// yükler. Sunumda "dotnet run" dışında bir adım gerekmesin diye bu iş açılışta
/// otomatik yapılıyor (K18).
/// </summary>
public static class DbSeeder
{
    public static async Task InitializeAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var provider = scope.ServiceProvider;
        var db = provider.GetRequiredService<AppDbContext>();

        await db.Database.MigrateAsync();

        // Örnek randevular bugünün tarihine göre yerleştiriliyor (K10: yerel saat).
        var now = provider.GetRequiredService<TimeProvider>().GetLocalNow().DateTime;
        await SeedAsync(db, provider.GetRequiredService<IPasswordHasher>(), now);
    }

    /// <summary>
    /// Veritabanında hiç şube yoksa örnek veriyi tek seferde kaydeder; yüklediyse true döner.
    /// Testler de bu metodu kendi saatleriyle çağırıyor.
    /// </summary>
    /// <remarks>
    /// 6 Kasım'daki ilk sürüm sadece üç şube yüklüyordu. O sürümle oluşmuş bir veritabanında
    /// şube olduğu için bu metot bir şey yapmıyor; yeni örnek veri için veritabanı dosyasını
    /// silmek gerekiyor (README, "Örnek veriye sıfırdan dönmek").
    /// </remarks>
    public static async Task<bool> SeedAsync(
        AppDbContext db, IPasswordHasher hasher, DateTime now, CancellationToken cancellationToken = default)
    {
        if (await db.Branches.AnyAsync(cancellationToken))
            return false;

        var data = SampleData.Create(hasher, now);
        db.Departments.AddRange(data.Departments);
        db.Services.AddRange(data.Services);
        db.Branches.AddRange(data.Branches);
        db.Users.AddRange(data.Users);
        db.Appointments.AddRange(data.Appointments);

        await db.SaveChangesAsync(cancellationToken);
        return true;
    }
}

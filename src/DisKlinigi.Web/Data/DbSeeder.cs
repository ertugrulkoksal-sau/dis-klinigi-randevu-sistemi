using DisKlinigi.Core.Domain;
using Microsoft.EntityFrameworkCore;

namespace DisKlinigi.Web.Data;

/// <summary>
/// Uygulama açılırken veritabanını günceller (migration) ve boşsa örnek veriyi
/// yükler. Sunumda "dotnet run" dışında bir adım gerekmesin diye bu iş açılışta
/// otomatik yapılıyor.
/// </summary>
public static class DbSeeder
{
    public static async Task InitializeAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        await db.Database.MigrateAsync();

        if (await db.Branches.AnyAsync())
            return; // Örnek veri daha önce yüklenmiş.

        db.Branches.AddRange(
            new Branch("Serdivan Şubesi", "Sakarya", "Kemalpaşa Mah. Üniversite Cad. No:12, Serdivan", chairCount: 4),
            new Branch("Adapazarı Merkez Şubesi", "Sakarya", "Çark Cad. No:45, Adapazarı", chairCount: 3),
            new Branch("Kadıköy Şubesi", "İstanbul", "Bağdat Cad. No:210, Kadıköy", chairCount: 5));

        await db.SaveChangesAsync();
    }
}

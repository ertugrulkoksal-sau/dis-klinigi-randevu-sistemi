using DisKlinigi.Core.Domain;
using DisKlinigi.Core.Domain.Users;
using DisKlinigi.Web.Data.Repositories;
using Microsoft.EntityFrameworkCore;

namespace DisKlinigi.Tests.Data;

/// <summary>Kullanıcı repository'si gerçek SQLite üzerinde: tür, şube yüklemesi ve e-posta benzersizliği.</summary>
public class EfUserRepositoryTests
{
    private const string Hash = "test:ozet";

    [Fact]
    public async Task Eposta_ile_bulunan_kullanici_gercek_sinifiyla_ve_subesiyle_gelir()
    {
        await using var db = await SqliteTestDatabase.CreateAsync();
        var serdivan = new Branch("Serdivan Şubesi", "Sakarya", "", chairCount: 4);
        await db.SaveAsync(serdivan,
            new Assistant("Elif Şahin", "elif.sahin@disklinigi.test", Hash, serdivan),
            new BranchManager("Murat Çelik", "murat.celik@disklinigi.test", Hash, serdivan),
            new Patient("Ali Yıldız", "ali.yildiz@ornek.test", Hash));

        await using var context = db.CreateContext();
        var users = new EfUserRepository(context);

        var assistant = Assert.IsType<Assistant>(await users.GetByEmailAsync(" Elif.Sahin@DisKlinigi.test"));
        var manager = Assert.IsType<BranchManager>(await users.GetByEmailAsync("murat.celik@disklinigi.test"));
        // Şube yüklenmeseydi CanManageBranch null şubeyle yanlış cevap verirdi.
        Assert.Equal("Serdivan Şubesi", assistant.AssignedBranch?.Name);
        Assert.True(manager.CanManageBranch(manager.Branch));
        Assert.IsType<Patient>(await users.GetByIdAsync((await users.GetByEmailAsync("ali.yildiz@ornek.test"))!.Id));
        Assert.Null(await users.GetByEmailAsync("yok@ornek.test"));
    }

    [Fact]
    public async Task Kullanilan_eposta_ile_ikinci_kullanici_eklenmez()
    {
        await using var db = await SqliteTestDatabase.CreateAsync();
        await db.SaveAsync(new Patient("Ali Yıldız", "ali.yildiz@ornek.test", Hash));

        await using (var context = db.CreateContext())
        {
            var users = new EfUserRepository(context);
            Assert.False(await users.TryAddAsync(new Patient("Başka Ali", "ALI.YILDIZ@ornek.test", Hash)));
            Assert.True(await users.TryAddAsync(new Patient("Zeynep Arslan", "zeynep.arslan@ornek.test", Hash)));
        }

        await using (var context = db.CreateContext())
            Assert.Equal(2, await context.Users.CountAsync());
    }

    /// <summary>
    /// Ön kontrolden sonra, kayıttan önce aynı e-posta başka bir bağlantıdan kaydedilirse
    /// (eş zamanlı iki kayıt isteği) benzersiz indeks yakalıyor ve cevap yine false.
    /// </summary>
    [Fact]
    public async Task On_kontrolden_kacan_ayni_eposta_benzersiz_indekse_takilir()
    {
        await using var db = await SqliteTestDatabase.CreateAsync();
        await using var context = db.CreateContext();
        var users = new EfUserRepository(context);

        // Bu bağlam kaydetmeye başladığı anda (ön kontrol "boş" demiş durumda) ikinci istek
        // aynı e-postayla kaydını ayrı bir bağlantıdan bitiriyor.
        void SecondRequest(object? sender, SavingChangesEventArgs e)
        {
            context.SavingChanges -= SecondRequest;
            db.SaveAsync(new Patient("İkinci", "ali@ornek.test", Hash)).GetAwaiter().GetResult();
        }
        context.SavingChanges += SecondRequest;

        Assert.False(await users.TryAddAsync(new Patient("Birinci", "ali@ornek.test", Hash)));

        // Kaydedilemeyen nesne bağlamdan çıkarıldı; aynı bağlamla başka bir kayıt hâlâ yapılabiliyor.
        Assert.True(await users.TryAddAsync(new Patient("Zeynep Arslan", "zeynep@ornek.test", Hash)));
        await using var check = db.CreateContext();
        Assert.Equal(["İkinci", "Zeynep Arslan"], await check.Users.OrderBy(u => u.Id).Select(u => u.FullName).ToListAsync());
    }
}

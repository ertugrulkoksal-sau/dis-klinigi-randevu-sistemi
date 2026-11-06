using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Unicode;
using DisKlinigi.Core.Abstractions;
using DisKlinigi.Web.Data;
using DisKlinigi.Web.Data.Repositories;
using Microsoft.AspNetCore.Localization;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.WebEncoders;

var builder = WebApplication.CreateBuilder(args);

// SQLite dosyasının yolu proje klasörüne göre çözülüyor. Böylece uygulama
// proje klasöründen "dotnet run" ile de, çözüm klasöründen
// "dotnet run --project src/DisKlinigi.Web" ile de başlatılsa aynı dosya kullanılıyor.
var sqlite = new SqliteConnectionStringBuilder(builder.Configuration.GetConnectionString("Default"));
if (!Path.IsPathRooted(sqlite.DataSource))
    sqlite.DataSource = Path.Combine(builder.Environment.ContentRootPath, sqlite.DataSource);

builder.Services.AddDbContext<AppDbContext>(options => options.UseSqlite(sqlite.ToString()));

// Zamanı doğrudan DateTime.Now ile okumak yerine TimeProvider enjekte ediliyor;
// 24 saat iptal kuralı gibi zamana bağlı kurallar testte sabit bir saatle sınanabilecek.
builder.Services.AddSingleton(TimeProvider.System);

builder.Services.AddScoped<IBranchRepository, EfBranchRepository>();

builder.Services.AddControllersWithViews();

// Razor varsayılan olarak ASCII dışındaki karakterleri (ş, ğ, ı...) HTML
// varlıklarına çeviriyor; sayfa kaynağı okunabilir kalsın diye tüm Unicode serbest.
builder.Services.Configure<WebEncoderOptions>(o =>
    o.TextEncoderSettings = new TextEncoderSettings(UnicodeRanges.All));

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
}

// Tarih ve para birimi (₺) gösterimi için Türkçe kültür kullanılıyor.
var turkish = new CultureInfo("tr-TR");
app.UseRequestLocalization(new RequestLocalizationOptions
{
    DefaultRequestCulture = new RequestCulture(turkish),
    SupportedCultures = [turkish],
    SupportedUICultures = [turkish]
});

app.UseRouting();
app.UseAuthorization();

app.MapStaticAssets();
app.MapControllerRoute(
        name: "default",
        pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

await DbSeeder.InitializeAsync(app.Services);

app.Run();

using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Unicode;
using DisKlinigi.Core.Abstractions;
using DisKlinigi.Core.Accounts;
using DisKlinigi.Core.Appointments;
using DisKlinigi.Core.Availability;
using DisKlinigi.Core.Booking;
using DisKlinigi.Web.Data;
using DisKlinigi.Web.Data.Repositories;
using DisKlinigi.Web.Security;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
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

// Repository'ler istek başına (scoped): her istek kendi DbContext'iyle çalışıyor.
builder.Services.AddScoped<IBranchRepository, EfBranchRepository>();
builder.Services.AddScoped<IDentistRepository, EfDentistRepository>();
builder.Services.AddScoped<IAppointmentRepository, EfAppointmentRepository>();
builder.Services.AddScoped<IUserRepository, EfUserRepository>();
builder.Services.AddScoped<ICatalogRepository, EfCatalogRepository>();

// Randevu kuralları durumsuz olduğu için tek örnek yeterli. Kayıt sırası, çiğnenen
// kuralların kullanıcıya gösterildiği sıra (tasarım.md bölüm 3); planlayıcı listeyi
// IEnumerable<IBookingRule> olarak bu sırayla alıyor.
foreach (var rule in BookingRules.CreateDefault())
    builder.Services.AddSingleton(rule);
builder.Services.AddScoped<AppointmentScheduler>();
builder.Services.AddScoped<AppointmentService>();
builder.Services.AddScoped<AvailabilityService>();

builder.Services.AddSingleton<IPasswordHasher, AspNetPasswordHasher>();
builder.Services.AddScoped<AccountService>();

// Kimlik doğrulama: ASP.NET Core Identity değil, çerez + kendi kullanıcı hiyerarşimiz (K15).
// Çerez JavaScript'ten okunamıyor (HttpOnly); http üzerinden çalıştığımız için (K21)
// Secure bayrağı isteğe göre veriliyor. Oturum 8 saat, kullanıldıkça uzuyor.
builder.Services.AddScoped<UserSessionValidator>();
builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.LogoutPath = "/Account/Logout";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.Cookie.Name = "DisKlinigi.Oturum";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
        // Her istekte çerezdeki kullanıcı veritabanıyla karşılaştırılıyor (K42).
        options.EventsType = typeof(UserSessionValidator);
    });
builder.Services.AddAuthorization(Policies.AddTo);

// Bütün POST formları sahte istek (CSRF) belirteci istiyor; form tag helper'ı belirteci kendisi ekliyor.
builder.Services.AddControllersWithViews(options =>
    options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute()));

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
app.UseAuthentication();
app.UseAuthorization();

// Varsayılan politika "giriş zorunlu" olduğu için (Policies.AddTo) CSS ve JS dosyaları
// ayrıca herkese açılıyor; yoksa giriş sayfası stilsiz görünürdü.
app.MapStaticAssets().AllowAnonymous();
app.MapControllerRoute(
        name: "default",
        pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

await DbSeeder.InitializeAsync(app.Services);

app.Run();

// Testlerdeki WebApplicationFactory<Program> için (üst düzey ifadelerin sınıfı varsayılan olarak internal).
public partial class Program;

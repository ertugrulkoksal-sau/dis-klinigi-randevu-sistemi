using System.Net;
using System.Text.RegularExpressions;
using DisKlinigi.Web.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace DisKlinigi.Tests.Web;

/// <summary>
/// Uygulamanın kendisini (Program.cs'teki bütün kayıtlarla) bellekte bir test sunucusunda
/// çalıştırıyor. Tek fark veritabanı: proje klasöründeki dosya yerine geçici bir SQLite dosyası.
/// Açılışta migration'lar ve örnek veri her zamanki gibi yükleniyor, parolalar gerçek özetleyiciyle.
/// </summary>
public sealed class ClinicWebFactory : WebApplicationFactory<Program>
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"disklinigi-web-{Guid.NewGuid():N}.db");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<AppDbContext>>();
            services.AddDbContext<AppDbContext>(options => options.UseSqlite($"Data Source={_path};Pooling=False"));
        });
    }

    /// <summary>Yönlendirmeleri kendisi izlemeyen istemci; testler 302 cevabının nereye gittiğine bakıyor.</summary>
    public HttpClient CreateBrowser() =>
        CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    public AppDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite($"Data Source={_path};Pooling=False").Options);

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        SqliteConnection.ClearAllPools();
        foreach (var file in new[] { _path, _path + "-wal", _path + "-shm" })
        {
            if (File.Exists(file))
                File.Delete(file);
        }
    }
}

/// <summary>Bütün web testleri aynı test sunucusunu kullanıyor; örnek veri bir kez yükleniyor.</summary>
[CollectionDefinition(Name)]
public sealed class WebCollection : ICollectionFixture<ClinicWebFactory>
{
    public const string Name = "Web";
}

/// <summary>Tarayıcının yaptığı gibi: sayfayı aç, formdaki CSRF belirtecini al, formu gönder.</summary>
internal static partial class BrowserExtensions
{
    public const string DemoPassword = DisKlinigi.Web.Data.SampleData.DemoPassword;

    [GeneratedRegex("name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"")]
    private static partial Regex TokenPattern();

    public static async Task<HttpResponseMessage> PostFormAsync(
        this HttpClient client, string pageUrl, string postUrl, IDictionary<string, string> fields)
    {
        var page = await client.GetStringAsync(pageUrl);
        var token = TokenPattern().Match(page);
        Assert.True(token.Success, $"{pageUrl} sayfasında form belirteci yok.");

        var form = new Dictionary<string, string>(fields) { ["__RequestVerificationToken"] = WebUtility.HtmlDecode(token.Groups[1].Value) };
        return await client.PostAsync(postUrl, new FormUrlEncodedContent(form));
    }

    public static Task<HttpResponseMessage> LoginAsync(this HttpClient client, string email, string password = DemoPassword, string? returnUrl = null) =>
        client.PostFormAsync(
            "/Account/Login",
            returnUrl is null ? "/Account/Login" : $"/Account/Login?returnUrl={Uri.EscapeDataString(returnUrl)}",
            new Dictionary<string, string> { ["Login.Email"] = email, ["Login.Password"] = password });

    /// <summary>302 cevabının gittiği adres, sorgu kısmı dahil (ör. "/Account/Login?ReturnUrl=%2FPatient%2FBook").</summary>
    public static string RedirectTarget(this HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var location = response.Headers.Location!;
        return location.IsAbsoluteUri ? location.PathAndQuery : location.OriginalString;
    }
}

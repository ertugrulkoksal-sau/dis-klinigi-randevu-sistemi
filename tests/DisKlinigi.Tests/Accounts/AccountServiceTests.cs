using DisKlinigi.Core.Accounts;
using DisKlinigi.Core.Domain;
using DisKlinigi.Core.Domain.Users;
using DisKlinigi.Tests.Data;

namespace DisKlinigi.Tests.Accounts;

/// <summary>Giriş ve hasta kaydı kuralları; web ve veritabanı olmadan (K11).</summary>
public class AccountServiceTests
{
    private const string Password = "Gizli-Parola-42";

    private readonly FakePasswordHasher _hasher = new();
    private readonly InMemoryUserRepository _users;
    private readonly AccountService _service;
    private readonly Assistant _elif;

    public AccountServiceTests()
    {
        var serdivan = new Branch("Serdivan Şubesi", "Sakarya", "", chairCount: 4);
        _elif = new Assistant("Elif Şahin", "elif.sahin@disklinigi.test", _hasher.Hash(Password), serdivan);
        _users = new InMemoryUserRepository(_elif);
        _service = new AccountService(_users, _hasher);
    }

    // ---- Giriş ----

    [Fact]
    public async Task Dogru_eposta_ve_parolayla_kullanici_kendi_siniflariyla_doner()
    {
        var user = await _service.AuthenticateAsync("elif.sahin@disklinigi.test", Password);

        // Tür kaybolmuyor: yetki soruları asistan sınıfında cevaplanıyor.
        Assert.Same(_elif, user);
        Assert.IsType<Assistant>(user);
    }

    [Fact]
    public async Task Eposta_buyuk_harfle_ve_bosluklu_yazilsa_da_giris_yapilir()
    {
        var user = await _service.AuthenticateAsync("  Elif.Sahin@DisKlinigi.TEST ", Password);

        Assert.Same(_elif, user);
    }

    [Theory]
    [InlineData("elif.sahin@disklinigi.test", "gizli-parola-42")] // parolada harf büyüklüğü önemli
    [InlineData("elif.sahin@disklinigi.test", Password + " ")]
    [InlineData("elif.sahin@disklinigi.test", "")]
    [InlineData("yok@disklinigi.test", Password)]
    [InlineData("", Password)]
    [InlineData(null, null)]
    public async Task Yanlis_parola_ya_da_kayitli_olmayan_eposta_ayni_cevabi_alir(string? email, string? password)
    {
        // İki durumda da null: ekran hangisinin yanlış olduğunu bilmiyor, söyleyemiyor (K43).
        Assert.Null(await _service.AuthenticateAsync(email, password));
    }

    // ---- Kayıt ----

    [Fact]
    public async Task Kayit_olan_hasta_parolasinin_ozetiyle_saklanir_ve_giris_yapabilir()
    {
        var result = await _service.RegisterPatientAsync(
            new PatientRegistration("Ali Yıldız", "Ali.Yildiz@Ornek.test", Password, "0532 111 22 33"));

        Assert.True(result.Succeeded);
        var patient = Assert.IsType<Patient>(Assert.Single(_users.Users, u => u is Patient));
        Assert.Same(result.Patient, patient);
        Assert.Equal("ali.yildiz@ornek.test", patient.Email);
        Assert.Equal("0532 111 22 33", patient.PhoneNumber);
        Assert.NotEqual(Password, patient.PasswordHash);
        Assert.DoesNotContain(Password, patient.PasswordHash);

        Assert.Same(patient, await _service.AuthenticateAsync("ali.yildiz@ornek.test", Password));
    }

    [Fact]
    public async Task Telefon_bos_birakilabilir()
    {
        var result = await _service.RegisterPatientAsync(new PatientRegistration("Ali Yıldız", "ali@ornek.test", Password));

        Assert.True(result.Succeeded);
        Assert.Null(result.Patient!.PhoneNumber);
    }

    [Theory]
    [InlineData("1234567", false)] // 7 karakter
    [InlineData("12345678", true)] // tam sınır
    public async Task Parola_en_az_sekiz_karakter_olmali(string password, bool accepted)
    {
        var result = await _service.RegisterPatientAsync(new PatientRegistration("Ali Yıldız", "ali@ornek.test", password));

        Assert.Equal(accepted, result.Succeeded);
        if (!accepted)
        {
            Assert.Equal(RegistrationField.Password, result.Field);
            Assert.Equal("Parola en az 8 karakter olmalıdır.", result.Error);
            Assert.Empty(_users.Users.OfType<Patient>());
        }
    }

    [Fact]
    public async Task Cok_uzun_parola_kabul_edilmez()
    {
        var tooLong = new string('a', AccountService.MaximumPasswordLength + 1);

        var result = await _service.RegisterPatientAsync(new PatientRegistration("Ali Yıldız", "ali@ornek.test", tooLong));

        Assert.False(result.Succeeded);
        Assert.Equal(RegistrationField.Password, result.Field);
    }

    [Fact]
    public async Task Kullanilan_eposta_harf_buyuklugu_farkli_olsa_da_ikinci_kez_kaydedilemez()
    {
        // Personel hesabının e-postasıyla da hasta kaydı açılamıyor; giriş adı bütün rollerde tek.
        var result = await _service.RegisterPatientAsync(
            new PatientRegistration("Elif Şahin", "ELIF.SAHIN@disklinigi.test", Password));

        Assert.False(result.Succeeded);
        Assert.Equal(RegistrationField.Email, result.Field);
        Assert.Single(_users.Users);
    }

    [Theory]
    [InlineData("", "ali@ornek.test", "Ad soyad boş olamaz.")]
    [InlineData("Ali Yıldız", "ali-ornek.test", "Geçerli bir e-posta adresi girilmelidir.")]
    public async Task Ad_ve_eposta_kurallari_alan_sinifindan_gelir(string fullName, string email, string expected)
    {
        var result = await _service.RegisterPatientAsync(new PatientRegistration(fullName, email, Password));

        Assert.False(result.Succeeded);
        Assert.Equal(expected, result.Error);
        Assert.Equal(RegistrationField.None, result.Field);
    }

    [Fact]
    public async Task Parola_kurali_once_kontrol_edilir_ve_gecersiz_parola_hic_ozetlenmez()
    {
        var counting = new CountingHasher();
        var service = new AccountService(new InMemoryUserRepository(), counting);

        await service.RegisterPatientAsync(new PatientRegistration("Ali Yıldız", "ali@ornek.test", "kisa"));

        Assert.Equal(0, counting.HashCount);
    }

    private sealed class CountingHasher : DisKlinigi.Core.Abstractions.IPasswordHasher
    {
        private readonly FakePasswordHasher _inner = new();

        public int HashCount { get; private set; }

        public string Hash(string password)
        {
            HashCount++;
            return _inner.Hash(password);
        }

        public bool Verify(string passwordHash, string password) => _inner.Verify(passwordHash, password);
    }
}

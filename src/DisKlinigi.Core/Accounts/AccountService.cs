using DisKlinigi.Core.Abstractions;
using DisKlinigi.Core.Domain;
using DisKlinigi.Core.Domain.Users;

namespace DisKlinigi.Core.Accounts;

/// <summary>
/// Giriş ve hasta kaydı. Çerez, yönlendirme gibi web ayrıntılarını bilmiyor; sadece
/// "bu e-posta ve parola hangi kullanıcıya ait?" ve "bu bilgilerle hasta kaydı açılabilir mi?"
/// sorularını cevaplıyor. Böylece giriş kuralları web sunucusu olmadan test edilebiliyor.
/// </summary>
/// <remarks>
/// Parola sadece <see cref="IPasswordHasher"/> üzerinden özetleniyor ve doğrulanıyor (K15);
/// bu sınıf da, kullanıcı sınıfları da düz parolayı hiçbir yere yazmıyor.
/// Personel hesapları burada değil, yönetici panelinde açılıyor (K36, 14 Kasım).
/// </remarks>
public sealed class AccountService
{
    /// <summary>Kayıt ekranındaki "En az 8 karakter" kuralı. Ekrandaki form doğrulaması da bu sabiti kullanıyor.</summary>
    public const int MinimumPasswordLength = 8;

    /// <summary>
    /// Üst sınır: PBKDF2 parolayı her turda yeniden işlediği için çok uzun bir parola sunucuyu
    /// gereksiz yere yorar. 128 karakter bir parola yöneticisinin üreteceğinden bile uzun.
    /// </summary>
    public const int MaximumPasswordLength = 128;

    private readonly IUserRepository _users;
    private readonly IPasswordHasher _hasher;

    public AccountService(IUserRepository users, IPasswordHasher hasher)
    {
        _users = users;
        _hasher = hasher;
    }

    /// <summary>
    /// E-posta ve parola doğruysa kullanıcıyı, değilse <c>null</c> döner.
    /// </summary>
    /// <remarks>
    /// "E-posta bulunamadı" ve "parola yanlış" ayrı cevaplar değil: ekranda tek bir mesaj
    /// gösteriliyor, böylece giriş formu hangi e-postaların kayıtlı olduğunu söylemiyor (K43).
    /// </remarks>
    public async Task<User?> AuthenticateAsync(string? email, string? password, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrEmpty(password))
            return null;

        var user = await _users.GetByEmailAsync(email, cancellationToken);
        if (user is null)
            return null;

        return _hasher.Verify(user.PasswordHash, password) ? user : null;
    }

    /// <summary>
    /// Yeni hasta hesabı açar. Parola kuralı, ad ve e-posta kontrolleri (alan sınıfında) ve
    /// e-postanın boşta olması sırayla kontrol ediliyor; ilk tutmayan kural sonuç olarak dönüyor.
    /// </summary>
    public async Task<RegistrationResult> RegisterPatientAsync(
        PatientRegistration registration, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(registration);

        var passwordError = CheckPassword(registration.Password);
        if (passwordError is not null)
            return RegistrationResult.Failure(passwordError, RegistrationField.Password);

        Patient patient;
        try
        {
            // Ad ve e-posta kuralları Patient/User sınıfında; burada tekrar yazmıyorum.
            patient = new Patient(
                registration.FullName,
                registration.Email,
                _hasher.Hash(registration.Password),
                registration.PhoneNumber);
        }
        catch (DomainException ex)
        {
            return RegistrationResult.Failure(ex.Message);
        }

        if (!await _users.TryAddAsync(patient, cancellationToken))
            return RegistrationResult.Failure(
                "Bu e-posta adresiyle açılmış bir hesap var. Giriş yapmayı deneyin.", RegistrationField.Email);

        return RegistrationResult.Success(patient);
    }

    /// <summary>
    /// Sadece uzunluk kontrol ediliyor. "Büyük harf, rakam, sembol şart" gibi kurallar kullanıcıyı
    /// tahmin edilebilir parolalara (Parola1!) itiyor; uzunluk daha anlamlı bir ölçü.
    /// </summary>
    private static string? CheckPassword(string? password)
    {
        if (string.IsNullOrEmpty(password) || password.Length < MinimumPasswordLength)
            return $"Parola en az {MinimumPasswordLength} karakter olmalıdır.";
        if (password.Length > MaximumPasswordLength)
            return $"Parola en fazla {MaximumPasswordLength} karakter olabilir.";
        return null;
    }
}

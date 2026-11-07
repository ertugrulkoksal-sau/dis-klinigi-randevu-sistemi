using System.Net.Mail;

namespace DisKlinigi.Core.Domain.Users;

/// <summary>
/// Sistemdeki tüm kullanıcıların ortak tabanı. Soyut: "sadece kullanıcı" olan
/// biri yok, herkes hasta, hekim, asistan ya da yöneticidir.
/// </summary>
/// <remarks>
/// Yetki soruları (<see cref="CanManageBranch"/>, <see cref="CanManageCatalog"/>)
/// her alt sınıfta kendi kuralıyla cevaplanıyor (çok biçimlilik). Controller'lar
/// "rol şu mu?" diye bakmak yerine bu metotları çağıracak; böylece yetki kuralı
/// tek yerde, ilgili sınıfın içinde duruyor (tasarım kararı K14). Randevuya özgü
/// CanApprove/CanCancel, randevu sınıfıyla birlikte ekleniyor.
/// </remarks>
public abstract class User : Entity
{
    public string FullName { get; private set; } = null!;

    /// <summary>Giriş adı. Küçük harfe çevrilip saklanıyor, böylece "Ali@..." ile "ali@..." aynı hesap.</summary>
    public string Email { get; private set; } = null!;

    /// <summary>
    /// Parolanın tuzlu özeti (hash). Bu sınıf düz parolayı hiç görmüyor; özet,
    /// giriş adımında (11 Kasım) parola özetleyici servis tarafından üretiliyor.
    /// </summary>
    public string PasswordHash { get; private set; } = null!;

    protected User()
    {
    }

    protected User(string fullName, string email, string passwordHash)
    {
        Rename(fullName);
        ChangeEmail(email);
        ChangePasswordHash(passwordHash);
    }

    public abstract UserRole Role { get; }

    /// <summary>Bu kullanıcı verilen şubenin çalışma saatlerini, bölümlerini, ünit sayısını ve personelini yönetebilir mi?</summary>
    public abstract bool CanManageBranch(Branch branch);

    /// <summary>Zincir genelindeki bölüm ve hizmet kataloğunu değiştirebilir mi? Varsayılan cevap hayır.</summary>
    public virtual bool CanManageCatalog => false;

    public void Rename(string fullName)
    {
        if (string.IsNullOrWhiteSpace(fullName))
            throw new DomainException("Ad soyad boş olamaz.");
        FullName = fullName.Trim();
    }

    public void ChangeEmail(string email)
    {
        var normalized = email?.Trim().ToLowerInvariant() ?? string.Empty;
        // MailAddress "Ad <adres>" gibi biçimleri de kabul ettiği için çözümlenen
        // adresin girilen metinle birebir aynı olmasını da istiyorum.
        if (!MailAddress.TryCreate(normalized, out var parsed) || parsed.Address != normalized)
            throw new DomainException("Geçerli bir e-posta adresi girilmelidir.");
        Email = normalized;
    }

    public void ChangePasswordHash(string passwordHash)
    {
        if (string.IsNullOrWhiteSpace(passwordHash))
            throw new DomainException("Parola özeti boş olamaz.");
        PasswordHash = passwordHash;
    }

    public override string ToString() => FullName;
}

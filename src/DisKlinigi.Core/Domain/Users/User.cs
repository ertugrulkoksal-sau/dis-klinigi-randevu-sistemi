using System.Net.Mail;
using DisKlinigi.Core.Domain.Appointments;

namespace DisKlinigi.Core.Domain.Users;

/// <summary>
/// Sistemdeki tüm kullanıcıların ortak tabanı. Soyut: "sadece kullanıcı" olan
/// biri yok, herkes hasta, hekim, asistan ya da yöneticidir.
/// </summary>
/// <remarks>
/// Yetki soruları (<see cref="CanManageBranch"/>, <see cref="CanManageChain"/>,
/// <see cref="CanApprove"/>, <see cref="CanCancel"/>) her alt sınıfta kendi kuralıyla
/// cevaplanıyor (çok biçimlilik). Controller'lar ve randevu durumları "rol şu mu?"
/// diye bakmak yerine bu metotları çağırıyor; böylece yetki kuralı tek yerde,
/// ilgili sınıfın içinde duruyor (tasarım kararı K14).
/// </remarks>
public abstract class User : Entity
{
    public string FullName { get; private set; } = null!;

    /// <summary>Giriş adı. Küçük harfe çevrilip saklanıyor, böylece "Ali@..." ile "ali@..." aynı hesap.</summary>
    public string Email { get; private set; } = null!;

    /// <summary>
    /// Parolanın tuzlu özeti (hash). Bu sınıf düz parolayı hiç görmüyor; özeti
    /// <see cref="Abstractions.IPasswordHasher"/> üretiyor (kayıtta <c>AccountService</c>).
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

    /// <summary>
    /// Kullanıcının bağlı olduğu tek şube; asistan ve şube yöneticisinde var, diğer rollerde yok.
    /// Hekim birden fazla şubede çalışabildiği için onda da boş (şubeleri <c>Dentist.Branches</c>).
    /// Menüde "Asistan, Serdivan Şubesi" gibi gösteriliyor; onay kuyruğu ve "Şubem" ekranı da
    /// bunu kullanacak. Sadece okunur, EF Core bu özelliği sütun olarak eşlemiyor.
    /// </summary>
    public virtual Branch? AssignedBranch => null;

    /// <summary>Bu kullanıcı verilen şubenin çalışma saatlerini, bölümlerini, ünit sayısını ve personelini yönetebilir mi?</summary>
    public abstract bool CanManageBranch(Branch branch);

    /// <summary>
    /// Zincir genelindeki kayıtları değiştirebilir mi: bölüm ve hizmet kataloğu, yeni şube açma, hekim kaydı ve
    /// hekimin bütün şubeler için ortak bilgileri (uzmanlık, hizmetler, e-posta; K34). Varsayılan cevap hayır.
    /// </summary>
    /// <remarks>
    /// İlk halinde adı <c>CanManageCatalog</c>'du. Yönetici panelinde (14 Kasım) şube açma ve hekim kaydı da
    /// aynı soruyu sordu ve her rolde aynı cevabı verdi; üç ayrı özellik açmak yerine (K27'deki gerekçeyle) adını
    /// genişlettim (K54).
    /// </remarks>
    public virtual bool CanManageChain => false;

    /// <summary>
    /// Bu kullanıcı randevuyu onaylayıp reddedebilir ve sonucunu (tamamlandı/gelmedi)
    /// işleyebilir mi? Kişinin o randevuyla ilişkisine bakıyor; randevunun durumunun
    /// bu işleme izin verip vermediğini randevu ayrıca kontrol ediyor.
    /// </summary>
    public abstract bool CanApprove(Appointment appointment);

    /// <summary>
    /// Bu kullanıcı randevuyu verilen anda iptal edebilir mi? Hasta için 24 saat kuralı
    /// burada; randevunun başlamış olması ve durumu randevu tarafında kontrol ediliyor.
    /// </summary>
    public abstract bool CanCancel(Appointment appointment, DateTime now);

    public void Rename(string fullName)
    {
        if (string.IsNullOrWhiteSpace(fullName))
            throw new DomainException("Ad soyad boş olamaz.");
        FullName = fullName.Trim();
    }

    /// <summary>
    /// E-postanın saklandığı biçim: baştaki/sondaki boşluklar atılmış, küçük harf. Kayıtta ve
    /// girişte aynı metot kullanılıyor; böylece "Ali@Ornek.test " ile giriş yapan da hesabını buluyor.
    /// </summary>
    public static string NormalizeEmail(string? email) => email?.Trim().ToLowerInvariant() ?? string.Empty;

    public void ChangeEmail(string email)
    {
        var normalized = NormalizeEmail(email);
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

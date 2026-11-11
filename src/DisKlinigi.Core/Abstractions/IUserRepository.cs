using DisKlinigi.Core.Domain.Users;

namespace DisKlinigi.Core.Abstractions;

/// <summary>Kullanıcı verisine erişim: giriş, kayıt ve oturum kontrolü için gereken üç işlem (K17).</summary>
/// <remarks>
/// Dönen nesne kullanıcının gerçek sınıfı (<see cref="Patient"/>, <see cref="Dentist"/>...);
/// yetki soruları (<c>CanApprove</c>, <c>CanManageBranch</c>) bu sınıfta cevaplandığı için tür
/// kaybolmamalı. Asistan ve şube yöneticisi bağlı oldukları şubeyle birlikte yükleniyor.
/// </remarks>
public interface IUserRepository
{
    /// <summary>E-postaya göre kullanıcı; büyük/küçük harf ve baştaki/sondaki boşluk fark etmiyor.</summary>
    Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);

    Task<User?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Kullanıcıyı kaydeder. E-posta başka bir hesapta kullanılıyorsa kaydetmeden <c>false</c> döner.
    /// </summary>
    /// <remarks>
    /// "Önce var mı diye bak, sonra ekle" iki ayrı adım olsaydı, aynı anda gelen iki kayıt
    /// isteği arasında boşluk kalırdı. Gerçekleştirim bu durumda veritabanındaki benzersiz
    /// indeksin hatasını da <c>false</c>'a çeviriyor; yani kararı en sonda veritabanı veriyor.
    /// </remarks>
    Task<bool> TryAddAsync(User user, CancellationToken cancellationToken = default);
}

using DisKlinigi.Core.Abstractions;
using DisKlinigi.Core.Domain.Users;
using Microsoft.AspNetCore.Identity;

namespace DisKlinigi.Web.Security;

/// <summary>
/// Core'daki <see cref="IPasswordHasher"/> arayüzünün ASP.NET Core <see cref="PasswordHasher{TUser}"/>
/// ile gerçekleştirimi (K15). Özet PBKDF2, her parola için ayrı rastgele tuz; aynı parola iki kez
/// özetlenince farklı metin çıkıyor.
/// </summary>
/// <remarks>
/// Framework sınıfı Identity'nin kullanıcı tipini parametre olarak alıyor ama varsayılan
/// gerçekleştirim kullanıcı nesnesine hiç bakmıyor; bu yüzden null veriliyor. Identity'nin
/// geri kalanı (tablolar, ekranlar) kullanılmıyor.
/// </remarks>
public sealed class AspNetPasswordHasher : IPasswordHasher
{
    private readonly PasswordHasher<User> _inner = new();

    public string Hash(string password)
    {
        ArgumentException.ThrowIfNullOrEmpty(password);
        return _inner.HashPassword(null!, password);
    }

    /// <summary>
    /// "Doğru ama yeniden özetlenmeli" (eski algoritma) cevabı da doğru parola sayılıyor;
    /// yeniden özetleme bu projede gerekmiyor çünkü bütün özetler aynı sürümle üretiliyor.
    /// Bozuk bir özet (Base64 olmayan metin) framework'te istisna fırlatıyor; giriş sayfası
    /// çökmesin diye bu durum da "parola yanlış" sayılıyor.
    /// </summary>
    public bool Verify(string passwordHash, string password)
    {
        if (string.IsNullOrEmpty(passwordHash) || string.IsNullOrEmpty(password))
            return false;

        try
        {
            return _inner.VerifyHashedPassword(null!, passwordHash, password) != PasswordVerificationResult.Failed;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}

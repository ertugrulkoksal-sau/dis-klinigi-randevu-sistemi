using System.Security.Cryptography;
using DisKlinigi.Core.Accounts;

namespace DisKlinigi.Core.Administration;

/// <summary>
/// Yönetici personel hesabı açarken üretilen geçici parola (K36). Bir kez ekranda gösteriliyor, veritabanına
/// sadece özeti yazılıyor.
/// </summary>
/// <remarks>
/// Rastgelelik <see cref="RandomNumberGenerator"/>'dan (kriptografik üreteç); <c>Random</c> sınıfı tahmin
/// edilebilir olduğu için parola üretmeye uygun değil. Birbirine karışan karakterler (0/O, 1/l/I) alfabede yok,
/// çünkü parolayı yönetici ekrandan okuyup personele söyleyecek. 10 karakter, kayıttaki en az 8 karakter
/// kuralını (<see cref="AccountService.MinimumPasswordLength"/>) karşılıyor.
/// </remarks>
public static class TemporaryPassword
{
    public const int Length = 10;

    private const string Alphabet = "abcdefghijkmnopqrstuvwxyzABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    public static string Create() => RandomNumberGenerator.GetString(Alphabet, Length);
}

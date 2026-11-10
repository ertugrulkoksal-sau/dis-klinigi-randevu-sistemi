namespace DisKlinigi.Core.Abstractions;

/// <summary>
/// Parolayı tuzlu özete çeviren ve girilen parolayı özetle karşılaştıran servis (K15).
/// </summary>
/// <remarks>
/// Core düz parolayı saklamıyor ve özetleme algoritmasını bilmiyor; kullanıcı sınıfları
/// sadece özeti tutuyor. Gerçekleştirimi Web projesinde, ASP.NET Core'un
/// <c>PasswordHasher</c> sınıfının arkasında (PBKDF2). Örnek veri bu arayüzle özetleniyor;
/// giriş ekranı (11 Kasım) <see cref="Verify"/> ile kullanacak.
/// </remarks>
public interface IPasswordHasher
{
    string Hash(string password);

    bool Verify(string passwordHash, string password);
}

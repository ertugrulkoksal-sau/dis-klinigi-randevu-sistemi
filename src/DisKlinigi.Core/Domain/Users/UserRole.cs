namespace DisKlinigi.Core.Domain.Users;

/// <summary>
/// Kullanıcının rolü. Yetki kararları bu değere bakılarak değil, kullanıcı
/// sınıflarının kendi metotlarıyla veriliyor; bu değer sadece giriş çerezine
/// rol bilgisini yazmak ve menüyü seçmek için kullanılacak.
/// </summary>
public enum UserRole
{
    Patient,
    Dentist,
    Assistant,
    BranchManager,
    ChainAdministrator
}

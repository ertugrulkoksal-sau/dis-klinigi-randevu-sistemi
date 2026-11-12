using DisKlinigi.Core.Domain;

namespace DisKlinigi.Core.Abstractions;

/// <summary>
/// Zincir genelindeki hizmet kataloğuna erişim (K17). Bölümler şubenin içinden geliyor
/// (<see cref="Branch.Departments"/>); burada sadece hizmetler var. Katalog yönetimi
/// (14 Kasım) ekleme ve değiştirme işlemlerini bu arayüze ekleyecek.
/// </summary>
public interface ICatalogRepository
{
    /// <summary>Bütün hizmetler bölümleriyle birlikte, ada göre sıralı.</summary>
    Task<IReadOnlyList<Service>> GetServicesAsync(CancellationToken cancellationToken = default);

    Task<Service?> GetServiceByIdAsync(int id, CancellationToken cancellationToken = default);
}

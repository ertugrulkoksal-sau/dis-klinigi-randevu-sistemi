using DisKlinigi.Core.Domain;

namespace DisKlinigi.Core.Abstractions;

/// <summary>
/// Zincir genelindeki hizmet kataloğuna erişim (K17). Bölümler şubenin içinden geliyor
/// (<see cref="Branch.Departments"/>); katalog ekranı ve şube ayarları ise bölümlerin tam listesine
/// ihtiyaç duyuyor (14 Kasım).
/// </summary>
public interface ICatalogRepository
{
    /// <summary>Bütün hizmetler bölümleriyle birlikte, ada göre sıralı.</summary>
    Task<IReadOnlyList<Service>> GetServicesAsync(CancellationToken cancellationToken = default);

    Task<Service?> GetServiceByIdAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>Zincirin bütün bölümleri, ada göre sıralı. Şubeye bölüm eklenecekse buradaki nesneler kullanılıyor.</summary>
    Task<IReadOnlyList<Department>> GetDepartmentsAsync(CancellationToken cancellationToken = default);

    void AddDepartment(Department department);

    void AddService(Service service);

    /// <summary>Eklenen ya da değiştirilen bölüm ve hizmetleri kaydeder.</summary>
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

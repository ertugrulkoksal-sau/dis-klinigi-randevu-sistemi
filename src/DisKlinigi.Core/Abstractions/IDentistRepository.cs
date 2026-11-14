using DisKlinigi.Core.Domain;
using DisKlinigi.Core.Domain.Users;

namespace DisKlinigi.Core.Abstractions;

/// <summary>Hekim verisine erişim (K17).</summary>
public interface IDentistRepository
{
    /// <summary>
    /// Hekimi bütün parçalarıyla getirir: uzmanlık, verdiği hizmetler (bölümleriyle),
    /// çalıştığı şubeler (çalışma saatleri ve bölümleriyle) ve müsaitlik aralıkları.
    /// </summary>
    /// <remarks>
    /// Randevu kuralları ve müsaitlik ekleme bu parçaların hepsine bakıyor; eksik yüklenmiş
    /// bir hekim "bu hizmeti vermiyor" ya da "şube kapalı" gibi yanlış cevaplar verirdi.
    /// Bu yüzden yarım yükleme seçeneği bilerek yok.
    /// </remarks>
    Task<Dentist?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Verilen şubeye atanmış hekimler, <see cref="GetByIdAsync"/> gibi bütün parçalarıyla ve ada göre sıralı.
    /// Randevu ekranı bunları <see cref="Dentist.CanServe"/> ile seçilen hizmete göre daraltıyor.
    /// </summary>
    Task<IReadOnlyList<Dentist>> GetWorkingAtAsync(Branch branch, CancellationToken cancellationToken = default);

    /// <summary>Zincirdeki bütün hekimler bütün parçalarıyla, ada göre sıralı (hekim kaydı ekranı, şubeye hekim atama listesi).</summary>
    Task<IReadOnlyList<Dentist>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>Yüklenmiş hekimde yapılan değişiklikleri (müsaitlik aralığı, şube ataması, ortak bilgiler) kaydeder.</summary>
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

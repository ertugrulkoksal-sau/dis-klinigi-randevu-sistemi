using DisKlinigi.Core.Domain;

namespace DisKlinigi.Core.Abstractions;

/// <summary>
/// Şube verisine erişim sözleşmesi. Arayüz Core'da, gerçekleştirimi (EF Core)
/// Web projesinde duruyor: üst katman alt katmanın ayrıntısını bilmiyor (DIP).
/// </summary>
/// <remarks>
/// Şube, çalışma saatleri ve bölümleriyle birlikte tek parça olarak yükleniyor;
/// <see cref="Branch.IsOpenDuring"/> ve <see cref="Branch.HostsDepartment"/> bu listelere bakıyor.
/// </remarks>
public interface IBranchRepository
{
    /// <summary>Bütün şubeler, ile ve ada göre sıralı.</summary>
    Task<IReadOnlyList<Branch>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>Şubeyi çalışma saatleri ve bölümleriyle getirir; değişiklik kaydedilebilsin diye izlenen nesne.</summary>
    Task<Branch?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>Yeni şubeyi kaydedilmek üzere ekler (yönetici paneli, "Yeni şube").</summary>
    void Add(Branch branch);

    /// <summary>Eklenen ya da yüklenip değiştirilen şubeleri kaydeder.</summary>
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

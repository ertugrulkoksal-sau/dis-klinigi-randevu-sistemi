using DisKlinigi.Core.Domain;

namespace DisKlinigi.Core.Abstractions;

/// <summary>
/// Şube verisine erişim sözleşmesi. Arayüz Core'da, gerçekleştirimi (EF Core)
/// Web projesinde duruyor: üst katman alt katmanın ayrıntısını bilmiyor (DIP).
/// </summary>
public interface IBranchRepository
{
    Task<IReadOnlyList<Branch>> GetAllAsync(CancellationToken cancellationToken = default);
}

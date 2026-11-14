using DisKlinigi.Core.Abstractions;
using DisKlinigi.Core.Domain;
using Microsoft.EntityFrameworkCore;

namespace DisKlinigi.Web.Data.Repositories;

public class EfBranchRepository : IBranchRepository
{
    private readonly AppDbContext _db;

    public EfBranchRepository(AppDbContext db)
    {
        _db = db;
    }

    /// <summary>Liste ekranı için; değiştirilmeyeceği için izlenmiyor (AsNoTracking).</summary>
    public async Task<IReadOnlyList<Branch>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await WithParts(_db.Branches.AsNoTracking())
            .OrderBy(b => b.City).ThenBy(b => b.Name)
            .ToListAsync(cancellationToken);
    }

    public Task<Branch?> GetByIdAsync(int id, CancellationToken cancellationToken = default) =>
        WithParts(_db.Branches).FirstOrDefaultAsync(b => b.Id == id, cancellationToken);

    public void Add(Branch branch)
    {
        ArgumentNullException.ThrowIfNull(branch);
        _db.Branches.Add(branch);
    }

    /// <remarks>
    /// Kapatılan günün kaydı şubenin listesinden çıkınca EF Core onu sahipsiz kayıt olarak siliyor
    /// (<c>BranchConfiguration</c>, sahibine ait tablo); ayrıca silme komutu gerekmiyor.
    /// </remarks>
    public Task SaveChangesAsync(CancellationToken cancellationToken = default) => _db.SaveChangesAsync(cancellationToken);

    /// <summary>Şube her zaman çalışma saatleri ve bölümleriyle birlikte yükleniyor.</summary>
    private static IQueryable<Branch> WithParts(IQueryable<Branch> query) =>
        query
            .Include(b => b.WorkingHours)
            .Include(b => b.Departments)
            .AsSplitQuery();
}

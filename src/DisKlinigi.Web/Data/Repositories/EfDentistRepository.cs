using DisKlinigi.Core.Abstractions;
using DisKlinigi.Core.Domain;
using DisKlinigi.Core.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace DisKlinigi.Web.Data.Repositories;

public class EfDentistRepository : IDentistRepository
{
    private readonly AppDbContext _db;

    public EfDentistRepository(AppDbContext db)
    {
        _db = db;
    }

    /// <remarks>
    /// Müsaitlik aralıklarının şubesi, hekimin şubeler listesindeki nesnenin aynısı
    /// (EF Core aynı satırı aynı nesneye bağlıyor); bu yüzden şubenin çalışma saatleri ve
    /// bölümleri bir kez yükleniyor. Dört koleksiyon tek sorguda birleştirilseydi satır sayısı
    /// çarpılırdı (hizmet × şube × aralık); <c>AsSplitQuery</c> her koleksiyonu ayrı sorguyla getiriyor.
    /// </remarks>
    public Task<Dentist?> GetByIdAsync(int id, CancellationToken cancellationToken = default) =>
        WithParts(_db.Dentists).FirstOrDefaultAsync(d => d.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Dentist>> GetWorkingAtAsync(Branch branch, CancellationToken cancellationToken = default)
    {
        var branchId = branch.Id;
        return await WithParts(_db.Dentists)
            .Where(d => d.Branches.Any(b => b.Id == branchId))
            .OrderBy(d => d.FullName)
            .ToListAsync(cancellationToken);
    }

    /// <remarks>
    /// Silinen müsaitlik aralığı hekimin listesinden çıkınca EF Core onu sahipsiz kayıt olarak görüp
    /// siliyor (ilişki zorunlu ve <c>Cascade</c>, <c>UserConfiguration</c>); ayrıca silme komutu gerekmiyor.
    /// </remarks>
    public Task SaveChangesAsync(CancellationToken cancellationToken = default) => _db.SaveChangesAsync(cancellationToken);

    /// <summary>Hekim her zaman bütün parçalarıyla yükleniyor (bkz. <see cref="IDentistRepository.GetByIdAsync"/>).</summary>
    private static IQueryable<Dentist> WithParts(IQueryable<Dentist> query) =>
        query
            .Include(d => d.Specialty)
            .Include(d => d.Services).ThenInclude(s => s.Department)
            .Include(d => d.Branches).ThenInclude(b => b.WorkingHours)
            .Include(d => d.Branches).ThenInclude(b => b.Departments)
            .Include(d => d.Availability).ThenInclude(a => a.Branch)
            .AsSplitQuery();
}

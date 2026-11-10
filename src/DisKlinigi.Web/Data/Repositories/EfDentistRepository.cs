using DisKlinigi.Core.Abstractions;
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
        _db.Dentists
            .Include(d => d.Specialty)
            .Include(d => d.Services).ThenInclude(s => s.Department)
            .Include(d => d.Branches).ThenInclude(b => b.WorkingHours)
            .Include(d => d.Branches).ThenInclude(b => b.Departments)
            .Include(d => d.Availability).ThenInclude(a => a.Branch)
            .AsSplitQuery()
            .FirstOrDefaultAsync(d => d.Id == id, cancellationToken);
}

using DisKlinigi.Core.Abstractions;
using DisKlinigi.Core.Domain;
using Microsoft.EntityFrameworkCore;

namespace DisKlinigi.Web.Data.Repositories;

public class EfCatalogRepository : ICatalogRepository
{
    private readonly AppDbContext _db;

    public EfCatalogRepository(AppDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<Service>> GetServicesAsync(CancellationToken cancellationToken = default) =>
        await _db.Services
            .Include(s => s.Department)
            .OrderBy(s => s.Name)
            .ToListAsync(cancellationToken);

    public Task<Service?> GetServiceByIdAsync(int id, CancellationToken cancellationToken = default) =>
        _db.Services
            .Include(s => s.Department)
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
}

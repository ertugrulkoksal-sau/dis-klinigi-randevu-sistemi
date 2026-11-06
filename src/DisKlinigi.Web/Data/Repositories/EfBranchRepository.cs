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

    public async Task<IReadOnlyList<Branch>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _db.Branches
            .AsNoTracking()
            .OrderBy(b => b.City).ThenBy(b => b.Name)
            .ToListAsync(cancellationToken);
    }
}

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

    /// <summary>İzleniyor: şubeye eklenecek bölüm nesneleri bunlar, adı da buradan değiştiriliyor.</summary>
    public async Task<IReadOnlyList<Department>> GetDepartmentsAsync(CancellationToken cancellationToken = default) =>
        await _db.Departments.OrderBy(d => d.Name).ToListAsync(cancellationToken);

    public void AddDepartment(Department department)
    {
        ArgumentNullException.ThrowIfNull(department);
        _db.Departments.Add(department);
    }

    public void AddService(Service service)
    {
        ArgumentNullException.ThrowIfNull(service);
        _db.Services.Add(service);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) => _db.SaveChangesAsync(cancellationToken);
}

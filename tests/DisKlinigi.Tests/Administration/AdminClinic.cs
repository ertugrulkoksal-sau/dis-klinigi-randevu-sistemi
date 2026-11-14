using DisKlinigi.Core.Abstractions;
using DisKlinigi.Core.Administration;
using DisKlinigi.Core.Domain;
using DisKlinigi.Core.Domain.Users;
using DisKlinigi.Tests.Accounts;
using DisKlinigi.Tests.Availability;
using DisKlinigi.Tests.Data;
using DisKlinigi.Tests.Domain.Appointments;

namespace DisKlinigi.Tests.Administration;

/// <summary>
/// Yönetici paneli servis testleri için klinik: <see cref="TestClinic"/>'teki kişiler ve şubeler, ekranın
/// formdan gönderdiği gibi Id'leriyle. Serdivan hafta içi 09:00–18:00 ve Cumartesi 09:00–14:00, Kadıköy hafta
/// içi 09:00–18:00 açık; Dt. Ayşe Kaya'nın Serdivan'da Salı 09:00–13:00 müsaitlik aralığı var.
/// </summary>
internal sealed class AdminClinic
{
    public TestClinic Clinic { get; } = new();
    public Department Endodontics => Clinic.CanalTreatment.Department;
    public Department General => Clinic.Examination.Department;

    public InMemoryBranchRepository Branches { get; }
    public InMemoryCatalogRepository Catalog { get; }
    public InMemoryDentistRepository Dentists { get; }
    public InMemoryUserRepository Users { get; }
    public FakePasswordHasher Hasher { get; } = new();

    public AdminClinic()
    {
        var c = Clinic;
        TestIds.Set(c.Serdivan, 1);
        TestIds.Set(c.Kadikoy, 2);
        TestIds.Set(Endodontics, 1);
        TestIds.Set(General, 2);
        TestIds.Set(c.CanalTreatment, 1);
        TestIds.Set(c.Examination, 2);
        var id = 10;
        foreach (var user in new User[] { c.Ayse, c.Mehmet, c.Ali, c.Zeynep, c.SerdivanAssistant, c.KadikoyAssistant, c.SerdivanManager, c.KadikoyManager, c.Admin })
            TestIds.Set(user, id++);

        foreach (var day in new[] { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday })
        {
            c.Serdivan.SetWorkingHours(day, TimeOfDayRange.Of(9, 0, 18, 0));
            c.Kadikoy.SetWorkingHours(day, TimeOfDayRange.Of(9, 0, 18, 0));
        }
        c.Serdivan.SetWorkingHours(DayOfWeek.Saturday, TimeOfDayRange.Of(9, 0, 14, 0));
        c.Serdivan.AddDepartment(Endodontics);
        c.Serdivan.AddDepartment(General);
        c.Kadikoy.AddDepartment(General);

        c.Ayse.OfferService(c.CanalTreatment);
        c.Mehmet.OfferService(c.Examination);
        c.Ayse.AddAvailability(c.Serdivan, DayOfWeek.Tuesday, TimeOfDayRange.Of(9, 0, 13, 0));

        Branches = new InMemoryBranchRepository(c.Serdivan, c.Kadikoy);
        Catalog = new InMemoryCatalogRepository([Endodontics, General], [c.CanalTreatment, c.Examination]);
        Dentists = new InMemoryDentistRepository(c.Ayse, c.Mehmet);
        Users = new InMemoryUserRepository(c.Ayse, c.Mehmet, c.Ali, c.Zeynep, c.SerdivanAssistant, c.KadikoyAssistant, c.SerdivanManager, c.KadikoyManager, c.Admin);
    }

    public BranchAdministrationService BranchService() => new(Branches, Dentists, Catalog);

    public CatalogService CatalogService() => new(Catalog);

    public StaffService StaffService() => new(Users, Dentists, Branches, Catalog, Hasher);

    /// <summary>Şubenin şu anki halini form gibi verir; testler sadece değiştirmek istediği alanı değiştiriyor.</summary>
    public static BranchSettings SettingsOf(Branch branch) => new(
        branch.Name,
        branch.City,
        branch.Address,
        branch.ChairCount,
        Enum.GetValues<DayOfWeek>().Select(day => Row(day, branch.GetWorkingHours(day))).ToList(),
        branch.Departments.Select(d => d.Id).ToList());

    /// <summary>Aynı ayarlar, bir günün satırı değiştirilmiş (saat boşsa o gün kapalı).</summary>
    public static BranchSettings WithDay(BranchSettings settings, DayOfWeek day, TimeOnly? opens, TimeOnly? closes) =>
        settings with { Hours = settings.Hours.Select(d => d.Day == day ? new DayHours(day, opens, closes) : d).ToList() };

    private static DayHours Row(DayOfWeek day, TimeOfDayRange? hours) => new(day, hours?.Start, hours?.End);
}

/// <summary>Id'nin ayarlayıcısı korumalı; testte veritabanının yapacağı gibi yansımayla veriliyor.</summary>
internal static class TestIds
{
    public static void Set(Entity entity, int id) =>
        typeof(Entity).GetProperty(nameof(Entity.Id))!.SetValue(entity, id);
}

internal sealed class InMemoryBranchRepository : IBranchRepository
{
    private readonly List<Branch> _branches;

    public InMemoryBranchRepository(params Branch[] branches)
    {
        _branches = [.. branches];
    }

    public int SaveCount { get; private set; }

    public Task<IReadOnlyList<Branch>> GetAllAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Branch>>(_branches.OrderBy(b => b.City).ThenBy(b => b.Name).ToList());

    public Task<Branch?> GetByIdAsync(int id, CancellationToken cancellationToken = default) =>
        Task.FromResult(_branches.FirstOrDefault(b => b.Id == id));

    public void Add(Branch branch) => _branches.Add(branch);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        SaveCount++;
        foreach (var branch in _branches.Where(b => b.Id == 0))
            TestIds.Set(branch, _branches.Max(b => b.Id) + 1);
        return Task.CompletedTask;
    }
}

internal sealed class InMemoryCatalogRepository : ICatalogRepository
{
    private readonly List<Department> _departments;
    private readonly List<Service> _services;

    public InMemoryCatalogRepository(IEnumerable<Department> departments, IEnumerable<Service> services)
    {
        _departments = [.. departments];
        _services = [.. services];
    }

    public int SaveCount { get; private set; }

    public IReadOnlyList<Department> Departments => _departments;

    public Task<IReadOnlyList<Service>> GetServicesAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Service>>(_services.OrderBy(s => s.Name).ToList());

    public Task<Service?> GetServiceByIdAsync(int id, CancellationToken cancellationToken = default) =>
        Task.FromResult(_services.FirstOrDefault(s => s.Id == id));

    public Task<IReadOnlyList<Department>> GetDepartmentsAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Department>>(_departments.OrderBy(d => d.Name).ToList());

    public void AddDepartment(Department department) => _departments.Add(department);

    public void AddService(Service service) => _services.Add(service);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        SaveCount++;
        foreach (var department in _departments.Where(d => d.Id == 0))
            TestIds.Set(department, _departments.Max(d => d.Id) + 1);
        foreach (var service in _services.Where(s => s.Id == 0))
            TestIds.Set(service, _services.Max(s => s.Id) + 1);
        return Task.CompletedTask;
    }
}

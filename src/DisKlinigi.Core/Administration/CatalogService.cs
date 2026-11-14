using DisKlinigi.Core.Abstractions;
using DisKlinigi.Core.Domain;
using DisKlinigi.Core.Domain.Users;

namespace DisKlinigi.Core.Administration;

/// <summary>Hizmet formunun (ekran 5b) alanları; süre dakika olarak.</summary>
public sealed record ServiceDraft(string Name, int DepartmentId, int DurationMinutes, decimal Price, bool RequiresApproval);

/// <summary>
/// Zincir genelindeki bölüm ve hizmet kataloğu (ekran 5b). Okumak bütün yöneticilere açık, değiştirmek sadece
/// <see cref="User.CanManageChain"/>'in evet dediği kullanıcıya (zincir yöneticisi); şube yöneticisi ekranı salt
/// okunur görüyor.
/// </summary>
/// <remarks>
/// Süre, ücret ve ad kuralları <see cref="Service"/> ve <see cref="Department"/> sınıflarında (K25); bu servis
/// onları tekrar yazmıyor. Buraya kalan tek kural aynı adın ikinci kez kullanılmaması: veritabanında benzersiz
/// indeks var (K37), ama indeksin hatası kullanıcıya anlamlı bir mesaj vermediği için önce burada bakılıyor.
/// Ücret değiştiğinde eski randevular etkilenmiyor, çünkü randevu kendi ücretinin kopyasını saklıyor (K9).
/// </remarks>
public sealed class CatalogService
{
    private readonly ICatalogRepository _catalog;

    public CatalogService(ICatalogRepository catalog)
    {
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
    }

    public Task<IReadOnlyList<Department>> GetDepartmentsAsync(CancellationToken cancellationToken = default) =>
        _catalog.GetDepartmentsAsync(cancellationToken);

    public Task<IReadOnlyList<Service>> GetServicesAsync(CancellationToken cancellationToken = default) =>
        _catalog.GetServicesAsync(cancellationToken);

    public async Task<CreateResult<Department>> AddDepartmentAsync(User user, string? name, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);
        if (!user.CanManageChain)
            return CreateResult<Department>.Refused(ReadOnlyMessage);

        var departments = await _catalog.GetDepartmentsAsync(cancellationToken);
        if (departments.Any(d => TurkishNames.SameName(d.Name, name)))
            return CreateResult<Department>.Refused($"\"{name!.Trim()}\" adında bir bölüm var.");

        Department department;
        try
        {
            department = new Department(name!);
        }
        catch (DomainException ex)
        {
            return CreateResult<Department>.Refused(ex.Message);
        }

        _catalog.AddDepartment(department);
        await _catalog.SaveChangesAsync(cancellationToken);
        return CreateResult<Department>.Done(department);
    }

    public async Task<ChangeResult> RenameDepartmentAsync(User user, int departmentId, string? name, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);
        if (!user.CanManageChain)
            return ChangeResult.Refused(ReadOnlyMessage);

        var departments = await _catalog.GetDepartmentsAsync(cancellationToken);
        var department = departments.FirstOrDefault(d => d.Id == departmentId);
        if (department is null)
            return ChangeResult.Missing;
        if (departments.Any(d => !d.IsSameAs(department) && TurkishNames.SameName(d.Name, name)))
            return ChangeResult.Refused($"\"{name!.Trim()}\" adında başka bir bölüm var.");

        try
        {
            department.Rename(name!);
        }
        catch (DomainException ex)
        {
            return ChangeResult.Refused(ex.Message);
        }

        await _catalog.SaveChangesAsync(cancellationToken);
        return ChangeResult.Done;
    }

    /// <summary>
    /// Hizmet ekler (<paramref name="serviceId"/> boşsa) ya da değiştirir. Alan sınıfının kuralları önce yeni bir
    /// nesneyle deneniyor; bir kural tutmazsa var olan hizmet hiç değişmiyor.
    /// </summary>
    public async Task<CreateResult<Service>> SaveServiceAsync(
        User user, int? serviceId, ServiceDraft draft, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(draft);
        if (!user.CanManageChain)
            return CreateResult<Service>.Refused(ReadOnlyMessage);

        var departments = await _catalog.GetDepartmentsAsync(cancellationToken);
        var department = departments.FirstOrDefault(d => d.Id == draft.DepartmentId);
        if (department is null)
            return CreateResult<Service>.Refused("Listeden bir bölüm seçin.");

        var services = await _catalog.GetServicesAsync(cancellationToken);
        var service = serviceId is { } id ? services.FirstOrDefault(s => s.Id == id) : null;
        if (serviceId is not null && service is null)
            return CreateResult<Service>.Refused("Hizmet bulunamadı.");

        if (services.Any(s => !s.IsSameAs(service) && s.Department.IsSameAs(department) && TurkishNames.SameName(s.Name, draft.Name)))
            return CreateResult<Service>.Refused($"{department.Name} bölümünde \"{draft.Name.Trim()}\" adında bir hizmet var.");

        Service checkedDraft;
        try
        {
            checkedDraft = new Service(draft.Name, department, TimeSpan.FromMinutes(draft.DurationMinutes), draft.Price, draft.RequiresApproval);
        }
        catch (DomainException ex)
        {
            return CreateResult<Service>.Refused(ex.Message);
        }

        if (service is null)
        {
            _catalog.AddService(checkedDraft);
            service = checkedDraft;
        }
        else
        {
            service.Rename(checkedDraft.Name);
            service.MoveTo(department);
            service.ChangeDuration(checkedDraft.Duration);
            service.ChangePrice(checkedDraft.Price);
            service.SetApprovalRequirement(checkedDraft.RequiresApproval);
        }

        await _catalog.SaveChangesAsync(cancellationToken);
        return CreateResult<Service>.Done(service);
    }

    private const string ReadOnlyMessage = "Bölüm ve hizmet kataloğunu sadece zincir yöneticisi değiştirebilir.";
}

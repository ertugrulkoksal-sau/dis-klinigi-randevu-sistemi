using DisKlinigi.Core.Abstractions;
using DisKlinigi.Core.Domain;
using DisKlinigi.Core.Domain.Users;

namespace DisKlinigi.Core.Administration;

/// <summary>Formdaki bir günün satırı; <c>Opens</c> ya da <c>Closes</c> boşsa şube o gün kapalı.</summary>
public sealed record DayHours(DayOfWeek Day, TimeOnly? Opens, TimeOnly? Closes)
{
    public bool IsOpen => Opens is not null && Closes is not null;
}

/// <summary>Şube ayarları formunun (ekran 5a) bütün alanları; formda listede olmayan gün kapalı sayılıyor.</summary>
public sealed record BranchSettings(
    string Name,
    string City,
    string? Address,
    int ChairCount,
    IReadOnlyList<DayHours> Hours,
    IReadOnlyCollection<int> DepartmentIds);

/// <summary>
/// Şube bilgileri, çalışma saatleri ve barındırılan bölümler (ekran 5a); yeni şube açma.
/// </summary>
/// <remarks>
/// Şube yöneticisi sadece kendi şubesini görüyor ve değiştiriyor. Bu kural burada ayrıca yazılmadı:
/// liste ve kayıt <see cref="User.CanManageBranch"/>'in cevabıyla süzülüyor (K3, K41). Başka şubenin
/// numarası denenirse cevap "bulunamadı" (K46'daki gerekçeyle).
///
/// Asıl iş kuralı çalışma saati daraltılırken: hekimin bu şubedeki bir müsaitlik aralığı yeni saatin dışında
/// kalıyorsa kayıt reddediliyor (K24). Şube sınıfı hekimlerin aralıklarını bilmediği için bu kontrol alan
/// sınıfında olamazdı; aralık eklerken kullanılan soru (<see cref="TimeOfDayRange.Contains"/>) burada da aynı.
/// </remarks>
public sealed class BranchAdministrationService
{
    private readonly IBranchRepository _branches;
    private readonly IDentistRepository _dentists;
    private readonly ICatalogRepository _catalog;

    public BranchAdministrationService(IBranchRepository branches, IDentistRepository dentists, ICatalogRepository catalog)
    {
        _branches = branches ?? throw new ArgumentNullException(nameof(branches));
        _dentists = dentists ?? throw new ArgumentNullException(nameof(dentists));
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
    }

    /// <summary>Kullanıcının yönetebildiği şubeler: zincir yöneticisinde hepsi, şube yöneticisinde sadece kendi şubesi.</summary>
    public async Task<IReadOnlyList<Branch>> GetManageableAsync(User user, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);
        var all = await _branches.GetAllAsync(cancellationToken);
        return all.Where(user.CanManageBranch).ToList();
    }

    /// <summary>
    /// Şubenin bilgilerini, haftalık saatlerini ve bölümlerini formdaki haliyle değiştirir. Önce bütün kontroller
    /// yapılıyor, sonra değişiklik uygulanıyor; bir kural tutmazsa şube hiç değişmiyor ve hiçbir şey kaydedilmiyor.
    /// </summary>
    public async Task<ChangeResult> UpdateAsync(
        User user, int branchId, BranchSettings settings, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(settings);

        var branch = await _branches.GetByIdAsync(branchId, cancellationToken);
        if (branch is null || !user.CanManageBranch(branch))
            return ChangeResult.Missing;

        // 1) Günlük saatler geçerli mi (bitiş başlangıçtan sonra)? Hata mesajı alan sınıfından, başına gün adı ekleniyor.
        var hours = new Dictionary<DayOfWeek, TimeOfDayRange>();
        foreach (var day in settings.Hours.Where(d => d.IsOpen))
        {
            try
            {
                hours[day.Day] = new TimeOfDayRange(day.Opens!.Value, day.Closes!.Value);
            }
            catch (DomainException ex)
            {
                return ChangeResult.Refused($"{TurkishNames.Day(day.Day)}: {ex.Message}");
            }
        }

        // 2) Hekimlerin bu şubedeki aralıkları yeni saatlere sığıyor mu (K24)?
        var outside = await WindowsOutsideAsync(branch, hours, cancellationToken);
        if (outside.Count > 0)
            return ChangeResult.Refused(OutsideMessage(branch, hours, outside));

        // 3) Seçilen bölümler katalogda var mı? Adres elle değiştirilse de olmayan bölüm eklenmiyor.
        var catalog = await _catalog.GetDepartmentsAsync(cancellationToken);
        var departments = catalog.Where(d => settings.DepartmentIds.Contains(d.Id)).ToList();
        if (departments.Count != settings.DepartmentIds.Distinct().Count())
            return ChangeResult.Refused("Seçilen bölümlerden biri katalogda bulunamadı; sayfayı yenileyip tekrar deneyin.");

        // 4) Ad başka şubede kullanılıyor mu?
        if (await NameTakenAsync(settings.Name, branch, cancellationToken))
            return ChangeResult.Refused($"\"{settings.Name.Trim()}\" adında başka bir şube var.");

        // Ad, il ve ünit sayısının kuralları şube sınıfında. Önce yeni bir şube nesnesiyle denenip asıl şubeye
        // ancak hepsi geçerse uygulanıyor; böylece yarısı değişmiş bir şube oluşamıyor.
        try
        {
            _ = new Branch(settings.Name, settings.City, settings.Address ?? "", settings.ChairCount);
        }
        catch (DomainException ex)
        {
            return ChangeResult.Refused(ex.Message);
        }

        branch.Rename(settings.Name);
        branch.Relocate(settings.City, settings.Address ?? "");
        branch.ChangeChairCount(settings.ChairCount);
        foreach (var day in Enum.GetValues<DayOfWeek>())
        {
            if (hours.TryGetValue(day, out var range))
                branch.SetWorkingHours(day, range);
            else
                branch.CloseOn(day);
        }
        foreach (var department in branch.Departments.Where(d => !departments.Any(d.IsSameAs)).ToList())
            branch.RemoveDepartment(department);
        foreach (var department in departments)
            branch.AddDepartment(department);

        await _branches.SaveChangesAsync(cancellationToken);
        return ChangeResult.Done;
    }

    /// <summary>
    /// Yeni şube açar; sadece zincir geneli kayıtları yönetebilen kullanıcı (<see cref="User.CanManageChain"/>).
    /// Şube bütün günler kapalı ve bölümsüz açılıyor; saatler ve bölümler ardından şube formundan seçiliyor.
    /// </summary>
    public async Task<CreateResult<Branch>> CreateAsync(
        User user, string name, string city, string? address, int chairCount, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);
        if (!user.CanManageChain)
            return CreateResult<Branch>.Refused("Yeni şubeyi sadece zincir yöneticisi açabilir.");

        if (await NameTakenAsync(name, null, cancellationToken))
            return CreateResult<Branch>.Refused($"\"{name.Trim()}\" adında başka bir şube var.");

        Branch branch;
        try
        {
            branch = new Branch(name, city, address ?? "", chairCount);
        }
        catch (DomainException ex)
        {
            return CreateResult<Branch>.Refused(ex.Message);
        }

        _branches.Add(branch);
        await _branches.SaveChangesAsync(cancellationToken);
        return CreateResult<Branch>.Done(branch);
    }

    /// <summary>Hekimlerin bu şubedeki, yeni saatlere tamamen sığmayan aralıkları (gün kapandıysa o günün bütün aralıkları).</summary>
    private async Task<IReadOnlyList<(Dentist Dentist, AvailabilityWindow Window)>> WindowsOutsideAsync(
        Branch branch, IReadOnlyDictionary<DayOfWeek, TimeOfDayRange> hours, CancellationToken cancellationToken)
    {
        var dentists = await _dentists.GetWorkingAtAsync(branch, cancellationToken);
        return dentists
            .SelectMany(d => d.Availability.Where(w => w.Branch.IsSameAs(branch)).Select(w => (Dentist: d, Window: w)))
            .Where(x => !hours.TryGetValue(x.Window.Day, out var open) || !open.Contains(x.Window.Hours))
            .OrderBy(x => ((int)x.Window.Day + 6) % 7)
            .ThenBy(x => x.Window.Hours.Start)
            .ThenBy(x => x.Dentist.FullName, TurkishNames.Comparer)
            .ToList();
    }

    private static string OutsideMessage(
        Branch branch, IReadOnlyDictionary<DayOfWeek, TimeOfDayRange> hours, IReadOnlyList<(Dentist Dentist, AvailabilityWindow Window)> outside)
    {
        var (dentist, window) = outside[0];
        var newHours = hours.TryGetValue(window.Day, out var open) ? $"yeni saatin ({open})" : "kapalı günün";
        var more = outside.Count > 1 ? $" Toplam {outside.Count} aralık dışarıda kalıyor." : "";
        return $"{dentist.FullName} için {TurkishNames.Day(window.Day)} {window.Hours} aralığı {newHours} dışında kalıyor; " +
               $"önce hekimin bu aralığı değiştirmesi ya da silmesi gerekiyor.{more}";
    }

    private async Task<bool> NameTakenAsync(string? name, Branch? except, CancellationToken cancellationToken)
    {
        var all = await _branches.GetAllAsync(cancellationToken);
        return all.Any(b => !b.IsSameAs(except) && TurkishNames.SameName(b.Name, name));
    }
}

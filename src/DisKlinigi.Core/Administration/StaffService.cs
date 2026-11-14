using DisKlinigi.Core.Abstractions;
using DisKlinigi.Core.Domain;
using DisKlinigi.Core.Domain.Users;

namespace DisKlinigi.Core.Administration;

/// <summary>Şube personeli ekranının (5c) içeriği.</summary>
/// <param name="Dentists">Şubeye atanmış hekimler.</param>
/// <param name="Assistants">Şubeye bağlı asistanlar.</param>
/// <param name="Assignable">Zincirde kayıtlı olup bu şubede çalışmayan hekimler ("Şubeye hekim ata" listesi).</param>
public sealed record BranchStaff(
    Branch Branch,
    IReadOnlyList<Dentist> Dentists,
    IReadOnlyList<Assistant> Assistants,
    IReadOnlyList<Dentist> Assignable);

/// <summary>Açılan personel hesabı ve bir kez gösterilecek geçici parolası (K36).</summary>
public sealed record StaffAccount(User User, string TemporaryPassword);

/// <summary>Hekim formunun (ekran 5d) alanları: hekimin bütün şubeler için ortak bilgileri ve çalıştığı şubeler.</summary>
public sealed record DentistDraft(
    string FullName,
    string Email,
    int SpecialtyId,
    IReadOnlyCollection<int> ServiceIds,
    IReadOnlyCollection<int> BranchIds);

/// <summary>
/// Personel yönetimi: şube personeli, şubeye hekim atama/çıkarma, asistan hesabı (ekran 5c) ve hekim kaydı (5d).
/// </summary>
/// <remarks>
/// Yetki iki soruya ayrılıyor (K34): şubeye hekim atamak, çıkarmak ve asistan açmak o şubeyi yönetmek demek
/// (<see cref="User.CanManageBranch"/>; şube yöneticisi sadece kendi şubesinde). Hekimin uzmanlığı, verdiği
/// hizmetler ve e-postası ise bütün şubeleri etkilediği için zincir geneli bir kayıt
/// (<see cref="User.CanManageChain"/>; sadece zincir yöneticisi). İkisi de rol kontrolü değil, kullanıcı
/// sınıflarının çok biçimli cevabı.
///
/// "Müsaitlik aralığı olan şubeden hekim çıkarılamaz" kuralı hekim sınıfında (<see cref="Dentist.RemoveFromBranch"/>,
/// K24); servis alan sınıfının mesajını ekrana taşıyor. Aralık da ileri tarihte randevusu varken silinemediği
/// için (K52) bu iki kural birlikte "randevusu olan hekim şubeden çıkarılamaz" sonucunu veriyor.
/// </remarks>
public sealed class StaffService
{
    private readonly IUserRepository _users;
    private readonly IDentistRepository _dentists;
    private readonly IBranchRepository _branches;
    private readonly ICatalogRepository _catalog;
    private readonly IPasswordHasher _hasher;

    public StaffService(
        IUserRepository users, IDentistRepository dentists, IBranchRepository branches, ICatalogRepository catalog, IPasswordHasher hasher)
    {
        _users = users ?? throw new ArgumentNullException(nameof(users));
        _dentists = dentists ?? throw new ArgumentNullException(nameof(dentists));
        _branches = branches ?? throw new ArgumentNullException(nameof(branches));
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _hasher = hasher ?? throw new ArgumentNullException(nameof(hasher));
    }

    // ---- Şube personeli (5c) ----

    /// <summary>Şubenin hekim ve asistanları; kullanıcı bu şubeyi yönetemiyorsa <c>null</c> (bulunamadı).</summary>
    public async Task<BranchStaff?> GetBranchStaffAsync(User user, int branchId, CancellationToken cancellationToken = default)
    {
        var branch = await ManagedBranchAsync(user, branchId, cancellationToken);
        if (branch is null)
            return null;

        var all = await _dentists.GetAllAsync(cancellationToken);
        return new BranchStaff(
            branch,
            all.Where(d => d.WorksAt(branch)).ToList(),
            await _users.GetAssistantsAtAsync(branch, cancellationToken),
            all.Where(d => !d.WorksAt(branch)).ToList());
    }

    /// <summary>Kayıtlı bir hekimi şubeye atar. Hekim bu şube için müsaitlik aralıklarını ardından kendisi giriyor.</summary>
    public async Task<ChangeResult> AssignDentistAsync(User user, int branchId, int dentistId, CancellationToken cancellationToken = default)
    {
        var branch = await ManagedBranchAsync(user, branchId, cancellationToken);
        var dentist = await _dentists.GetByIdAsync(dentistId, cancellationToken);
        if (branch is null || dentist is null)
            return ChangeResult.Missing;

        dentist.AssignToBranch(branch);
        await _dentists.SaveChangesAsync(cancellationToken);
        return ChangeResult.Done;
    }

    /// <summary>Hekimi şubeden çıkarır; bu şubede müsaitlik aralığı varsa hekim sınıfı reddediyor.</summary>
    public async Task<ChangeResult> RemoveDentistAsync(User user, int branchId, int dentistId, CancellationToken cancellationToken = default)
    {
        var branch = await ManagedBranchAsync(user, branchId, cancellationToken);
        var dentist = await _dentists.GetByIdAsync(dentistId, cancellationToken);
        if (branch is null || dentist is null || !dentist.WorksAt(branch))
            return ChangeResult.Missing;

        try
        {
            dentist.RemoveFromBranch(branch);
        }
        catch (DomainException ex)
        {
            return ChangeResult.Refused(ex.Message);
        }

        await _dentists.SaveChangesAsync(cancellationToken);
        return ChangeResult.Done;
    }

    /// <summary>Şube için asistan hesabı açar; geçici parola bir kez dönüyor, kayıtta sadece özeti var (K15, K36).</summary>
    public async Task<CreateResult<StaffAccount>> CreateAssistantAsync(
        User user, int branchId, string fullName, string email, CancellationToken cancellationToken = default)
    {
        var branch = await ManagedBranchAsync(user, branchId, cancellationToken);
        if (branch is null)
            return CreateResult<StaffAccount>.Refused("Şube bulunamadı.");

        var password = TemporaryPassword.Create();
        Assistant assistant;
        try
        {
            assistant = new Assistant(fullName, email, _hasher.Hash(password), branch);
        }
        catch (DomainException ex)
        {
            return CreateResult<StaffAccount>.Refused(ex.Message);
        }

        if (!await _users.TryAddAsync(assistant, cancellationToken))
            return CreateResult<StaffAccount>.Refused(EmailTaken);

        return CreateResult<StaffAccount>.Done(new StaffAccount(assistant, password));
    }

    // ---- Hekim kaydı (5d) ----

    /// <summary>Zincirdeki bütün hekimler; sadece zincir geneli kayıtları yönetebilen kullanıcıya.</summary>
    public async Task<IReadOnlyList<Dentist>> GetDentistsAsync(User user, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);
        return user.CanManageChain
            ? (await _dentists.GetAllAsync(cancellationToken)).OrderBy(d => d.FullName, TurkishNames.Comparer).ToList()
            : [];
    }

    /// <summary>
    /// Yeni hekim kaydı açar: ortak bilgiler, verdiği hizmetler ve çalışacağı şubeler. Geçici parola bir kez dönüyor.
    /// </summary>
    public async Task<CreateResult<StaffAccount>> RegisterDentistAsync(
        User user, DentistDraft draft, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(draft);
        var parts = await ResolveAsync(user, draft, cancellationToken);
        if (parts.Error is { } error)
            return CreateResult<StaffAccount>.Refused(error);

        var password = TemporaryPassword.Create();
        Dentist dentist;
        try
        {
            dentist = new Dentist(draft.FullName, draft.Email, _hasher.Hash(password), parts.Specialty!);
        }
        catch (DomainException ex)
        {
            return CreateResult<StaffAccount>.Refused(ex.Message);
        }

        foreach (var service in parts.Services)
            dentist.OfferService(service);
        foreach (var branch in parts.Branches)
            dentist.AssignToBranch(branch);

        if (!await _users.TryAddAsync(dentist, cancellationToken))
            return CreateResult<StaffAccount>.Refused(EmailTaken);

        return CreateResult<StaffAccount>.Done(new StaffAccount(dentist, password));
    }

    /// <summary>
    /// Hekimin ortak bilgilerini ve şubelerini değiştirir. Çıkarılan şubede müsaitlik aralığı varsa hekim sınıfı
    /// reddediyor; o durumda hiçbir şey kaydedilmiyor.
    /// </summary>
    public async Task<ChangeResult> UpdateDentistAsync(
        User user, int dentistId, DentistDraft draft, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(draft);
        var dentist = await _dentists.GetByIdAsync(dentistId, cancellationToken);
        if (dentist is null || !user.CanManageChain)
            return ChangeResult.Missing;

        var parts = await ResolveAsync(user, draft, cancellationToken);
        if (parts.Error is { } error)
            return ChangeResult.Refused(error);

        var owner = await _users.GetByEmailAsync(draft.Email, cancellationToken);
        if (owner is not null && !owner.IsSameAs(dentist))
            return ChangeResult.Refused(EmailTaken);

        // Ad ve e-posta kuralları User sınıfında; önce geçici bir nesneyle deneniyor ki hekim yarım değişmesin.
        try
        {
            _ = new Dentist(draft.FullName, draft.Email, dentist.PasswordHash, parts.Specialty!);
        }
        catch (DomainException ex)
        {
            return ChangeResult.Refused(ex.Message);
        }

        // Şubeden çıkarma, alan sınıfında reddedilebilen tek adım. Aralığı olan şubeler öne alınıyor: reddedilecekse
        // ilk çağrı listeye dokunmadan hata veriyor ve hekim hiç değişmemiş oluyor.
        var leaving = dentist.Branches
            .Where(b => !parts.Branches.Any(b.IsSameAs))
            .OrderByDescending(b => dentist.Availability.Any(w => w.Branch.IsSameAs(b)))
            .ToList();
        try
        {
            foreach (var branch in leaving)
                dentist.RemoveFromBranch(branch);
        }
        catch (DomainException ex)
        {
            return ChangeResult.Refused(ex.Message);
        }

        dentist.Rename(draft.FullName);
        dentist.ChangeEmail(draft.Email);
        dentist.ChangeSpecialty(parts.Specialty!);
        foreach (var service in dentist.Services.Where(s => !parts.Services.Any(s.IsSameAs)).ToList())
            dentist.StopOfferingService(service);
        foreach (var service in parts.Services)
            dentist.OfferService(service);
        foreach (var branch in parts.Branches)
            dentist.AssignToBranch(branch);

        await _dentists.SaveChangesAsync(cancellationToken);
        return ChangeResult.Done;
    }

    // ---- Yardımcılar ----

    private const string EmailTaken = "Bu e-posta adresiyle açılmış bir hesap var.";

    private async Task<Branch?> ManagedBranchAsync(User user, int branchId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);
        var branch = await _branches.GetByIdAsync(branchId, cancellationToken);
        return branch is not null && user.CanManageBranch(branch) ? branch : null;
    }

    private sealed record DentistParts(Department? Specialty, IReadOnlyList<Service> Services, IReadOnlyList<Branch> Branches, string? Error);

    /// <summary>Formdaki numaraları katalogdaki ve şube listesindeki nesnelere çevirir; olmayan numara varsa hata.</summary>
    private async Task<DentistParts> ResolveAsync(User user, DentistDraft draft, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);
        if (!user.CanManageChain)
            return new DentistParts(null, [], [], "Hekim kaydını sadece zincir yöneticisi değiştirebilir.");

        var specialty = (await _catalog.GetDepartmentsAsync(cancellationToken)).FirstOrDefault(d => d.Id == draft.SpecialtyId);
        if (specialty is null)
            return new DentistParts(null, [], [], "Listeden bir uzmanlık bölümü seçin.");

        var services = (await _catalog.GetServicesAsync(cancellationToken)).Where(s => draft.ServiceIds.Contains(s.Id)).ToList();
        if (services.Count != draft.ServiceIds.Distinct().Count())
            return new DentistParts(null, [], [], "Seçilen hizmetlerden biri katalogda bulunamadı.");

        var branches = new List<Branch>();
        foreach (var id in draft.BranchIds.Distinct())
        {
            var branch = await _branches.GetByIdAsync(id, cancellationToken);
            if (branch is null)
                return new DentistParts(null, [], [], "Seçilen şubelerden biri bulunamadı.");
            branches.Add(branch);
        }

        return new DentistParts(specialty, services, branches, null);
    }
}

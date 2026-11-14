using System.ComponentModel.DataAnnotations;
using DisKlinigi.Core.Administration;
using DisKlinigi.Core.Domain;
using DisKlinigi.Core.Domain.Users;

namespace DisKlinigi.Web.Models;

// ---- Şubeler (ekran 5a) ----

/// <summary>Şubeler ekranı: soldaki liste kullanıcının yönetebildiği şubeler (şube yöneticisinde tek şube).</summary>
public sealed class BranchesViewModel
{
    public required User Viewer { get; init; }
    public required IReadOnlyList<Branch> Branches { get; init; }

    /// <summary>Formu açık şube; kullanıcının yönetebildiği şube yoksa <c>null</c>.</summary>
    public Branch? Selected { get; init; }

    /// <summary>Zincirin bütün bölümleri ("Barındırılan bölümler" kutuları).</summary>
    public required IReadOnlyList<Department> Departments { get; init; }

    public BranchForm Form { get; init; } = new();
    public string? Error { get; init; }

    public NewBranchForm NewBranch { get; init; } = new();
    public bool ShowNewBranch { get; init; }
    public string? NewBranchError { get; init; }

    /// <summary>"Yeni şube" sadece zincir geneli kayıtları yönetebilene (zincir yöneticisi).</summary>
    public bool CanOpenBranches => Viewer.CanManageChain;
}

/// <summary>Şube formu: bilgiler, yedi günün saatleri ve barındırılan bölümler.</summary>
public sealed class BranchForm : IValidatableObject
{
    [Display(Name = "Şube adı")]
    [Required(ErrorMessage = "Şube adını girin.")]
    [StringLength(100, ErrorMessage = "Şube adı en fazla {1} karakter olabilir.")]
    public string Name { get; set; } = "";

    [Display(Name = "İl")]
    [Required(ErrorMessage = "İli girin.")]
    [StringLength(50, ErrorMessage = "İl en fazla {1} karakter olabilir.")]
    public string City { get; set; } = "";

    [Display(Name = "Adres")]
    [StringLength(250, ErrorMessage = "Adres en fazla {1} karakter olabilir.")]
    public string? Address { get; set; }

    [Display(Name = "Ünit (diş koltuğu) sayısı")]
    [Range(1, 50, ErrorMessage = "Ünit sayısı {1} ile {2} arasında olmalı.")]
    public int ChairCount { get; set; } = 1;

    /// <summary>Pazartesi'den Pazar'a yedi satır.</summary>
    public List<DayHoursForm> Days { get; set; } = Display.Week.Select(day => new DayHoursForm { Day = day }).ToList();

    public List<int> DepartmentIds { get; set; } = [];

    public static BranchForm From(Branch branch) => new()
    {
        Name = branch.Name,
        City = branch.City,
        Address = branch.Address,
        ChairCount = branch.ChairCount,
        Days = Display.Week.Select(day => DayHoursForm.From(day, branch.GetWorkingHours(day))).ToList(),
        DepartmentIds = branch.Departments.Select(d => d.Id).ToList()
    };

    public BranchSettings ToSettings() => new(
        Name, City, Address, ChairCount,
        Days.Select(d => d.Open ? new DayHours(d.Day, d.Opens, d.Closes) : new DayHours(d.Day, null, null)).ToList(),
        DepartmentIds);

    /// <summary>Açık işaretlenen günde iki saat de girilmeli; saatlerin sırası alan sınıfında kontrol ediliyor.</summary>
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        foreach (var day in Days.Where(d => d.Open && (d.Opens is null || d.Closes is null)))
            yield return new ValidationResult($"{TurkishNames.Day(day.Day)}: açılış ve kapanış saatini girin ya da \"Açık\" işaretini kaldırın.");
    }
}

public sealed class DayHoursForm
{
    public DayOfWeek Day { get; set; }
    public bool Open { get; set; }
    public TimeOnly? Opens { get; set; }
    public TimeOnly? Closes { get; set; }

    public static DayHoursForm From(DayOfWeek day, TimeOfDayRange? hours) =>
        new() { Day = day, Open = hours is not null, Opens = hours?.Start, Closes = hours?.End };
}

public sealed class NewBranchForm
{
    [Display(Name = "Şube adı")]
    [Required(ErrorMessage = "Şube adını girin.")]
    [StringLength(100, ErrorMessage = "Şube adı en fazla {1} karakter olabilir.")]
    public string Name { get; set; } = "";

    [Display(Name = "İl")]
    [Required(ErrorMessage = "İli girin.")]
    [StringLength(50, ErrorMessage = "İl en fazla {1} karakter olabilir.")]
    public string City { get; set; } = "";

    [Display(Name = "Adres")]
    [StringLength(250, ErrorMessage = "Adres en fazla {1} karakter olabilir.")]
    public string? Address { get; set; }

    [Display(Name = "Ünit sayısı")]
    [Range(1, 50, ErrorMessage = "Ünit sayısı {1} ile {2} arasında olmalı.")]
    public int ChairCount { get; set; } = 1;
}

// ---- Bölüm ve hizmetler (ekran 5b) ----

public sealed class CatalogViewModel
{
    public required User Viewer { get; init; }
    public required IReadOnlyList<Department> Departments { get; init; }
    public required IReadOnlyList<Service> Services { get; init; }

    /// <summary>Formu açık hizmet ("Düzenle"); yeni hizmette <c>null</c>.</summary>
    public Service? Editing { get; init; }

    /// <summary>Hizmet formu açık mı ("Düzenle" ya da "Yeni hizmet")?</summary>
    public bool ShowServiceForm { get; init; }
    public ServiceForm Form { get; init; } = new();
    public string? FormError { get; init; }

    /// <summary>Adı değiştirilen bölüm ("Adı değiştir" tıklanınca satırda form açılıyor).</summary>
    public int? RenameDepartmentId { get; init; }

    /// <summary>Şube yöneticisi kataloğu salt okunur görüyor (K34, <see cref="User.CanManageChain"/>).</summary>
    public bool CanEdit => Viewer.CanManageChain;

    public int ServiceCount(Department department) => Services.Count(s => s.Department.IsSameAs(department));
}

public sealed class ServiceForm
{
    [Display(Name = "Hizmet adı")]
    [Required(ErrorMessage = "Hizmet adını girin.")]
    [StringLength(100, ErrorMessage = "Hizmet adı en fazla {1} karakter olabilir.")]
    public string Name { get; set; } = "";

    [Display(Name = "Bölüm")]
    public int DepartmentId { get; set; }

    [Display(Name = "Süre (dakika)")]
    [Range(Service.DurationStepMinutes, Service.MaxDurationMinutes, ErrorMessage = "Süre {1} ile {2} dakika arasında olmalı.")]
    public int DurationMinutes { get; set; } = 30;

    [Display(Name = "Ücret (₺)")]
    [Range(typeof(decimal), "0", "1000000", ErrorMessage = "Ücret {1} ile {2} ₺ arasında olmalı.")]
    public decimal Price { get; set; }

    [Display(Name = "Onay gerekir (hekim ya da şube asistanı onaylar)")]
    public bool RequiresApproval { get; set; }

    public static ServiceForm From(Service service) => new()
    {
        Name = service.Name,
        DepartmentId = service.Department.Id,
        DurationMinutes = (int)service.Duration.TotalMinutes,
        Price = service.Price,
        RequiresApproval = service.RequiresApproval
    };

    public ServiceDraft ToDraft() => new(Name, DepartmentId, DurationMinutes, Price, RequiresApproval);
}

// ---- Şube personeli (ekran 5c) ----

public sealed class StaffViewModel
{
    public required User Viewer { get; init; }

    /// <summary>Şubenin personeli; kullanıcının yönetebildiği şube yoksa <c>null</c>.</summary>
    public BranchStaff? Staff { get; init; }

    /// <summary>Zincir yöneticisinin şube seçicisi; şube yöneticisinde boş.</summary>
    public IReadOnlyList<Branch> BranchChoices { get; init; } = [];

    /// <summary>"Şubeden çıkar…" onayı gösterilen hekim.</summary>
    public int? RemoveId { get; init; }

    public AssistantForm Form { get; init; } = new();
    public string? Error { get; init; }

    /// <summary>Az önce açılan hesap; geçici parola sadece bu cevapta görünüyor (K36).</summary>
    public StaffAccount? Created { get; init; }
}

public sealed class AssistantForm
{
    [Display(Name = "Ad soyad")]
    [Required(ErrorMessage = "Ad ve soyadı girin.")]
    [StringLength(100, ErrorMessage = "Ad soyad en fazla {1} karakter olabilir.")]
    public string FullName { get; set; } = "";

    [Display(Name = "E-posta (giriş adı)")]
    [Required(ErrorMessage = "E-posta adresini girin.")]
    [EmailAddress(ErrorMessage = "Geçerli bir e-posta adresi girin.")]
    [StringLength(254)]
    public string Email { get; set; } = "";
}

// ---- Hekim kaydı (ekran 5d) ----

public sealed class DentistsViewModel
{
    public required User Viewer { get; init; }

    /// <summary>Tabloda gösterilen hekimler (şube filtresi uygulanmış).</summary>
    public required IReadOnlyList<Dentist> Dentists { get; init; }
    public required IReadOnlyList<Branch> Branches { get; init; }
    public required IReadOnlyList<Department> Departments { get; init; }
    public required IReadOnlyList<Service> Services { get; init; }
    public int? BranchFilter { get; init; }

    /// <summary>Formu açık hekim ("Düzenle"); yeni hekim formunda <c>null</c>.</summary>
    public Dentist? Editing { get; init; }
    public DentistForm Form { get; init; } = new();
    public string? Error { get; init; }
    public StaffAccount? Created { get; init; }
}

public sealed class DentistForm
{
    [Display(Name = "Ad soyad")]
    [Required(ErrorMessage = "Ad ve soyadı girin.")]
    [StringLength(100, ErrorMessage = "Ad soyad en fazla {1} karakter olabilir.")]
    public string FullName { get; set; } = "";

    [Display(Name = "E-posta (giriş adı)")]
    [Required(ErrorMessage = "E-posta adresini girin.")]
    [EmailAddress(ErrorMessage = "Geçerli bir e-posta adresi girin.")]
    [StringLength(254)]
    public string Email { get; set; } = "";

    [Display(Name = "Uzmanlık bölümü")]
    public int SpecialtyId { get; set; }

    public List<int> ServiceIds { get; set; } = [];
    public List<int> BranchIds { get; set; } = [];

    public static DentistForm From(Dentist dentist) => new()
    {
        FullName = dentist.FullName,
        Email = dentist.Email,
        SpecialtyId = dentist.Specialty.Id,
        ServiceIds = dentist.Services.Select(s => s.Id).ToList(),
        BranchIds = dentist.Branches.Select(b => b.Id).ToList()
    };

    public DentistDraft ToDraft() => new(FullName, Email, SpecialtyId, ServiceIds, BranchIds);
}

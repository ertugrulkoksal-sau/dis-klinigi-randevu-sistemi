using DisKlinigi.Core.Abstractions;
using DisKlinigi.Core.Administration;
using DisKlinigi.Core.Domain;
using DisKlinigi.Core.Domain.Users;
using DisKlinigi.Web.Models;
using DisKlinigi.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DisKlinigi.Web.Controllers;

/// <summary>
/// Yönetici paneli: şubeler ve çalışma saatleri (5a), bölüm ve hizmetler (5b), şube personeli (5c), hekim kaydı (5d).
/// </summary>
/// <remarks>
/// Sayfa politikası sadece kaba kapı (K41): ekranları şube ve zincir yöneticisi açabiliyor, hekim kaydı ve katalog
/// değişiklikleri sadece zincir yöneticisine. Şube yöneticisinin sadece kendi şubesini görmesi burada yazılmadı;
/// servisler listeyi ve kaydı <see cref="User.CanManageBranch"/> ile süzüyor. Başka şubenin numarası adrese
/// yazılırsa "Şube bulunamadı" çıkıyor. Controller kural yazmıyor, formu servise iletip sonucu ekrana çeviriyor.
/// </remarks>
[Authorize(Policy = Policies.Management)]
public class AdminController : Controller
{
    private readonly IUserRepository _users;
    private readonly BranchAdministrationService _branches;
    private readonly CatalogService _catalog;
    private readonly StaffService _staff;

    public AdminController(IUserRepository users, BranchAdministrationService branches, CatalogService catalog, StaffService staff)
    {
        _users = users;
        _branches = branches;
        _catalog = catalog;
        _staff = staff;
    }

    // ---- Şubeler ve çalışma saatleri (5a) ----

    [HttpGet]
    public async Task<IActionResult> Branches(int? id, bool create, CancellationToken cancellationToken)
    {
        var viewer = await CurrentUserAsync(cancellationToken);
        var list = await _branches.GetManageableAsync(viewer, cancellationToken);
        var selected = list.FirstOrDefault(b => b.Id == id);
        if (id is not null && selected is null)
            return BranchNotFound();

        selected ??= list.FirstOrDefault();
        return View(await BranchesViewAsync(viewer, list, selected,
            selected is null ? new BranchForm() : BranchForm.From(selected), null, create && viewer.CanManageChain, null, cancellationToken));
    }

    [HttpPost]
    public async Task<IActionResult> SaveBranch(int id, [Bind(Prefix = "Form")] BranchForm form, CancellationToken cancellationToken)
    {
        var viewer = await CurrentUserAsync(cancellationToken);
        var list = await _branches.GetManageableAsync(viewer, cancellationToken);
        var selected = list.FirstOrDefault(b => b.Id == id);
        if (selected is null)
            return BranchNotFound();

        if (!ModelState.IsValid)
            return View(nameof(Branches), await BranchesViewAsync(viewer, list, selected, form, null, false, null, cancellationToken));

        var result = await _branches.UpdateAsync(viewer, id, form.ToSettings(), cancellationToken);
        if (result.NotFound)
            return BranchNotFound();
        if (!result.Succeeded)
            return View(nameof(Branches), await BranchesViewAsync(viewer, list, selected, form, result.Error, false, null, cancellationToken));

        TempData["Status"] = $"{form.Name.Trim()} kaydedildi.";
        return RedirectToAction(nameof(Branches), new { id });
    }

    [HttpPost]
    [Authorize(Policy = Policies.ChainAdministrator)]
    public async Task<IActionResult> CreateBranch([Bind(Prefix = "NewBranch")] NewBranchForm form, CancellationToken cancellationToken)
    {
        var viewer = await CurrentUserAsync(cancellationToken);
        var list = await _branches.GetManageableAsync(viewer, cancellationToken);
        var selected = list.FirstOrDefault();

        string? error = null;
        if (ModelState.IsValid)
        {
            var result = await _branches.CreateAsync(viewer, form.Name, form.City, form.Address, form.ChairCount, cancellationToken);
            if (result.Created is { } branch)
            {
                TempData["StatusTitle"] = $"{branch.Name} açıldı.";
                TempData["Status"] = "Şu an bütün günler kapalı; çalışma saatlerini ve bölümlerini seçip kaydedin. Ardından şubeye hekim atanabilir.";
                return RedirectToAction(nameof(Branches), new { id = branch.Id });
            }
            error = result.Error;
        }

        return View(nameof(Branches), await BranchesViewAsync(viewer, list, selected,
            selected is null ? new BranchForm() : BranchForm.From(selected), null, true, error, cancellationToken, form));
    }

    // ---- Bölüm ve hizmetler (5b) ----

    [HttpGet]
    public async Task<IActionResult> Catalog(int? service, bool newService, int? rename, CancellationToken cancellationToken)
    {
        var viewer = await CurrentUserAsync(cancellationToken);
        var services = await _catalog.GetServicesAsync(cancellationToken);
        var editing = viewer.CanManageChain ? services.FirstOrDefault(s => s.Id == service) : null;
        var showForm = viewer.CanManageChain && (editing is not null || newService);

        return View(await CatalogViewAsync(viewer, editing, showForm,
            editing is null ? new ServiceForm() : ServiceForm.From(editing), null, viewer.CanManageChain ? rename : null, cancellationToken));
    }

    [HttpPost]
    [Authorize(Policy = Policies.ChainAdministrator)]
    public async Task<IActionResult> AddDepartment(string? name, CancellationToken cancellationToken)
    {
        var viewer = await CurrentUserAsync(cancellationToken);
        var result = await _catalog.AddDepartmentAsync(viewer, name, cancellationToken);
        if (result.Created is { } department)
            TempData["Status"] = $"{department.Name} bölümü eklendi. Hangi şubenin barındırdığı Şubeler sayfasından seçiliyor.";
        else
            TempData["Error"] = result.Error;
        return RedirectToAction(nameof(Catalog));
    }

    [HttpPost]
    [Authorize(Policy = Policies.ChainAdministrator)]
    public async Task<IActionResult> RenameDepartment(int id, string? name, CancellationToken cancellationToken)
    {
        var viewer = await CurrentUserAsync(cancellationToken);
        var result = await _catalog.RenameDepartmentAsync(viewer, id, name, cancellationToken);
        if (result.Succeeded)
            TempData["Status"] = "Bölümün adı değiştirildi.";
        else
            TempData["Error"] = result.NotFound ? "Bölüm bulunamadı." : result.Error;
        return RedirectToAction(nameof(Catalog));
    }

    [HttpPost]
    [Authorize(Policy = Policies.ChainAdministrator)]
    public async Task<IActionResult> SaveService(int? id, [Bind(Prefix = "Form")] ServiceForm form, CancellationToken cancellationToken)
    {
        var viewer = await CurrentUserAsync(cancellationToken);
        var services = await _catalog.GetServicesAsync(cancellationToken);
        var editing = services.FirstOrDefault(s => s.Id == id);
        var oldPrice = editing?.Price;

        if (!ModelState.IsValid)
            return View(nameof(Catalog), await CatalogViewAsync(viewer, editing, true, form, null, null, cancellationToken));

        var result = await _catalog.SaveServiceAsync(viewer, id, form.ToDraft(), cancellationToken);
        if (result.Created is not { } saved)
            return View(nameof(Catalog), await CatalogViewAsync(viewer, editing, true, form, result.Error, null, cancellationToken));

        TempData["StatusTitle"] = editing is null ? $"{saved.Name} eklendi." : $"{saved.Name} kaydedildi.";
        if (oldPrice is { } before && before != saved.Price)
            TempData["Status"] = $"Ücret {Display.Price(before)} → {Display.Price(saved.Price)} oldu. Daha önce alınmış randevular kendi ücretini koruyor; yeni ücret bundan sonraki randevulara uygulanıyor.";
        return RedirectToAction(nameof(Catalog));
    }

    // ---- Şube personeli (5c) ----

    [HttpGet]
    public async Task<IActionResult> Staff(int? branchId, int? remove, CancellationToken cancellationToken)
    {
        var viewer = await CurrentUserAsync(cancellationToken);
        var staff = await StaffOfAsync(viewer, branchId, cancellationToken);
        if (staff is null && branchId is not null)
            return BranchNotFound(nameof(Staff));

        return View(await StaffViewAsync(viewer, staff, new AssistantForm(), null, null, remove, cancellationToken));
    }

    [HttpPost]
    public async Task<IActionResult> AssignDentist(int branchId, int dentistId, CancellationToken cancellationToken)
    {
        var viewer = await CurrentUserAsync(cancellationToken);
        var result = await _staff.AssignDentistAsync(viewer, branchId, dentistId, cancellationToken);
        if (result.Succeeded)
            TempData["Status"] = "Hekim şubeye atandı. Bu şubedeki müsaitlik aralıklarını hekim kendi Müsaitlik ekranından girecek.";
        else
            TempData["Error"] = result.NotFound ? "Hekim ya da şube bulunamadı." : result.Error;
        return RedirectToAction(nameof(Staff), new { branchId });
    }

    [HttpPost]
    public async Task<IActionResult> RemoveDentist(int branchId, int dentistId, CancellationToken cancellationToken)
    {
        var viewer = await CurrentUserAsync(cancellationToken);
        var result = await _staff.RemoveDentistAsync(viewer, branchId, dentistId, cancellationToken);
        if (result.Succeeded)
            TempData["Status"] = "Hekim şubeden çıkarıldı.";
        else
            TempData["Error"] = result.NotFound ? "Hekim ya da şube bulunamadı." : result.Error;
        return RedirectToAction(nameof(Staff), new { branchId });
    }

    /// <summary>
    /// Başarılı olunca yönlendirme yapılmıyor, sayfa geçici parolayla birlikte bu cevapta çiziliyor. Yönlendirme
    /// olsaydı parolayı bir sonraki isteğe taşımak gerekirdi (TempData çerezi); parola hiçbir yerde saklanmasın diye
    /// bu yolu seçtim (K36, K55).
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> CreateAssistant(int branchId, [Bind(Prefix = "Form")] AssistantForm form, CancellationToken cancellationToken)
    {
        var viewer = await CurrentUserAsync(cancellationToken);
        if (!ModelState.IsValid)
            return View(nameof(Staff), await StaffViewAsync(viewer, await StaffOfAsync(viewer, branchId, cancellationToken), form, null, null, null, cancellationToken));

        var result = await _staff.CreateAssistantAsync(viewer, branchId, form.FullName, form.Email, cancellationToken);
        if (!result.Succeeded)
            return View(nameof(Staff), await StaffViewAsync(viewer, await StaffOfAsync(viewer, branchId, cancellationToken), form, result.Error, null, null, cancellationToken));

        ModelState.Clear();
        return View(nameof(Staff), await StaffViewAsync(viewer, await StaffOfAsync(viewer, branchId, cancellationToken), new AssistantForm(), null, result.Created, null, cancellationToken));
    }

    // ---- Hekim kaydı (5d) ----

    /// <summary>Hekimin uzmanlığı ve hizmetleri bütün şubeler için ortak; sadece zincir yöneticisi (K34).</summary>
    [HttpGet]
    [Authorize(Policy = Policies.ChainAdministrator)]
    public async Task<IActionResult> Dentists(int? branchId, int? edit, CancellationToken cancellationToken)
    {
        var viewer = await CurrentUserAsync(cancellationToken);
        var all = await _staff.GetDentistsAsync(viewer, cancellationToken);
        var editing = all.FirstOrDefault(d => d.Id == edit);
        return View(await DentistsViewAsync(viewer, all, branchId, editing,
            editing is null ? new DentistForm() : DentistForm.From(editing), null, null, cancellationToken));
    }

    [HttpPost]
    [Authorize(Policy = Policies.ChainAdministrator)]
    public async Task<IActionResult> SaveDentist(int? id, [Bind(Prefix = "Form")] DentistForm form, CancellationToken cancellationToken)
    {
        var viewer = await CurrentUserAsync(cancellationToken);
        var all = await _staff.GetDentistsAsync(viewer, cancellationToken);
        var editing = all.FirstOrDefault(d => d.Id == id);
        if (id is not null && editing is null)
        {
            TempData["Error"] = "Hekim bulunamadı.";
            return RedirectToAction(nameof(Dentists));
        }

        if (!ModelState.IsValid)
            return View(nameof(Dentists), await DentistsViewAsync(viewer, all, null, editing, form, null, null, cancellationToken));

        if (editing is null)
        {
            var created = await _staff.RegisterDentistAsync(viewer, form.ToDraft(), cancellationToken);
            if (!created.Succeeded)
                return View(nameof(Dentists), await DentistsViewAsync(viewer, all, null, null, form, created.Error, null, cancellationToken));

            ModelState.Clear();
            return View(nameof(Dentists), await DentistsViewAsync(viewer, await _staff.GetDentistsAsync(viewer, cancellationToken),
                null, null, new DentistForm(), null, created.Created, cancellationToken));
        }

        var result = await _staff.UpdateDentistAsync(viewer, editing.Id, form.ToDraft(), cancellationToken);
        if (!result.Succeeded)
            return View(nameof(Dentists), await DentistsViewAsync(viewer, all, null, editing, form, result.Error, null, cancellationToken));

        TempData["Status"] = $"{form.FullName.Trim()} kaydedildi.";
        return RedirectToAction(nameof(Dentists));
    }

    // ---- Yardımcılar ----

    private IActionResult BranchNotFound(string action = nameof(Branches))
    {
        // İlgisiz şube için de "bulunamadı" (K46): şube yöneticisi başka şubenin var olduğunu buradan anlamıyor.
        // id açıkça boşaltılıyor; yoksa adres üretici geldiğimiz adresteki id'yi tekrar kullanıp aynı sayfaya
        // yönlendiriyordu (/Admin/Branches/3 → /Admin/Branches/3, sonsuz döngü; web testi yakaladı).
        TempData["Error"] = "Şube bulunamadı.";
        return RedirectToAction(action, new { id = (int?)null });
    }

    private async Task<BranchesViewModel> BranchesViewAsync(
        User viewer, IReadOnlyList<Branch> list, Branch? selected, BranchForm form, string? error,
        bool showNewBranch, string? newBranchError, CancellationToken cancellationToken, NewBranchForm? newBranch = null) =>
        new()
        {
            Viewer = viewer,
            Branches = list,
            Selected = selected,
            Departments = await _catalog.GetDepartmentsAsync(cancellationToken),
            Form = form,
            Error = error,
            ShowNewBranch = showNewBranch,
            NewBranch = newBranch ?? new NewBranchForm(),
            NewBranchError = newBranchError
        };

    private async Task<CatalogViewModel> CatalogViewAsync(
        User viewer, Service? editing, bool showForm, ServiceForm form, string? error, int? rename, CancellationToken cancellationToken) =>
        new()
        {
            Viewer = viewer,
            Departments = await _catalog.GetDepartmentsAsync(cancellationToken),
            Services = await _catalog.GetServicesAsync(cancellationToken),
            Editing = editing,
            ShowServiceForm = showForm,
            Form = form,
            FormError = error,
            RenameDepartmentId = rename
        };

    /// <summary>
    /// Personeli gösterilecek şube: şube yöneticisinde kendi şubesi, zincir yöneticisinde seçilen (seçilmediyse ilk) şube.
    /// Başka bir şube sorulursa servis <c>null</c> dönüyor (CanManageBranch).
    /// </summary>
    private async Task<BranchStaff?> StaffOfAsync(User viewer, int? branchId, CancellationToken cancellationToken)
    {
        var id = branchId ?? viewer.AssignedBranch?.Id ?? (await _branches.GetManageableAsync(viewer, cancellationToken)).FirstOrDefault()?.Id;
        return id is null ? null : await _staff.GetBranchStaffAsync(viewer, id.Value, cancellationToken);
    }

    private async Task<StaffViewModel> StaffViewAsync(
        User viewer, BranchStaff? staff, AssistantForm form, string? error, StaffAccount? created, int? remove, CancellationToken cancellationToken) =>
        new()
        {
            Viewer = viewer,
            Staff = staff,
            BranchChoices = viewer.CanManageChain ? await _branches.GetManageableAsync(viewer, cancellationToken) : [],
            Form = form,
            Error = error,
            Created = created,
            RemoveId = remove
        };

    private async Task<DentistsViewModel> DentistsViewAsync(
        User viewer, IReadOnlyList<Dentist> all, int? branchFilter, Dentist? editing, DentistForm form, string? error,
        StaffAccount? created, CancellationToken cancellationToken) =>
        new()
        {
            Viewer = viewer,
            Dentists = branchFilter is { } b ? all.Where(d => d.Branches.Any(x => x.Id == b)).ToList() : all,
            Branches = await _branches.GetManageableAsync(viewer, cancellationToken),
            Departments = await _catalog.GetDepartmentsAsync(cancellationToken),
            Services = await _catalog.GetServicesAsync(cancellationToken),
            BranchFilter = branchFilter,
            Editing = editing,
            Form = form,
            Error = error,
            Created = created
        };

    /// <summary>Girişli yönetici; şube yöneticisi şubesiyle birlikte yükleniyor (CanManageBranch şubeye bakıyor).</summary>
    private async Task<User> CurrentUserAsync(CancellationToken cancellationToken)
    {
        var id = User.GetUserId() ?? throw new InvalidOperationException("Oturumda kullanıcı yok.");
        return await _users.GetByIdAsync(id, cancellationToken)
            ?? throw new InvalidOperationException("Oturumdaki kullanıcı bulunamadı.");
    }
}

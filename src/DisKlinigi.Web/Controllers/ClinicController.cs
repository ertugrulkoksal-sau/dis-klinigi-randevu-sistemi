using System.Globalization;
using DisKlinigi.Core.Abstractions;
using DisKlinigi.Core.Appointments;
using DisKlinigi.Core.Availability;
using DisKlinigi.Core.Domain;
using DisKlinigi.Core.Domain.Appointments;
using DisKlinigi.Core.Domain.Users;
using DisKlinigi.Web.Models;
using DisKlinigi.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DisKlinigi.Web.Controllers;

/// <summary>
/// Hekim ve asistan ekranları (personel paneli): günlük takvim (4a), onay kuyruğu (4b) ve müsaitlik
/// aralıkları (4c). Takvim ve kuyruk ikisine de açık; müsaitlik sadece hekime.
/// </summary>
/// <remarks>
/// Bu sınıf da kural yazmıyor. Kimin hangi randevuyu gördüğü ve hangi butonun çıktığı alan
/// sınıflarının cevabı (<see cref="User.CanApprove"/>, <see cref="Appointment.ActionsFor"/>); işlemleri
/// <see cref="AppointmentService"/>, müsaitliği <see cref="AvailabilityService"/> yapıyor. Hekim ile
/// asistanın ekranları arasındaki tek fark hekim seçici: hekim kendi takvimini görüyor, asistan şubesinin
/// hekimleri arasından seçiyor. Bu ayrım bir yetki kararı değil, ekranın hangi hekimle açılacağı.
/// </remarks>
[Authorize(Policy = Policies.ClinicStaff)]
public class ClinicController : Controller
{
    private readonly IUserRepository _users;
    private readonly IDentistRepository _dentists;
    private readonly AppointmentService _appointments;
    private readonly AvailabilityService _availability;

    public ClinicController(
        IUserRepository users, IDentistRepository dentists, AppointmentService appointments, AvailabilityService availability)
    {
        _users = users;
        _dentists = dentists;
        _appointments = appointments;
        _availability = availability;
    }

    // ---- Günlük takvim (4a) ----

    [HttpGet]
    public async Task<IActionResult> Calendar(DateOnly? date, int? dentistId, int? reject, int? cancel, CancellationToken cancellationToken)
    {
        var viewer = await CurrentUserAsync(cancellationToken);
        var now = _appointments.Now;
        var day = date ?? DateOnly.FromDateTime(now);
        var (dentist, choices) = await DentistToShowAsync(viewer, dentistId, cancellationToken);

        return View(new CalendarViewModel
        {
            Viewer = viewer,
            Now = now,
            Date = day,
            Dentist = dentist,
            Day = dentist is null ? null : await _appointments.GetDayAsync(viewer, dentist, day, cancellationToken),
            DentistChoices = choices,
            RejectId = reject,
            CancelId = cancel
        });
    }

    // ---- Onay kuyruğu (4b) ----

    [HttpGet]
    public async Task<IActionResult> Queue(int? dentistId, int? reject, CancellationToken cancellationToken)
    {
        var viewer = await CurrentUserAsync(cancellationToken);
        var queue = await _appointments.GetApprovalQueueAsync(viewer, cancellationToken);
        var choices = viewer.AssignedBranch is { } branch
            ? await _dentists.GetWorkingAtAsync(branch, cancellationToken)
            : [];

        return View(new QueueViewModel
        {
            Viewer = viewer,
            Now = _appointments.Now,
            Items = dentistId is { } id ? queue.Where(a => a.Dentist.Id == id).ToList() : queue,
            DentistChoices = choices,
            DentistId = dentistId,
            RejectId = reject
        });
    }

    // ---- Randevu işlemleri (takvim ve kuyruk ortak) ----

    [HttpPost]
    public async Task<IActionResult> Approve(int id, string? returnUrl, CancellationToken cancellationToken)
    {
        var viewer = await CurrentUserAsync(cancellationToken);
        return Report(await _appointments.ApproveAsync(viewer, id, cancellationToken), "Randevu onaylandı.", returnUrl);
    }

    [HttpPost]
    public async Task<IActionResult> Reject(int id, string? reason, string? returnUrl, CancellationToken cancellationToken)
    {
        var viewer = await CurrentUserAsync(cancellationToken);
        return Report(await _appointments.RejectAsync(viewer, id, reason, cancellationToken),
            "Randevu reddedildi; gerekçe hastaya gösterilecek.", returnUrl);
    }

    [HttpPost]
    public async Task<IActionResult> Cancel(int id, string? returnUrl, CancellationToken cancellationToken)
    {
        var viewer = await CurrentUserAsync(cancellationToken);
        return Report(await _appointments.CancelAsync(viewer, id, cancellationToken),
            "Randevu iptal edildi; saat başka hastalara açıldı.", returnUrl);
    }

    [HttpPost]
    public async Task<IActionResult> Complete(int id, string? returnUrl, CancellationToken cancellationToken)
    {
        var viewer = await CurrentUserAsync(cancellationToken);
        return Report(await _appointments.CompleteAsync(viewer, id, cancellationToken), "Randevu tamamlandı olarak işaretlendi.", returnUrl);
    }

    [HttpPost]
    public async Task<IActionResult> NoShow(int id, string? returnUrl, CancellationToken cancellationToken)
    {
        var viewer = await CurrentUserAsync(cancellationToken);
        return Report(await _appointments.MarkNoShowAsync(viewer, id, cancellationToken), "Randevu gelmedi olarak işaretlendi.", returnUrl);
    }

    // ---- Müsaitlik aralıkları (4c) ----

    /// <summary>Asistanın kendi müsaitliği yok; controller'daki politikaya ek olarak sadece hekim.</summary>
    [HttpGet]
    [Authorize(Policy = Policies.Dentist)]
    public async Task<IActionResult> Availability(int? delete, CancellationToken cancellationToken)
    {
        var dentist = await CurrentDentistAsync(cancellationToken);
        return View(await AvailabilityViewAsync(dentist, new AvailabilityForm { BranchId = FirstBranchId(dentist) }, null, delete, cancellationToken));
    }

    [HttpPost]
    [Authorize(Policy = Policies.Dentist)]
    public async Task<IActionResult> AddAvailability([Bind(Prefix = "Form")] AvailabilityForm form, CancellationToken cancellationToken)
    {
        var dentist = await CurrentDentistAsync(cancellationToken);
        if (!ModelState.IsValid)
            return View(nameof(Availability), await AvailabilityViewAsync(dentist, form, null, null, cancellationToken));

        var result = await _availability.AddAsync(dentist, form.BranchId, form.Day, form.Start!.Value, form.End!.Value, cancellationToken);
        if (!result.Succeeded)
            return View(nameof(Availability), await AvailabilityViewAsync(dentist, form, result.Error, null, cancellationToken));

        TempData["Status"] = $"{TurkishNames.Day(form.Day)} {new TimeOfDayRange(form.Start.Value, form.End.Value)} aralığı eklendi; hastalar bu aralığa randevu alabilir.";
        return RedirectToAction(nameof(Availability));
    }

    [HttpPost]
    [Authorize(Policy = Policies.Dentist)]
    public async Task<IActionResult> RemoveAvailability(int id, CancellationToken cancellationToken)
    {
        var dentist = await CurrentDentistAsync(cancellationToken);
        var result = await _availability.RemoveAsync(dentist, id, cancellationToken);

        if (result.Succeeded)
            TempData["Status"] = "Aralık silindi.";
        else
            TempData["Error"] = result.NotFound ? "Aralık bulunamadı." : result.Error;
        return RedirectToAction(nameof(Availability));
    }

    // ---- Yardımcılar ----

    /// <summary>
    /// İşlem sonucunu sayfa üstü mesaja çevirip geldiği sayfaya döner. Dönüş adresi sadece site içiyse
    /// kullanılıyor (<see cref="IUrlHelper.IsLocalUrl"/>); yoksa takvime.
    /// </summary>
    private IActionResult Report(AppointmentActionResult result, string success, string? returnUrl)
    {
        switch (result.Outcome)
        {
            case AppointmentActionOutcome.Succeeded:
                var a = result.Appointment!;
                TempData["StatusTitle"] = success;
                TempData["Status"] = $"{a.Patient.FullName}, {Display.ShortDateWithDay(a.Period.Start)} {a.Period.ToTimeString()}, {Display.InSentence(a.ServiceNameAtBooking)}.";
                break;
            case AppointmentActionOutcome.Refused:
                TempData["Error"] = result.Error;
                break;
            default:
                // İlgisiz personel için de "bulunamadı" (K46): randevunun varlığı anlaşılmıyor.
                TempData["Error"] = "Randevu bulunamadı.";
                break;
        }

        return Url.IsLocalUrl(returnUrl) ? LocalRedirect(returnUrl) : RedirectToAction(nameof(Calendar));
    }

    /// <summary>
    /// Takvimde gösterilecek hekim: hekimin kendisi ya da asistanın şubesindeki hekimlerden seçilen
    /// (seçilmediyse ilki). Hekim kendisi için bütün parçalarıyla yeniden yükleniyor; müsaitlik aralıkları gerekiyor.
    /// </summary>
    private async Task<(Dentist? Dentist, IReadOnlyList<Dentist> Choices)> DentistToShowAsync(
        User viewer, int? dentistId, CancellationToken cancellationToken)
    {
        if (viewer is Dentist)
            return (await _dentists.GetByIdAsync(viewer.Id, cancellationToken), []);

        var choices = viewer.AssignedBranch is { } branch
            ? await _dentists.GetWorkingAtAsync(branch, cancellationToken)
            : [];
        return (choices.FirstOrDefault(d => d.Id == dentistId) ?? choices.FirstOrDefault(), choices);
    }

    private async Task<AvailabilityViewModel> AvailabilityViewAsync(
        Dentist dentist, AvailabilityForm form, string? error, int? deleteId, CancellationToken cancellationToken) =>
        new()
        {
            Dentist = dentist,
            Windows = await _availability.GetUsageAsync(dentist, cancellationToken),
            Form = form,
            Error = error,
            DeleteId = deleteId
        };

    private static int FirstBranchId(Dentist dentist) =>
        dentist.Branches.OrderBy(b => b.Name, StringComparer.Create(CultureInfo.GetCultureInfo("tr-TR"), true)).FirstOrDefault()?.Id ?? 0;

    /// <summary>Girişli personel. Politika rolü, oturum doğrulayıcı (K42) kullanıcının var olduğunu garanti ediyor.</summary>
    private async Task<User> CurrentUserAsync(CancellationToken cancellationToken)
    {
        var id = User.GetUserId() ?? throw new InvalidOperationException("Oturumda kullanıcı yok.");
        return await _users.GetByIdAsync(id, cancellationToken)
            ?? throw new InvalidOperationException("Oturumdaki kullanıcı bulunamadı.");
    }

    /// <summary>Girişli hekim, müsaitlik aralıkları ve şubeleriyle (değişiklik kaydedilebilsin diye izlenen nesne).</summary>
    private async Task<Dentist> CurrentDentistAsync(CancellationToken cancellationToken)
    {
        var id = User.GetUserId() ?? throw new InvalidOperationException("Oturumda kullanıcı yok.");
        return await _dentists.GetByIdAsync(id, cancellationToken)
            ?? throw new InvalidOperationException("Oturumdaki kullanıcı hekim değil.");
    }
}

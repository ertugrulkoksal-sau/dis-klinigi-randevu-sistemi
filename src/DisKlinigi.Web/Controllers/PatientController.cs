using DisKlinigi.Core.Abstractions;
using DisKlinigi.Core.Appointments;
using DisKlinigi.Core.Booking;
using DisKlinigi.Core.Domain;
using DisKlinigi.Core.Domain.Appointments;
using DisKlinigi.Core.Domain.Users;
using DisKlinigi.Web.Models;
using DisKlinigi.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DisKlinigi.Web.Controllers;

/// <summary>
/// Hasta ekranları: randevu alma akışı (ekran 2a, 2b) ve randevularım (ekran 3).
/// </summary>
/// <remarks>
/// Bu sınıf kural yazmıyor. Hekim listesini <see cref="Dentist.CanServe"/>, boş saatleri ve kaydı
/// <see cref="AppointmentScheduler"/>, iptali <see cref="AppointmentService"/> yapıyor; controller
/// seçimleri sorgu parametrelerinden okuyup bu sınıflara veriyor ve sonucu ekrana çeviriyor.
/// Seçimler adres çubuğunda olduğu için biri elle bozuk bir kombinasyon yazarsa (şubede olmayan
/// bölüm, hizmeti vermeyen hekim) o seçim yok sayılıyor; kayıt sırasında kurallar zaten yeniden çalışıyor.
/// </remarks>
[Authorize(Policy = Policies.Patient)]
public class PatientController : Controller
{
    private readonly IBranchRepository _branches;
    private readonly ICatalogRepository _catalog;
    private readonly IDentistRepository _dentists;
    private readonly IUserRepository _users;
    private readonly AppointmentScheduler _scheduler;
    private readonly AppointmentService _appointments;

    public PatientController(
        IBranchRepository branches, ICatalogRepository catalog, IDentistRepository dentists,
        IUserRepository users, AppointmentScheduler scheduler, AppointmentService appointments)
    {
        _branches = branches;
        _catalog = catalog;
        _dentists = dentists;
        _users = users;
        _scheduler = scheduler;
        _appointments = appointments;
    }

    // ---- Randevu al: şube, bölüm, hizmet, hekim (2a) ----

    [HttpGet]
    public async Task<IActionResult> Book(int? branchId, int? departmentId, int? serviceId, int? dentistId, CancellationToken cancellationToken)
    {
        var branches = await _branches.GetAllAsync(cancellationToken);
        var branch = branches.FirstOrDefault(b => b.Id == branchId);
        if (branch is null)
            return View(new BookingChoicesViewModel { Branches = branches });

        var departments = branch.Departments.OrderBy(d => d.Name, TurkishOrder).ToList();
        var department = departments.FirstOrDefault(d => d.Id == departmentId);
        if (department is null)
            return View(new BookingChoicesViewModel { Branches = branches, Branch = branch, Departments = departments });

        var services = (await _catalog.GetServicesAsync(cancellationToken))
            .Where(s => s.Department.IsSameAs(department))
            .ToList();
        var service = services.FirstOrDefault(s => s.Id == serviceId);
        var dentists = service is null
            ? []
            : (await _dentists.GetWorkingAtAsync(branch, cancellationToken)).Where(d => d.CanServe(branch, service)).ToList();

        return View(new BookingChoicesViewModel
        {
            Branches = branches,
            Branch = branch,
            Departments = departments,
            Department = department,
            Services = services,
            Service = service,
            Dentists = dentists,
            Dentist = dentists.FirstOrDefault(d => d.Id == dentistId)
        });
    }

    // ---- Randevu al: gün, saat, özet ve onay (2b) ----

    [HttpGet]
    public async Task<IActionResult> Schedule(
        int branchId, int serviceId, int dentistId, DateOnly? from, DateOnly? date, TimeOnly? start,
        CancellationToken cancellationToken)
    {
        var selection = await LoadSelectionAsync(branchId, serviceId, dentistId, cancellationToken);
        if (selection is null)
            return RedirectToAction(nameof(Book), new { branchId });

        var form = new BookingForm { BranchId = branchId, ServiceId = serviceId, DentistId = dentistId, Date = date, Start = start };
        return View(await ScheduleViewAsync(selection.Value, from, form, [], cancellationToken));
    }

    /// <summary>
    /// "Randevuyu oluştur". Kurallar planlayıcıda bir kez daha, kayıtla aynı işlemde çalışıyor (K19);
    /// arada saat dolduysa sayfa çiğnenen kuralların hepsiyle ve seçim kaldırılmış olarak geri geliyor.
    /// </summary>
    [HttpPost]
    [ActionName(nameof(Schedule))]
    public async Task<IActionResult> ScheduleSubmit(BookingForm form, CancellationToken cancellationToken)
    {
        var selection = await LoadSelectionAsync(form.BranchId, form.ServiceId, form.DentistId, cancellationToken);
        if (selection is null)
            return RedirectToAction(nameof(Book), new { branchId = form.BranchId });

        var (branch, service, dentist) = selection.Value;
        if (!ModelState.IsValid)
            return View(nameof(Schedule), await ScheduleViewAsync(selection.Value, null, form, [], cancellationToken));

        var patient = await CurrentPatientAsync(cancellationToken);
        BookingResult result;
        try
        {
            var start = form.Date!.Value.ToDateTime(form.Start!.Value);
            result = await _scheduler.BookAsync(
                new BookingRequest(patient, dentist, service, branch, start, form.Note), cancellationToken);
        }
        catch (DomainException ex)
        {
            // Randevu isteği kendi tutarlılığını kontrol ediyor (ör. çok uzun not); kurallara gelmeden dönüyor.
            return View(nameof(Schedule), await ScheduleViewAsync(selection.Value, null, form, [ex.Message], cancellationToken));
        }

        if (!result.Succeeded)
        {
            form.Start = null;
            return View(nameof(Schedule), await ScheduleViewAsync(selection.Value, null, form, result.Violations, cancellationToken));
        }

        var appointment = result.Appointment;
        TempData["StatusTitle"] = "Randevunuz oluşturuldu.";
        TempData["Status"] = appointment.Status == AppointmentStatus.PendingApproval
            ? $"{appointment.ServiceNameAtBooking} onay gerektirdiği için şu an \"Onay bekliyor\" durumunda; hekim ya da şube asistanı onaylayınca kesinleşecek."
            : $"{appointment.ServiceNameAtBooking} otomatik onaylandı; {Display.LongDate(DateOnly.FromDateTime(appointment.Period.Start))} {appointment.Period.ToTimeString()} sizin.";
        return RedirectToAction(nameof(Appointments));
    }

    // ---- Randevularım (3) ----

    [HttpGet]
    public async Task<IActionResult> Appointments(int? cancel, CancellationToken cancellationToken)
    {
        var patient = await CurrentPatientAsync(cancellationToken);
        var all = await _appointments.GetForPatientAsync(patient, cancellationToken);
        var now = _appointments.Now;

        var upcoming = all.Where(a => a.IsUpcoming(now)).ToList();
        var model = new PatientAppointmentsViewModel
        {
            Patient = patient,
            Now = now,
            Upcoming = upcoming,
            Past = all.Except(upcoming).OrderByDescending(a => a.Period.Start).ToList(),
            // Onay adımı sadece gerçekten iptal edilebilecek randevuda açılıyor.
            ConfirmCancelId = upcoming.FirstOrDefault(a => a.Id == cancel && patient.CanCancel(a, now))?.Id
        };
        return View(model);
    }

    /// <summary>İptal onaylandı ("Evet, iptal et"). 24 saat kuralı ve durum kontrolü alan sınıflarında.</summary>
    [HttpPost]
    public async Task<IActionResult> Cancel(int id, CancellationToken cancellationToken)
    {
        var patient = await CurrentPatientAsync(cancellationToken);
        var result = await _appointments.CancelAsync(patient, id, cancellationToken);

        switch (result.Outcome)
        {
            case AppointmentActionOutcome.Succeeded:
                TempData["StatusTitle"] = "Randevunuz iptal edildi.";
                TempData["Status"] = "Saat başka hastalara açıldı. Aynı hizmet için yeniden randevu alabilirsiniz.";
                break;
            case AppointmentActionOutcome.Refused:
                TempData["Error"] = result.Error;
                break;
            default:
                // Başka hastanın randevusu da "bulunamadı" (AppointmentService.CancelAsync).
                TempData["Error"] = "Randevu bulunamadı.";
                break;
        }
        return RedirectToAction(nameof(Appointments));
    }

    // ---- Yardımcılar ----

    private static readonly StringComparer TurkishOrder =
        StringComparer.Create(System.Globalization.CultureInfo.GetCultureInfo("tr-TR"), ignoreCase: true);

    /// <summary>
    /// Şube, hizmet ve hekimi kayıt yapılabilecek şekilde (izlenen nesneler olarak) yükler; kombinasyon
    /// geçersizse (hekim bu şubede bu hizmeti vermiyor) <c>null</c>.
    /// </summary>
    private async Task<(Branch Branch, Service Service, Dentist Dentist)?> LoadSelectionAsync(
        int branchId, int serviceId, int dentistId, CancellationToken cancellationToken)
    {
        var branch = await _branches.GetByIdAsync(branchId, cancellationToken);
        var service = await _catalog.GetServiceByIdAsync(serviceId, cancellationToken);
        var dentist = await _dentists.GetByIdAsync(dentistId, cancellationToken);

        if (branch is null || service is null || dentist is null || !dentist.CanServe(branch, service))
            return null;
        return (branch, service, dentist);
    }

    private async Task<BookingScheduleViewModel> ScheduleViewAsync(
        (Branch Branch, Service Service, Dentist Dentist) selection, DateOnly? from, BookingForm form,
        IReadOnlyList<string> violations, CancellationToken cancellationToken)
    {
        var (branch, service, dentist) = selection;
        var today = DateOnly.FromDateTime(_appointments.Now);

        // Seçili gün pencerede görünsün: pencere verilmediyse seçili günden (yoksa bugünden) başlıyor.
        var windowStart = from ?? form.Date ?? today;
        if (windowStart < today)
            windowStart = today;

        var days = dentist.WorkingDaysAt(branch, windowStart, BookingScheduleViewModel.DaysPerPage);
        var date = form.Date is { } d && d >= today && dentist.WorkingDaysAt(branch, d, 1).Count == 1
            ? d
            : days.Count > 0 ? days[0] : (DateOnly?)null;

        IReadOnlyList<TimeRange> slots = [];
        if (date is { } day)
        {
            var patient = await CurrentPatientAsync(cancellationToken);
            slots = await _scheduler.GetFreeSlotsAsync(patient, dentist, service, branch, day, cancellationToken);
        }

        form.BranchId = branch.Id;
        form.ServiceId = service.Id;
        form.DentistId = dentist.Id;
        form.Date = date;
        var selected = slots.FirstOrDefault(s => form.Start is { } t && TimeOnly.FromDateTime(s.Start) == t);
        form.Start = selected is null ? null : TimeOnly.FromDateTime(selected.Start);

        return new BookingScheduleViewModel
        {
            Branch = branch,
            Service = service,
            Dentist = dentist,
            From = windowStart,
            Today = today,
            Days = days,
            Date = date,
            Slots = slots,
            Selected = selected,
            Form = form,
            Violations = violations
        };
    }

    /// <summary>
    /// Girişli hasta. Sayfa politikası rolü, oturum doğrulayıcı (K42) kullanıcının veritabanında
    /// olduğunu zaten garanti ediyor; burada bulunamaması bir programlama hatası.
    /// </summary>
    private async Task<Patient> CurrentPatientAsync(CancellationToken cancellationToken)
    {
        var id = User.GetUserId() ?? throw new InvalidOperationException("Oturumda kullanıcı yok.");
        return await _users.GetByIdAsync(id, cancellationToken) as Patient
            ?? throw new InvalidOperationException("Oturumdaki kullanıcı hasta değil.");
    }
}

using DisKlinigi.Core.Abstractions;
using DisKlinigi.Core.Appointments;
using DisKlinigi.Web.Security;
using Microsoft.AspNetCore.Mvc;

namespace DisKlinigi.Web.ViewComponents;

/// <summary>
/// Üst menüde "Onay kuyruğu"nun yanındaki sayı. Sayı kuyruk ekranıyla aynı yoldan geliyor
/// (<see cref="AppointmentService.GetApprovalQueueAsync"/>): hekim kendi bekleyen randevularını, asistan
/// şubesinin randevularını sayıyor. Tasarım incelemesinde (9 Kasım) hekimin menüsündeki sayının onun
/// göremeyeceği randevuları da saydığı bulunmuştu; aynı süzgeç kullanıldığı için bu artık olamıyor.
/// </summary>
public sealed class QueueCountViewComponent : ViewComponent
{
    private readonly IUserRepository _users;
    private readonly AppointmentService _appointments;

    public QueueCountViewComponent(IUserRepository users, AppointmentService appointments)
    {
        _users = users;
        _appointments = appointments;
    }

    public async Task<IViewComponentResult> InvokeAsync()
    {
        if (UserClaimsPrincipal.GetUserId() is not { } id || await _users.GetByIdAsync(id) is not { } user)
            return Content(string.Empty);

        var count = (await _appointments.GetApprovalQueueAsync(user)).Count;
        return View(count);
    }
}

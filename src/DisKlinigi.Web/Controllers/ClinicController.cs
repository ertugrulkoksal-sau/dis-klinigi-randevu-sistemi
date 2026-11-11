using DisKlinigi.Web.Models;
using DisKlinigi.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DisKlinigi.Web.Controllers;

/// <summary>
/// Hekim ve asistan ekranları (personel paneli). Takvim ve onay kuyruğu ikisine de açık;
/// müsaitlik aralıkları sadece hekime. İçerikler 13 Kasım'da dolduruluyor.
/// </summary>
[Authorize(Policy = Policies.ClinicStaff)]
public class ClinicController : Controller
{
    public IActionResult Calendar() => View("ComingSoon", new ComingSoonViewModel(
        "Günlük takvim", "13 Kasım",
        "Günün randevuları ve boş aralıklar; onaylama, reddetme ve tamamlandı/gelmedi işaretleme."));

    public IActionResult Queue() => View("ComingSoon", new ComingSoonViewModel(
        "Onay kuyruğu", "13 Kasım",
        "Onay bekleyen tedavi randevuları. Hekim kendi randevularını, asistan şubesinin bütün randevularını görecek."));

    /// <summary>Asistanın kendi müsaitliği yok; controller'daki politikaya ek olarak sadece hekim.</summary>
    [Authorize(Policy = Policies.Dentist)]
    public IActionResult Availability() => View("ComingSoon", new ComingSoonViewModel(
        "Müsaitlik aralıkları", "13 Kasım",
        "Haftalık çalışma aralıklarınız; hastalar sadece bu aralıklara randevu alabiliyor."));
}

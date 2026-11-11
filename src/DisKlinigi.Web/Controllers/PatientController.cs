using DisKlinigi.Web.Models;
using DisKlinigi.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DisKlinigi.Web.Controllers;

/// <summary>
/// Hasta ekranları. 11 Kasım'da sadece yetki iskeleti var: sayfalar sadece hastaya açılıyor,
/// içerikleri 12 Kasım'da (randevu alma akışı ve randevularım) dolduruluyor.
/// </summary>
[Authorize(Policy = Policies.Patient)]
public class PatientController : Controller
{
    public IActionResult Book() => View("ComingSoon", new ComingSoonViewModel(
        "Randevu al", "12 Kasım",
        "Şube, bölüm, hizmet ve hekim seçip hekimin boş saatlerinden randevu alacağınız ekran."));

    public IActionResult Appointments() => View("ComingSoon", new ComingSoonViewModel(
        "Randevularım", "12 Kasım",
        "Yaklaşan ve geçmiş randevularınız; 24 saatten fazla kalan randevuyu buradan iptal edebileceksiniz."));
}

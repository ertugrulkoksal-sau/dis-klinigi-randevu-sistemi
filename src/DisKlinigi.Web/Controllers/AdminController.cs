using DisKlinigi.Web.Models;
using DisKlinigi.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DisKlinigi.Web.Controllers;

/// <summary>
/// Yönetici paneli: şube ve zincir yöneticisi. Şube yöneticisinin sadece kendi şubesini
/// görmesi sayfa politikasıyla değil, içerik kodlanırken <c>CanManageBranch</c> ile sağlanacak
/// (K41). İçerikler 14 Kasım'da dolduruluyor.
/// </summary>
[Authorize(Policy = Policies.Management)]
public class AdminController : Controller
{
    public IActionResult Branches() => View("ComingSoon", new ComingSoonViewModel(
        "Şubeler", "14 Kasım",
        "Şube bilgileri, ünit sayısı, haftalık çalışma saatleri ve barındırılan bölümler."));

    public IActionResult Catalog() => View("ComingSoon", new ComingSoonViewModel(
        "Bölüm ve hizmetler", "14 Kasım",
        "Bölüm kataloğu ve hizmetlerin süre ve ücretleri. Şube yöneticisi bu ekranı salt okunur görecek."));

    public IActionResult Staff() => View("ComingSoon", new ComingSoonViewModel(
        "Şube personeli", "14 Kasım",
        "Şubede çalışan hekim ve asistanlar, şubeye hekim atama ve yeni asistan hesabı."));

    /// <summary>Hekimin uzmanlığı ve hizmetleri bütün şubeler için ortak; sadece zincir yöneticisi (K34).</summary>
    [Authorize(Policy = Policies.ChainAdministrator)]
    public IActionResult Dentists() => View("ComingSoon", new ComingSoonViewModel(
        "Hekim kaydı", "14 Kasım",
        "Zincirdeki bütün hekimler ve yeni hekim kaydı (uzmanlık, verdiği hizmetler, çalışacağı şubeler)."));
}

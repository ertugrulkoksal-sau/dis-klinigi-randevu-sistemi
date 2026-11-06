using System.Diagnostics;
using DisKlinigi.Core.Abstractions;
using DisKlinigi.Web.Models;
using Microsoft.AspNetCore.Mvc;

namespace DisKlinigi.Web.Controllers;

public class HomeController : Controller
{
    private readonly IBranchRepository _branches;

    // Controller somut EF sınıfını değil arayüzü istiyor; hangi sınıfın
    // verileceğine Program.cs'teki DI kaydı karar veriyor.
    public HomeController(IBranchRepository branches)
    {
        _branches = branches;
    }

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var branches = await _branches.GetAllAsync(cancellationToken);
        return View(branches);
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}

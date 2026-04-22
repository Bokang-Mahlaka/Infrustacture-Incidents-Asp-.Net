using System.Diagnostics;
using Mesa_Mohloane_internal.Models;
using Microsoft.AspNetCore.Mvc;

namespace Mesa_Mohloane_internal.Controllers
{
    public class HomeController : Controller
    {
        public IActionResult Index()
        {
            if (User.Identity != null && User.Identity.IsAuthenticated)
            {
                if (User.IsInRole("Admin")) return RedirectToAction("Dashboard", "Admin");
                if (User.IsInRole("Contractor")) return RedirectToAction("Dashboard", "Contractor");
                if (User.IsInRole("Citizen")) return RedirectToAction("Dashboard", "Citizen");
                if (User.IsInRole("Auditor")) return RedirectToAction("Dashboard", "Auditor");
            }
            return View();
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}

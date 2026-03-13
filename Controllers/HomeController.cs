using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Recruit_Finder_AI.Models;

namespace Recruit_Finder_AI.Controllers
{
    public class HomeController : Controller
    {

        public IActionResult Index()
        {
            var categories = new List<string> { "IT", "Marketing", "Finance", "Healthcare", "Education" };
            return View(categories);
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
        public IActionResult About()
        {
            return View();
        }
        public IActionResult MyJobOffers()
        {
            return View();
        }
    }
}

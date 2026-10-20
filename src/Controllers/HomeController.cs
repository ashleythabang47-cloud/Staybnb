using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Staybnb.Data;
using Staybnb.Models;
using Staybnb.ViewModels;

namespace Staybnb.Controllers
{
    public class HomeController : Controller
    {
        private readonly ApplicationDbContext _db;

        public HomeController(ApplicationDbContext db)
        {
            _db = db;
        }

        // Property discovery: only active (approved) listings, filter by city or type
        public async Task<IActionResult> Index(string? city, string? type)
        {
            var active = _db.Properties.Where(p => p.IsActive);

            var query = active
                .Include(p => p.Images)
                .Include(p => p.Reviews)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(city))
                query = query.Where(p => p.City == city);

            if (!string.IsNullOrWhiteSpace(type))
                query = query.Where(p => p.PropertyType == type);

            var model = new HomeIndexViewModel
            {
                Properties = await query.OrderByDescending(p => p.Id).ToListAsync(),
                Cities = await active.Select(p => p.City).Distinct().OrderBy(c => c).ToListAsync(),
                City = city,
                Type = type
            };

            return View(model);
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

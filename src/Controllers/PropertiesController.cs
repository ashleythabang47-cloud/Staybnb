using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Staybnb.Data;
using Staybnb.Models;
using Staybnb.Models.Enums;
using Staybnb.Services;
using Staybnb.ViewModels;

namespace Staybnb.Controllers
{
    // Public pages: anyone can browse properties
    public class PropertiesController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;

        public PropertiesController(ApplicationDbContext db, UserManager<ApplicationUser> userManager)
        {
            _db = db;
            _userManager = userManager;
        }

        public async Task<IActionResult> Details(int id)
        {
            var property = await _db.Properties
                .Include(p => p.Host)
                .Include(p => p.Images)
                .Include(p => p.Amenities)
                .Include(p => p.Reviews).ThenInclude(r => r.Reviewer)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (property == null)
                return NotFound();

            // Inactive listings are visible only to their host and to admins
            if (!property.IsActive)
            {
                var isOwner = User.Identity?.IsAuthenticated == true && property.HostId == _userManager.GetUserId(User);
                var isAdmin = User.IsInRole("Admin") || User.IsInRole("SuperAdmin");
                if (!isOwner && !isAdmin)
                    return NotFound();
            }

            var model = new PropertyDetailsViewModel
            {
                Property = property,
                ReviewCount = property.Reviews.Count,
                AverageRating = property.Reviews.Any() ? property.Reviews.Average(r => r.Rating) : 0
            };

            return View(model);
        }

        // Live price preview used by the booking widget (returns JSON)
        [HttpGet]
        public async Task<IActionResult> Quote(int id, DateTime checkIn, DateTime checkOut)
        {
            var property = await _db.Properties.FirstOrDefaultAsync(p => p.Id == id && p.IsActive);
            if (property == null)
                return NotFound();

            var nights = (checkOut.Date - checkIn.Date).Days;
            if (nights < 1)
                return Json(new { valid = false, message = "Check-out must be after check-in." });

            var price = PricingCalculator.Calculate(property.PricePerNight, property.CleaningFee, nights);

            var overlap = await _db.Bookings.AnyAsync(b =>
                b.PropertyId == id &&
                (b.Status == BookingStatus.Approved || b.Status == BookingStatus.CheckedIn) &&
                b.CheckInDate < checkOut.Date && b.CheckOutDate > checkIn.Date);

            return Json(new
            {
                valid = !overlap,
                message = overlap ? "These dates are not available." : "",
                nights = price.Nights,
                pricePerNight = property.PricePerNight,
                subtotal = price.Subtotal,
                cleaningFee = price.CleaningFee,
                serviceFee = price.ServiceFee,
                total = price.Total
            });
        }
    }
}

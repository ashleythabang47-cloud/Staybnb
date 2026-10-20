using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
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
    [Authorize(Roles = "Host")]
    public class HostController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IActivityLogService _log;

        public HostController(
            ApplicationDbContext db,
            UserManager<ApplicationUser> userManager,
            IActivityLogService log)
        {
            _db = db;
            _userManager = userManager;
            _log = log;
        }

        // ---------- Dashboard ----------

        public async Task<IActionResult> Dashboard()
        {
            var hostId = _userManager.GetUserId(User)!;
            var host = await _userManager.GetUserAsync(User);

            var myProperties = _db.Properties.Where(p => p.HostId == hostId);
            var myBookings = _db.Bookings.Where(b => b.Property!.HostId == hostId);

            var model = new HostDashboardViewModel
            {
                HostName = host?.FullName ?? "Host",
                TotalProperties = await myProperties.CountAsync(),
                ActiveProperties = await myProperties.CountAsync(p => p.IsActive),
                TotalBookings = await myBookings.CountAsync(),
                PendingBookings = await myBookings.CountAsync(b => b.Status == BookingStatus.Pending),
                TotalEarnings = await _db.Payments
                    .Where(p => p.Booking!.Property!.HostId == hostId && p.Status == PaymentStatus.Paid)
                    .SumAsync(p => p.Amount),
                AverageRating = await _db.Reviews
                    .Where(r => r.Property!.HostId == hostId)
                    .Select(r => (double?)r.Rating)
                    .AverageAsync(),
                ReviewCount = await _db.Reviews.CountAsync(r => r.Property!.HostId == hostId),
                RecentBookings = await myBookings
                    .Include(b => b.Guest)
                    .Include(b => b.Property)
                    .OrderByDescending(b => b.Id)
                    .Take(5)
                    .ToListAsync()
            };

            return View(model);
        }

        // ---------- My properties ----------

        public async Task<IActionResult> Properties()
        {
            var hostId = _userManager.GetUserId(User)!;

            var properties = await _db.Properties
                .Include(p => p.Images)
                .Include(p => p.Bookings)
                .Where(p => p.HostId == hostId)
                .OrderByDescending(p => p.Id)
                .ToListAsync();

            var approvals = await _db.HostApplications
                .Where(a => a.ApplicationUserId == hostId)
                .ToDictionaryAsync(a => a.PropertyId, a => a.Status);

            var rows = properties.Select(p => new PropertyRowViewModel
            {
                Property = p,
                ApprovalStatus = approvals.TryGetValue(p.Id, out var s) ? s : null,
                BookingCount = p.Bookings.Count
            }).ToList();

            return View(rows);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleActive(int id)
        {
            var hostId = _userManager.GetUserId(User)!;

            // Ownership check: a host can only change their own properties
            var property = await _db.Properties.FirstOrDefaultAsync(p => p.Id == id && p.HostId == hostId);
            if (property == null)
                return NotFound();

            // A property that an admin has not approved can never be switched on
            var application = await _db.HostApplications.FirstOrDefaultAsync(a => a.PropertyId == id);
            if (application != null && application.Status != ApplicationStatus.Approved)
            {
                TempData["Error"] = "This property has not been approved by an administrator yet.";
                return RedirectToAction(nameof(Properties));
            }

            property.IsActive = !property.IsActive;
            await _db.SaveChangesAsync();

            await _log.LogAsync(hostId,
                $"Property '{property.Title}' set to {(property.IsActive ? "active" : "inactive")}",
                ActivityType.PropertyChange);

            TempData["Success"] = property.IsActive
                ? $"'{property.Title}' is now visible to guests."
                : $"'{property.Title}' is now hidden from guests.";
            return RedirectToAction(nameof(Properties));
        }

        // ---------- Edit property ----------

        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var hostId = _userManager.GetUserId(User)!;

            var property = await _db.Properties
                .Include(p => p.Amenities)
                .Include(p => p.Images)
                .FirstOrDefaultAsync(p => p.Id == id && p.HostId == hostId);

            if (property == null)
                return NotFound();

            var model = new PropertyEditViewModel
            {
                Id = property.Id,
                Title = property.Title,
                Description = property.Description,
                PricePerNight = property.PricePerNight,
                CleaningFee = property.CleaningFee,
                Address = property.Address,
                City = property.City,
                PropertyType = property.PropertyType,
                MaxGuests = property.MaxGuests,
                SelectedAmenityIds = property.Amenities.Select(a => a.Id).ToList(),
                ImageUrls = property.Images.Select(i => i.ImageUrl).ToList(),
                AvailableAmenities = await _db.Amenities.OrderBy(a => a.Name).ToListAsync()
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(PropertyEditViewModel model)
        {
            var hostId = _userManager.GetUserId(User)!;

            var property = await _db.Properties
                .Include(p => p.Amenities)
                .Include(p => p.Images)
                .FirstOrDefaultAsync(p => p.Id == model.Id && p.HostId == hostId);

            if (property == null)
                return NotFound();

            if (!ModelState.IsValid)
            {
                model.AvailableAmenities = await _db.Amenities.OrderBy(a => a.Name).ToListAsync();
                model.ImageUrls = property.Images.Select(i => i.ImageUrl).ToList();
                return View(model);
            }

            property.Title = model.Title;
            property.Description = model.Description;
            property.PricePerNight = model.PricePerNight;
            property.CleaningFee = model.CleaningFee;
            property.Address = model.Address;
            property.City = model.City;
            property.PropertyType = model.PropertyType;
            property.MaxGuests = model.MaxGuests;

            property.Amenities.Clear();
            var chosen = await _db.Amenities
                .Where(a => model.SelectedAmenityIds.Contains(a.Id))
                .ToListAsync();
            foreach (var amenity in chosen)
                property.Amenities.Add(amenity);

            await _db.SaveChangesAsync();
            await _log.LogAsync(hostId, $"Property '{property.Title}' updated", ActivityType.PropertyChange);

            TempData["Success"] = "Property updated.";
            return RedirectToAction(nameof(Properties));
        }

        // ---------- Check-in process (rules and required documents) ----------

        [HttpGet]
        public async Task<IActionResult> ConfigureCheckIn(int id)
        {
            var hostId = _userManager.GetUserId(User)!;

            var property = await _db.Properties
                .FirstOrDefaultAsync(p => p.Id == id && p.HostId == hostId);
            if (property == null)
                return NotFound();

            var existing = await _db.CheckInProcesses.FirstOrDefaultAsync(c => c.PropertyId == id);

            var model = new CheckInSetupViewModel
            {
                PropertyId = property.Id,
                PropertyTitle = property.Title,
                Title = existing?.Title ?? $"Check-in for {property.Title}",
                Rules = "",
                Documents = existing == null ? "ID or Passport" : ""
            };

            if (existing != null)
            {
                var config = ReadConfig(existing.StepsJson);
                model.Rules = string.Join(Environment.NewLine, config.Rules);
                model.Documents = string.Join(Environment.NewLine, config.Documents);
            }

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ConfigureCheckIn(CheckInSetupViewModel model)
        {
            var hostId = _userManager.GetUserId(User)!;

            var property = await _db.Properties
                .FirstOrDefaultAsync(p => p.Id == model.PropertyId && p.HostId == hostId);
            if (property == null)
                return NotFound();

            if (!ModelState.IsValid)
            {
                model.PropertyTitle = property.Title;
                return View(model);
            }

            var config = new CheckInConfig
            {
                Rules = SplitLines(model.Rules),
                Documents = SplitLines(model.Documents)
            };
            if (config.Documents.Count == 0)
                config.Documents.Add("ID or Passport");

            var json = JsonSerializer.Serialize(config);

            var process = await _db.CheckInProcesses.FirstOrDefaultAsync(c => c.PropertyId == property.Id);
            if (process == null)
            {
                process = new CheckInProcess { PropertyId = property.Id };
                _db.CheckInProcesses.Add(process);
            }

            process.Title = model.Title;
            process.StepsJson = json;
            process.CreatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();
            await _log.LogAsync(hostId, $"Check-in process saved for '{property.Title}'", ActivityType.PropertyChange);

            TempData["Success"] = "Check-in process saved.";
            return RedirectToAction(nameof(Properties));
        }

        // ---------- helpers ----------

        private static List<string> SplitLines(string? text)
        {
            return (text ?? "")
                .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToList();
        }

        private static CheckInConfig ReadConfig(string json)
        {
            try
            {
                return JsonSerializer.Deserialize<CheckInConfig>(json) ?? new CheckInConfig();
            }
            catch (JsonException)
            {
                return new CheckInConfig();
            }
        }
    }
}

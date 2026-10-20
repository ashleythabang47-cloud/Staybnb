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
    [Authorize(Roles = "Guest")]
    public class HostApplicationsController : Controller
    {
        private static readonly string[] AllowedExtensions = { ".jpg", ".jpeg", ".png", ".webp" };
        private const long MaxImageBytes = 5 * 1024 * 1024; // 5 MB per image

        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IWebHostEnvironment _env;
        private readonly IActivityLogService _log;
        private readonly INotificationService _notifications;

        public HostApplicationsController(
            ApplicationDbContext db,
            UserManager<ApplicationUser> userManager,
            IWebHostEnvironment env,
            IActivityLogService log,
            INotificationService notifications)
        {
            _db = db;
            _userManager = userManager;
            _env = env;
            _log = log;
            _notifications = notifications;
        }

        // ---------- Apply ----------

        [HttpGet]
        public async Task<IActionResult> Create()
        {
            var userId = _userManager.GetUserId(User)!;

            // One pending application at a time
            var hasPending = await _db.HostApplications
                .AnyAsync(a => a.ApplicationUserId == userId && a.Status == ApplicationStatus.Pending);
            if (hasPending)
            {
                TempData["Error"] = "You already have an application waiting for review.";
                return RedirectToAction(nameof(Status));
            }

            var model = new HostApplicationViewModel
            {
                AvailableAmenities = await _db.Amenities.OrderBy(a => a.Name).ToListAsync()
            };
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(HostApplicationViewModel model, List<IFormFile> images)
        {
            var userId = _userManager.GetUserId(User)!;

            // Rule from the brief: an application cannot be submitted without an image
            var files = images?.Where(f => f != null && f.Length > 0).ToList() ?? new List<IFormFile>();
            if (files.Count == 0)
                ModelState.AddModelError("images", "Please upload at least one property image.");

            foreach (var file in files)
            {
                var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
                if (!AllowedExtensions.Contains(ext))
                    ModelState.AddModelError("images", $"'{file.FileName}' is not a supported image (use JPG, PNG or WEBP).");
                else if (file.Length > MaxImageBytes)
                    ModelState.AddModelError("images", $"'{file.FileName}' is larger than 5 MB.");
            }

            if (!ModelState.IsValid)
            {
                model.AvailableAmenities = await _db.Amenities.OrderBy(a => a.Name).ToListAsync();
                return View(model);
            }

            // 1. Save the image files to wwwroot/uploads/properties
            var folder = Path.Combine(_env.WebRootPath, "uploads", "properties");
            Directory.CreateDirectory(folder);

            var savedPaths = new List<string>();
            var property = new HostProperty
            {
                Title = model.Title,
                Description = model.Description,
                PricePerNight = model.PricePerNight,
                CleaningFee = model.CleaningFee,
                Address = model.Address,
                City = model.City,
                PropertyType = model.PropertyType,
                MaxGuests = model.MaxGuests,
                IsActive = false, // hidden until an admin approves the application
                HostId = userId
            };

            try
            {
                foreach (var file in files)
                {
                    var fileName = Guid.NewGuid().ToString("N") + Path.GetExtension(file.FileName).ToLowerInvariant();
                    var fullPath = Path.Combine(folder, fileName);

                    using (var stream = new FileStream(fullPath, FileMode.Create))
                    {
                        await file.CopyToAsync(stream);
                    }

                    savedPaths.Add(fullPath);
                    property.Images.Add(new PropertyImage { ImageUrl = "/uploads/properties/" + fileName });
                }

                // 2. Amenities chosen on the form
                if (model.SelectedAmenityIds.Count > 0)
                {
                    property.Amenities = await _db.Amenities
                        .Where(a => model.SelectedAmenityIds.Contains(a.Id))
                        .ToListAsync();
                }

                // 3. Property, images and application are saved together (all or nothing)
                var application = new HostApplication
                {
                    ApplicationUserId = userId,
                    Property = property,
                    Status = ApplicationStatus.Pending,
                    AppliedAt = DateTime.UtcNow
                };

                _db.HostApplications.Add(application);
                await _db.SaveChangesAsync();
            }
            catch
            {
                // If anything failed, do not leave orphan image files behind
                foreach (var path in savedPaths)
                {
                    if (System.IO.File.Exists(path))
                        System.IO.File.Delete(path);
                }
                throw;
            }

            await _log.LogAsync(userId, $"Host application submitted: {property.Title}", ActivityType.ApplicationSubmitted);
            await _notifications.NotifyAsync(userId, "Host application submitted",
                $"Your application for '{property.Title}' was received and is under review.", NotificationType.Application);
            await _notifications.NotifyAdminsAsync("New host application",
                $"A new host application for '{property.Title}' needs review.", NotificationType.Application);

            TempData["Success"] = "Application submitted. An administrator will review it soon.";
            return RedirectToAction(nameof(Status));
        }

        // ---------- Status ----------

        [HttpGet]
        public async Task<IActionResult> Status()
        {
            var userId = _userManager.GetUserId(User)!;

            var applications = await _db.HostApplications
                .Include(a => a.Property)
                    .ThenInclude(p => p!.Images)
                .Where(a => a.ApplicationUserId == userId)
                .OrderByDescending(a => a.AppliedAt)
                .ToListAsync();

            return View(applications);
        }
    }
}

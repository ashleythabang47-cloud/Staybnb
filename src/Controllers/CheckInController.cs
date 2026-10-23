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
    // The guest side of check-in: Step 1 rules, Step 2 documents, Step 3 review and submit
    [Authorize(Roles = "Guest")]
    public class CheckInController : Controller
    {
        private static readonly string[] AllowedExtensions = { ".jpg", ".jpeg", ".png", ".pdf" };
        private const long MaxFileBytes = 5 * 1024 * 1024; // 5 MB

        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IWebHostEnvironment _env;
        private readonly IActivityLogService _log;
        private readonly INotificationService _notifications;

        public CheckInController(
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

        // ---------- Step 1: rules ----------

        [HttpGet]
        public async Task<IActionResult> Start(int bookingId)
        {
            var booking = await LoadBookingAsync(bookingId);
            if (booking == null) return NotFound();

            var redirect = CheckAccess(booking, bookingId);
            if (redirect != null) return redirect;

            return View(BuildModel(booking));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Start(int bookingId, bool agree)
        {
            var booking = await LoadBookingAsync(bookingId);
            if (booking == null) return NotFound();

            var redirect = CheckAccess(booking, bookingId);
            if (redirect != null) return redirect;

            if (!agree)
            {
                TempData["Error"] = "Please confirm that you accept the house rules.";
                return RedirectToAction(nameof(Start), new { bookingId });
            }

            if (booking.GuestCheckIn == null)
            {
                var process = await GetOrCreateProcessAsync(booking.Property!);

                _db.GuestCheckIns.Add(new GuestCheckIn
                {
                    BookingId = booking.Id,
                    CheckInProcessId = process.Id,
                    Status = CheckInStatus.InProgress,
                    CreatedAt = DateTime.UtcNow
                });
                await _db.SaveChangesAsync();

                await _log.LogAsync(booking.GuestId, $"Check-in started for booking #{booking.Id}", ActivityType.CheckIn);
            }

            return RedirectToAction(nameof(Documents), new { bookingId });
        }

        // ---------- Step 2: documents ----------

        [HttpGet]
        public async Task<IActionResult> Documents(int bookingId)
        {
            var booking = await LoadBookingAsync(bookingId);
            if (booking == null) return NotFound();

            var redirect = CheckAccess(booking, bookingId);
            if (redirect != null) return redirect;

            if (booking.GuestCheckIn == null)
                return RedirectToAction(nameof(Start), new { bookingId });

            return View(BuildModel(booking));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Documents(int bookingId, string? unused = null)
        {
            var booking = await LoadBookingAsync(bookingId);
            if (booking == null) return NotFound();

            var redirect = CheckAccess(booking, bookingId);
            if (redirect != null) return redirect;

            var checkIn = booking.GuestCheckIn;
            if (checkIn == null)
                return RedirectToAction(nameof(Start), new { bookingId });

            var config = ReadConfig(booking.Property!.CheckInProcess);

            // 1. Validate every uploaded file first
            var uploads = new List<(string DocType, IFormFile File)>();
            for (var i = 0; i < config.Documents.Count; i++)
            {
                var file = Request.Form.Files["doc_" + i];
                if (file == null || file.Length == 0)
                    continue;

                var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
                if (!AllowedExtensions.Contains(ext))
                {
                    TempData["Error"] = $"'{file.FileName}' is not allowed. Use JPG, PNG or PDF.";
                    return RedirectToAction(nameof(Documents), new { bookingId });
                }
                if (file.Length > MaxFileBytes)
                {
                    TempData["Error"] = $"'{file.FileName}' is larger than 5 MB.";
                    return RedirectToAction(nameof(Documents), new { bookingId });
                }

                uploads.Add((config.Documents[i], file));
            }

            if (uploads.Count == 0)
            {
                TempData["Error"] = "Please choose at least one file to upload.";
                return RedirectToAction(nameof(Documents), new { bookingId });
            }

            // 2. Save the files OUTSIDE wwwroot, so they cannot be opened by a public URL
            var folder = GetDocumentFolder();
            Directory.CreateDirectory(folder);

            foreach (var (docType, file) in uploads)
            {
                var existing = checkIn.Documents.FirstOrDefault(d => d.DocumentType == docType);

                // A verified document is final and cannot be replaced
                if (existing != null && existing.Status == DocumentStatus.Verified)
                    continue;

                var storedName = Guid.NewGuid().ToString("N") + Path.GetExtension(file.FileName).ToLowerInvariant();
                using (var stream = new FileStream(Path.Combine(folder, storedName), FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }

                if (existing != null)
                {
                    var oldPath = Path.Combine(folder, existing.FileName);
                    if (System.IO.File.Exists(oldPath))
                        System.IO.File.Delete(oldPath);

                    existing.FileName = storedName;
                    existing.Status = DocumentStatus.Pending;
                }
                else
                {
                    checkIn.Documents.Add(new GuestDocument
                    {
                        DocumentType = docType,
                        FileName = storedName,
                        Status = DocumentStatus.Pending
                    });
                }
            }

            checkIn.Status = CheckInStatus.InProgress;
            await _db.SaveChangesAsync();

            await _log.LogAsync(booking.GuestId, $"Documents uploaded for booking #{booking.Id}", ActivityType.CheckIn);

            return RedirectToAction(nameof(Summary), new { bookingId });
        }

        // ---------- Step 3: review and submit ----------

        [HttpGet]
        public async Task<IActionResult> Summary(int bookingId)
        {
            var booking = await LoadBookingAsync(bookingId);
            if (booking == null) return NotFound();

            if (booking.GuestCheckIn == null)
                return RedirectToAction(nameof(Start), new { bookingId });

            return View(BuildModel(booking));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Submit(int bookingId)
        {
            var booking = await LoadBookingAsync(bookingId);
            if (booking == null) return NotFound();

            var checkIn = booking.GuestCheckIn;
            if (checkIn == null)
                return RedirectToAction(nameof(Start), new { bookingId });

            if (booking.Status != BookingStatus.Approved ||
                (checkIn.Status != CheckInStatus.InProgress && checkIn.Status != CheckInStatus.Rejected))
            {
                TempData["Error"] = "This check-in cannot be submitted right now.";
                return RedirectToAction(nameof(Summary), new { bookingId });
            }

            var config = ReadConfig(booking.Property!.CheckInProcess);
            if (!AllDocumentsReady(config, checkIn))
            {
                TempData["Error"] = "Please upload every required document before submitting.";
                return RedirectToAction(nameof(Documents), new { bookingId });
            }

            checkIn.Status = CheckInStatus.Submitted;
            await _db.SaveChangesAsync();

            await _log.LogAsync(booking.GuestId, $"Check-in submitted for booking #{booking.Id}", ActivityType.CheckIn);
            await _notifications.NotifyAsync(booking.Property.HostId, "Check-in documents submitted",
                $"A guest submitted their check-in documents for '{booking.Property.Title}'. Please review them.",
                NotificationType.Booking);

            TempData["Success"] = "Check-in submitted. Your host will verify your documents.";
            return RedirectToAction(nameof(Summary), new { bookingId });
        }

        // ---------- helpers ----------

        private async Task<Booking?> LoadBookingAsync(int bookingId)
        {
            var guestId = _userManager.GetUserId(User)!;

            return await _db.Bookings
                .Include(b => b.Property).ThenInclude(p => p!.CheckInProcess)
                .Include(b => b.GuestCheckIn).ThenInclude(c => c!.Documents)
                .FirstOrDefaultAsync(b => b.Id == bookingId && b.GuestId == guestId);
        }

        // Returns a redirect when the guest may not edit this check-in right now
        private IActionResult? CheckAccess(Booking booking, int bookingId)
        {
            if (booking.Status == BookingStatus.CheckedIn)
                return RedirectToAction(nameof(Summary), new { bookingId });

            if (booking.Status != BookingStatus.Approved)
            {
                TempData["Error"] = "Check-in is only available for approved bookings.";
                return RedirectToAction("Index", "Bookings");
            }

            var checkIn = booking.GuestCheckIn;
            if (checkIn != null &&
                (checkIn.Status == CheckInStatus.Submitted || checkIn.Status == CheckInStatus.Verified))
            {
                return RedirectToAction(nameof(Summary), new { bookingId });
            }

            return null;
        }

        private CheckInFlowViewModel BuildModel(Booking booking)
        {
            var config = ReadConfig(booking.Property?.CheckInProcess);
            var checkIn = booking.GuestCheckIn;

            return new CheckInFlowViewModel
            {
                Booking = booking,
                Config = config,
                CheckIn = checkIn,
                CanSubmit = booking.Status == BookingStatus.Approved
                    && checkIn != null
                    && (checkIn.Status == CheckInStatus.InProgress || checkIn.Status == CheckInStatus.Rejected)
                    && AllDocumentsReady(config, checkIn)
            };
        }

        // Every required document type has a file that has not been rejected
        private static bool AllDocumentsReady(CheckInConfig config, GuestCheckIn checkIn)
        {
            return config.Documents.All(type =>
                checkIn.Documents.Any(d => d.DocumentType == type && d.Status != DocumentStatus.Rejected));
        }

        private static CheckInConfig ReadConfig(CheckInProcess? process)
        {
            var config = new CheckInConfig();

            if (process != null)
            {
                try
                {
                    config = JsonSerializer.Deserialize<CheckInConfig>(process.StepsJson) ?? new CheckInConfig();
                }
                catch (JsonException)
                {
                    config = new CheckInConfig();
                }
            }

            if (config.Documents.Count == 0)
                config.Documents.Add("ID or Passport");

            return config;
        }

        // If the host never configured a check-in process, create a default one
        private async Task<CheckInProcess> GetOrCreateProcessAsync(HostProperty property)
        {
            if (property.CheckInProcess != null)
                return property.CheckInProcess;

            var process = new CheckInProcess
            {
                PropertyId = property.Id,
                Title = $"Check-in for {property.Title}",
                StepsJson = JsonSerializer.Serialize(new CheckInConfig
                {
                    Documents = new List<string> { "ID or Passport" }
                }),
                CreatedAt = DateTime.UtcNow
            };

            _db.CheckInProcesses.Add(process);
            await _db.SaveChangesAsync();
            return process;
        }

        private string GetDocumentFolder()
        {
            return Path.Combine(_env.ContentRootPath, "App_Data", "guest-documents");
        }
    }
}

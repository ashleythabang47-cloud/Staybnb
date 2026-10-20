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
    [Authorize(Roles = "Admin,SuperAdmin")]
    public class AdminController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IActivityLogService _log;
        private readonly INotificationService _notifications;

        public AdminController(
            ApplicationDbContext db,
            UserManager<ApplicationUser> userManager,
            IActivityLogService log,
            INotificationService notifications)
        {
            _db = db;
            _userManager = userManager;
            _log = log;
            _notifications = notifications;
        }

        // ---------- Dashboard ----------

        public async Task<IActionResult> Dashboard()
        {
            var hosts = await _userManager.GetUsersInRoleAsync("Host");

            var model = new AdminDashboardViewModel
            {
                TotalUsers = await _userManager.Users.CountAsync(),
                PendingApplications = await _db.HostApplications.CountAsync(a => a.Status == ApplicationStatus.Pending),
                ActiveProperties = await _db.Properties.CountAsync(p => p.IsActive),
                PlatformRevenue = await _db.Payments
                    .Where(p => p.Status == PaymentStatus.Paid)
                    .SumAsync(p => p.Amount),
                TotalBookings = await _db.Bookings.CountAsync(),
                PendingBookings = await _db.Bookings.CountAsync(b => b.Status == BookingStatus.Pending),
                ApprovedHosts = hosts.Count,
                RecentActivity = await _db.ActivityLogs
                    .Include(l => l.User)
                    .OrderByDescending(l => l.CreatedAt)
                    .Take(8)
                    .ToListAsync(),
                RecentApplications = await _db.HostApplications
                    .Include(a => a.ApplicationUser)
                    .Include(a => a.Property)
                    .OrderByDescending(a => a.AppliedAt)
                    .Take(5)
                    .ToListAsync()
            };

            return View(model);
        }

        // ---------- Application review ----------

        public async Task<IActionResult> Applications(string? status)
        {
            var query = _db.HostApplications
                .Include(a => a.ApplicationUser)
                .Include(a => a.Property)
                .AsQueryable();

            if (Enum.TryParse<ApplicationStatus>(status, true, out var parsed))
                query = query.Where(a => a.Status == parsed);

            ViewData["CurrentStatus"] = status ?? "All";

            var list = await query.OrderByDescending(a => a.AppliedAt).ToListAsync();
            return View(list);
        }

        public async Task<IActionResult> ApplicationDetails(int id)
        {
            var application = await _db.HostApplications
                .Include(a => a.ApplicationUser)
                .Include(a => a.Property).ThenInclude(p => p!.Images)
                .Include(a => a.Property).ThenInclude(p => p!.Amenities)
                .FirstOrDefaultAsync(a => a.Id == id);

            if (application == null)
                return NotFound();

            return View(application);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Approve(int id)
        {
            var application = await _db.HostApplications
                .Include(a => a.ApplicationUser)
                .Include(a => a.Property)
                .FirstOrDefaultAsync(a => a.Id == id);

            if (application == null)
                return NotFound();

            if (application.Status != ApplicationStatus.Pending)
            {
                TempData["Error"] = "This application has already been reviewed.";
                return RedirectToAction(nameof(Applications));
            }

            var applicant = application.ApplicationUser!;

            // THE ROLE SWAP: add Host, remove Guest (a user is a Guest or a Host, not both)
            if (!await _userManager.IsInRoleAsync(applicant, "Host"))
            {
                var addResult = await _userManager.AddToRoleAsync(applicant, "Host");
                if (!addResult.Succeeded)
                {
                    TempData["Error"] = "Could not add the Host role: " +
                        string.Join(", ", addResult.Errors.Select(e => e.Description));
                    return RedirectToAction(nameof(ApplicationDetails), new { id });
                }
            }

            if (await _userManager.IsInRoleAsync(applicant, "Guest"))
            {
                var removeResult = await _userManager.RemoveFromRoleAsync(applicant, "Guest");
                if (!removeResult.Succeeded)
                {
                    TempData["Error"] = "Could not remove the Guest role: " +
                        string.Join(", ", removeResult.Errors.Select(e => e.Description));
                    return RedirectToAction(nameof(ApplicationDetails), new { id });
                }
            }

            application.Status = ApplicationStatus.Approved;
            if (application.Property != null)
                application.Property.IsActive = true;

            await _db.SaveChangesAsync();

            var adminId = _userManager.GetUserId(User);
            await _log.LogAsync(adminId, $"Approved host application #{application.Id}", ActivityType.ApplicationDecision);
            await _log.LogAsync(applicant.Id, "Role changed: Guest to Host", ActivityType.RoleChange);

            await _notifications.NotifyAsync(applicant.Id, "Host Application Approved",
                $"Congratulations! Your host application for '{application.Property?.Title}' has been approved.",
                NotificationType.Success);

            TempData["Success"] = $"Approved. {applicant.FullName} is now a Host.";
            return RedirectToAction(nameof(Applications));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reject(int id, string? reason)
        {
            var application = await _db.HostApplications
                .Include(a => a.ApplicationUser)
                .Include(a => a.Property)
                .FirstOrDefaultAsync(a => a.Id == id);

            if (application == null)
                return NotFound();

            if (application.Status != ApplicationStatus.Pending)
            {
                TempData["Error"] = "This application has already been reviewed.";
                return RedirectToAction(nameof(Applications));
            }

            application.Status = ApplicationStatus.Rejected;
            await _db.SaveChangesAsync();

            var adminId = _userManager.GetUserId(User);
            await _log.LogAsync(adminId, $"Rejected host application #{application.Id}", ActivityType.ApplicationDecision);

            var message = $"Your host application for '{application.Property?.Title}' was not approved.";
            if (!string.IsNullOrWhiteSpace(reason))
                message += $" Reason: {reason.Trim()}";

            await _notifications.NotifyAsync(application.ApplicationUserId, "Host Application Rejected",
                message, NotificationType.Warning);

            TempData["Success"] = "Application rejected.";
            return RedirectToAction(nameof(Applications));
        }
    }
}

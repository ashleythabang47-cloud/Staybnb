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

        // ---------- User management (SuperAdmin only) ----------

        [Authorize(Roles = "SuperAdmin")]
        public async Task<IActionResult> Users(string? search)
        {
            var query = _userManager.Users.AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim();
                query = query.Where(u =>
                    (u.Email != null && u.Email.Contains(term)) ||
                    u.FirstName.Contains(term) ||
                    u.LastName.Contains(term));
            }

            var users = await query.OrderBy(u => u.FirstName).ThenBy(u => u.LastName).ToListAsync();

            var roleMap = await (from ur in _db.UserRoles
                                 join r in _db.Roles on ur.RoleId equals r.Id
                                 select new { ur.UserId, RoleName = r.Name })
                                .ToListAsync();

            var rows = users.Select(u => new UserRowViewModel
            {
                User = u,
                Roles = roleMap.Where(x => x.UserId == u.Id)
                               .Select(x => x.RoleName ?? "")
                               .ToList()
            }).ToList();

            ViewData["Search"] = search;
            return View(rows);
        }

        [Authorize(Roles = "SuperAdmin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> PromoteToAdmin(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
                return NotFound();

            // Only existing Guests can be promoted
            if (!await _userManager.IsInRoleAsync(user, "Guest"))
            {
                TempData["Error"] = "Only Guests can be promoted to Admin.";
                return RedirectToAction(nameof(Users));
            }

            var add = await _userManager.AddToRoleAsync(user, "Admin");
            if (!add.Succeeded)
            {
                TempData["Error"] = "Could not promote the user: " +
                    string.Join(", ", add.Errors.Select(e => e.Description));
                return RedirectToAction(nameof(Users));
            }

            await _userManager.RemoveFromRoleAsync(user, "Guest");

            var actorId = _userManager.GetUserId(User);
            await _log.LogAsync(actorId, $"Promoted {user.Email} to Admin", ActivityType.RoleChange);
            await _log.LogAsync(user.Id, "Role changed: Guest to Admin", ActivityType.RoleChange);
            await _notifications.NotifyAsync(user.Id, "You are now an Admin",
                "A SuperAdmin promoted your account to Admin.", NotificationType.Success);

            TempData["Success"] = $"{user.FullName} is now an Admin.";
            return RedirectToAction(nameof(Users));
        }

        [Authorize(Roles = "SuperAdmin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DemoteAdmin(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
                return NotFound();

            // The SuperAdmin account can never be demoted
            if (await _userManager.IsInRoleAsync(user, "SuperAdmin") ||
                !await _userManager.IsInRoleAsync(user, "Admin"))
            {
                TempData["Error"] = "Only regular Admins can be demoted.";
                return RedirectToAction(nameof(Users));
            }

            await _userManager.RemoveFromRoleAsync(user, "Admin");
            await _userManager.AddToRoleAsync(user, "Guest");

            var actorId = _userManager.GetUserId(User);
            await _log.LogAsync(actorId, $"Demoted {user.Email} to Guest", ActivityType.RoleChange);
            await _log.LogAsync(user.Id, "Role changed: Admin to Guest", ActivityType.RoleChange);
            await _notifications.NotifyAsync(user.Id, "Admin role removed",
                "Your Admin role was removed and your account is now a Guest.", NotificationType.Warning);

            TempData["Success"] = $"{user.FullName} is now a Guest.";
            return RedirectToAction(nameof(Users));
        }

        // ---------- System audit ----------

        public async Task<IActionResult> ActivityLogs(string? type, string? search, int page = 1)
        {
            const int pageSize = 25;
            if (page < 1) page = 1;

            var query = _db.ActivityLogs.Include(l => l.User).AsQueryable();

            if (Enum.TryParse<ActivityType>(type, true, out var parsed))
                query = query.Where(l => l.ActivityType == parsed);

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim();
                query = query.Where(l => l.Action.Contains(term) ||
                                         (l.User != null && l.User.Email != null && l.User.Email.Contains(term)));
            }

            var total = await query.CountAsync();

            var logs = await query
                .OrderByDescending(l => l.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var model = new ActivityLogsViewModel
            {
                Logs = logs,
                Type = type,
                Search = search,
                Page = page,
                TotalPages = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize)),
                TotalCount = total
            };

            return View(model);
        }
    }
}

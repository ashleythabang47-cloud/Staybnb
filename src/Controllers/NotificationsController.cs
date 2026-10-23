using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Staybnb.Data;
using Staybnb.Models;
using Staybnb.Models.Enums;
using Staybnb.ViewModels;

namespace Staybnb.Controllers
{
    // Any logged-in user has their own notification center
    [Authorize]
    public class NotificationsController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;

        public NotificationsController(ApplicationDbContext db, UserManager<ApplicationUser> userManager)
        {
            _db = db;
            _userManager = userManager;
        }

        public async Task<IActionResult> Index(int? selectedId, string? filter)
        {
            var userId = _userManager.GetUserId(User)!;
            filter = (filter ?? "all").ToLowerInvariant();

            // Opening a notification marks it as read (saved in the database)
            Notification? selected = null;
            if (selectedId.HasValue)
            {
                selected = await _db.Notifications
                    .FirstOrDefaultAsync(n => n.Id == selectedId.Value && n.UserId == userId);

                if (selected != null && !selected.IsRead)
                {
                    selected.IsRead = true;
                    await _db.SaveChangesAsync();
                }
            }

            var mine = _db.Notifications.Where(n => n.UserId == userId);

            var query = mine;
            if (filter == "unread")
                query = query.Where(n => !n.IsRead);
            else if (filter == "read")
                query = query.Where(n => n.IsRead);

            var model = new NotificationsViewModel
            {
                Filter = filter,
                Items = await query.OrderByDescending(n => n.CreatedAt).Take(100).ToListAsync(),
                Selected = selected,
                SelectedActionUrl = selected == null ? null : GetActionUrl(selected),
                TotalCount = await mine.CountAsync(),
                UnreadCount = await mine.CountAsync(n => !n.IsRead),
                ReadCount = await mine.CountAsync(n => n.IsRead)
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkAllRead()
        {
            var userId = _userManager.GetUserId(User)!;

            var unread = await _db.Notifications
                .Where(n => n.UserId == userId && !n.IsRead)
                .ToListAsync();

            foreach (var n in unread)
                n.IsRead = true;

            await _db.SaveChangesAsync();

            TempData["Success"] = "All notifications marked as read.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var userId = _userManager.GetUserId(User)!;

            var notification = await _db.Notifications
                .FirstOrDefaultAsync(n => n.Id == id && n.UserId == userId);

            if (notification != null)
            {
                _db.Notifications.Remove(notification);
                await _db.SaveChangesAsync();
            }

            return RedirectToAction(nameof(Index));
        }

        // Where the "Take Action" button should go, depending on the type and the user's role
        private string? GetActionUrl(Notification n)
        {
            var isAdmin = User.IsInRole("Admin") || User.IsInRole("SuperAdmin");
            var isHost = User.IsInRole("Host");
            var isGuest = User.IsInRole("Guest");

            switch (n.Type)
            {
                case NotificationType.Booking:
                    if (isHost) return Url.Action("BookingRequests", "Host");
                    if (isGuest) return Url.Action("Index", "Bookings");
                    break;
                case NotificationType.Application:
                    if (isAdmin) return Url.Action("Applications", "Admin");
                    if (isGuest) return Url.Action("Status", "HostApplications");
                    break;
                case NotificationType.Success:
                case NotificationType.Warning:
                    if (isHost) return Url.Action("Dashboard", "Host");
                    if (isAdmin) return Url.Action("Dashboard", "Admin");
                    if (isGuest) return Url.Action("Index", "Bookings");
                    break;
            }

            return null;
        }
    }
}

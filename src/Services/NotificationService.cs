using Microsoft.AspNetCore.Identity;
using Staybnb.Data;
using Staybnb.Models;
using Staybnb.Models.Enums;

namespace Staybnb.Services
{
    public interface INotificationService
    {
        Task NotifyAsync(string userId, string title, string message, NotificationType type);
        Task NotifyAdminsAsync(string title, string message, NotificationType type);
    }

    public class NotificationService : INotificationService
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;

        public NotificationService(ApplicationDbContext db, UserManager<ApplicationUser> userManager)
        {
            _db = db;
            _userManager = userManager;
        }

        public async Task NotifyAsync(string userId, string title, string message, NotificationType type)
        {
            _db.Notifications.Add(Build(userId, title, message, type));
            await _db.SaveChangesAsync();
        }

        public async Task NotifyAdminsAsync(string title, string message, NotificationType type)
        {
            var admins = await _userManager.GetUsersInRoleAsync("Admin");
            var superAdmins = await _userManager.GetUsersInRoleAsync("SuperAdmin");

            var ids = admins.Concat(superAdmins).Select(u => u.Id).Distinct();

            foreach (var id in ids)
                _db.Notifications.Add(Build(id, title, message, type));

            await _db.SaveChangesAsync();
        }

        private static Notification Build(string userId, string title, string message, NotificationType type)
        {
            // The Title column allows 100 characters
            if (title.Length > 100)
                title = title.Substring(0, 100);

            return new Notification
            {
                UserId = userId,
                Title = title,
                Message = message,
                Type = type,
                IsRead = false,
                CreatedAt = DateTime.UtcNow
            };
        }
    }
}

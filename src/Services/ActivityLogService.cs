using Staybnb.Data;
using Staybnb.Models;
using Staybnb.Models.Enums;

namespace Staybnb.Services
{
    public interface IActivityLogService
    {
        Task LogAsync(string? userId, string action, ActivityType type);
    }

    public class ActivityLogService : IActivityLogService
    {
        private readonly ApplicationDbContext _db;

        public ActivityLogService(ApplicationDbContext db)
        {
            _db = db;
        }

        public async Task LogAsync(string? userId, string action, ActivityType type)
        {
            // The Action column allows 100 characters
            if (action.Length > 100)
                action = action.Substring(0, 100);

            _db.ActivityLogs.Add(new ActivityLog
            {
                UserId = userId,
                Action = action,
                ActivityType = type,
                CreatedAt = DateTime.UtcNow
            });

            await _db.SaveChangesAsync();
        }
    }
}

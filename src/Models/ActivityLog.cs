using System.ComponentModel.DataAnnotations;
using Staybnb.Models.Enums;

namespace Staybnb.Models
{
    public class ActivityLog
    {
        public int Id { get; set; }
        public string? UserId { get; set; }
        public ApplicationUser? User { get; set; }
        [StringLength(100)] public string Action { get; set; } = string.Empty;
        public ActivityType ActivityType { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}

using Staybnb.Models;

namespace Staybnb.ViewModels
{
    public class NotificationsViewModel
    {
        public List<Notification> Items { get; set; } = new();
        public Notification? Selected { get; set; }
        public string? SelectedActionUrl { get; set; }
        public string Filter { get; set; } = "all";
        public int TotalCount { get; set; }
        public int UnreadCount { get; set; }
        public int ReadCount { get; set; }
    }
}

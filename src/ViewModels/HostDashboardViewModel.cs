using Staybnb.Models;

namespace Staybnb.ViewModels
{
    public class HostDashboardViewModel
    {
        public string HostName { get; set; } = string.Empty;
        public int TotalProperties { get; set; }
        public int ActiveProperties { get; set; }
        public int TotalBookings { get; set; }
        public int PendingBookings { get; set; }
        public decimal TotalEarnings { get; set; }
        public double? AverageRating { get; set; }
        public int ReviewCount { get; set; }
        public List<Booking> RecentBookings { get; set; } = new();
    }
}

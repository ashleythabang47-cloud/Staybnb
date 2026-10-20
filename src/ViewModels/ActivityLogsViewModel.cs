using Staybnb.Models;

namespace Staybnb.ViewModels
{
    public class ActivityLogsViewModel
    {
        public List<ActivityLog> Logs { get; set; } = new();
        public string? Type { get; set; }
        public string? Search { get; set; }
        public int Page { get; set; } = 1;
        public int TotalPages { get; set; } = 1;
        public int TotalCount { get; set; }
    }
}

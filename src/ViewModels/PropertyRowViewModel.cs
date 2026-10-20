using Staybnb.Models;
using Staybnb.Models.Enums;

namespace Staybnb.ViewModels
{
    public class PropertyRowViewModel
    {
        public HostProperty Property { get; set; } = null!;
        public ApplicationStatus? ApprovalStatus { get; set; }
        public int BookingCount { get; set; }
    }
}

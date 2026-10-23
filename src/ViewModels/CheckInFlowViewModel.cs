using Staybnb.Models;

namespace Staybnb.ViewModels
{
    public class CheckInFlowViewModel
    {
        public Booking Booking { get; set; } = null!;
        public CheckInConfig Config { get; set; } = new();
        public GuestCheckIn? CheckIn { get; set; }
        public bool CanSubmit { get; set; }
    }
}

using Staybnb.Models;

namespace Staybnb.ViewModels
{
    public class PropertyDetailsViewModel
    {
        public HostProperty Property { get; set; } = null!;
        public double AverageRating { get; set; }
        public int ReviewCount { get; set; }
    }
}

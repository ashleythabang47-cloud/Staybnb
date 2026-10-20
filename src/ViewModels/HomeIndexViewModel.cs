using Staybnb.Models;

namespace Staybnb.ViewModels
{
    public class HomeIndexViewModel
    {
        public List<HostProperty> Properties { get; set; } = new();
        public List<string> Cities { get; set; } = new();
        public string? City { get; set; }
        public string? Type { get; set; }
    }
}

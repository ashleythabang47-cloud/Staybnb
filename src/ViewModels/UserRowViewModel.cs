using Staybnb.Models;

namespace Staybnb.ViewModels
{
    public class UserRowViewModel
    {
        public ApplicationUser User { get; set; } = null!;
        public List<string> Roles { get; set; } = new();
    }
}

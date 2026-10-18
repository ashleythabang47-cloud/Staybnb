using Staybnb.Models.Enums;

namespace Staybnb.Models
{
    public class HostApplication
    {
        public int Id { get; set; }
        public string ApplicationUserId { get; set; } = string.Empty;
        public ApplicationUser? ApplicationUser { get; set; }
        public int PropertyId { get; set; }
        public HostProperty? Property { get; set; }
        public ApplicationStatus Status { get; set; } = ApplicationStatus.Pending;
        public DateTime AppliedAt { get; set; } = DateTime.UtcNow;
    }
}
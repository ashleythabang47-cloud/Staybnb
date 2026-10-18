using System.ComponentModel.DataAnnotations;

namespace Staybnb.Models
{
    public class Review
    {
        public int Id { get; set; }
        public int PropertyId { get; set; }
        public HostProperty? Property { get; set; }
        public string ReviewerId { get; set; } = string.Empty;
        public ApplicationUser? Reviewer { get; set; }
        [Range(1, 5)] public int Rating { get; set; }
        [StringLength(1000)] public string Comment { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
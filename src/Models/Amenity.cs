using System.ComponentModel.DataAnnotations;

namespace Staybnb.Models
{
    public class Amenity
    {
        public int Id { get; set; }
        [Required, StringLength(50)] public string Name { get; set; } = string.Empty;
        [StringLength(50)] public string IconClass { get; set; } = string.Empty;
        public ICollection<HostProperty> Properties { get; set; } = new List<HostProperty>();
    }
}
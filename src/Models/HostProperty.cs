using System.ComponentModel.DataAnnotations;

namespace Staybnb.Models
{
    public class HostProperty
    {
        public int Id { get; set; }

        [Required, StringLength(100)]
        public string Title { get; set; } = string.Empty;

        [Required]
        public string Description { get; set; } = string.Empty;

        [Range(1, 1000000)]
        public decimal PricePerNight { get; set; }

        [Range(0, 1000000)]
        public decimal CleaningFee { get; set; }

        [Required]
        public string Address { get; set; } = string.Empty;

        [Required]
        public string City { get; set; } = string.Empty;

        [Required]
        public string PropertyType { get; set; } = string.Empty;

        [Range(1, 50)]
        public int MaxGuests { get; set; } = 1;

        public bool IsActive { get; set; } = false;

        public string HostId { get; set; } = string.Empty;
        public ApplicationUser? Host { get; set; }

        public ICollection<PropertyImage> Images { get; set; } = new List<PropertyImage>();
        public ICollection<Booking> Bookings { get; set; } = new List<Booking>();
        public ICollection<Review> Reviews { get; set; } = new List<Review>();
        public ICollection<Amenity> Amenities { get; set; } = new List<Amenity>();
        public CheckInProcess? CheckInProcess { get; set; }
    }
}

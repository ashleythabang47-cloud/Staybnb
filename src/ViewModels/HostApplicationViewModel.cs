using System.ComponentModel.DataAnnotations;
using Staybnb.Models;

namespace Staybnb.ViewModels
{
    public class HostApplicationViewModel
    {
        public static readonly string[] PropertyTypes =
            { "Apartment", "House", "Cabin", "Villa", "Cottage", "Guesthouse" };

        [Required, StringLength(100)]
        public string Title { get; set; } = string.Empty;

        [Required, StringLength(2000)]
        public string Description { get; set; } = string.Empty;

        [Required, Range(1, 1000000)]
        [Display(Name = "Price per night (R)")]
        public decimal PricePerNight { get; set; }

        [Range(0, 1000000)]
        [Display(Name = "Cleaning fee (R)")]
        public decimal CleaningFee { get; set; }

        [Required]
        public string Address { get; set; } = string.Empty;

        [Required]
        public string City { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Property type")]
        public string PropertyType { get; set; } = string.Empty;

        [Required, Range(1, 50)]
        [Display(Name = "Maximum guests")]
        public int MaxGuests { get; set; } = 1;

        public List<int> SelectedAmenityIds { get; set; } = new();

        // Not bound from the form: filled by the controller for display
        public List<Amenity> AvailableAmenities { get; set; } = new();
    }
}

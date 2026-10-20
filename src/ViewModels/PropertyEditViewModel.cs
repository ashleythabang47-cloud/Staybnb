using System.ComponentModel.DataAnnotations;
using Staybnb.Models;

namespace Staybnb.ViewModels
{
    public class PropertyEditViewModel
    {
        public int Id { get; set; }

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

        // Display only (not bound from the form)
        public List<Amenity> AvailableAmenities { get; set; } = new();
        public List<string> ImageUrls { get; set; } = new();
    }
}

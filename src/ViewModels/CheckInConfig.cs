using System.ComponentModel.DataAnnotations;

namespace Staybnb.ViewModels
{
    // The shape stored (as JSON) in CheckInProcess.StepsJson
    public class CheckInConfig
    {
        public List<string> Rules { get; set; } = new();
        public List<string> Documents { get; set; } = new();
    }

    // The form for a host to configure the check-in process of one property
    public class CheckInSetupViewModel
    {
        public int PropertyId { get; set; }
        public string PropertyTitle { get; set; } = string.Empty;

        [Required, StringLength(200)]
        [Display(Name = "Check-in title")]
        public string Title { get; set; } = string.Empty;

        [Display(Name = "House rules / check-in steps (one per line)")]
        public string Rules { get; set; } = string.Empty;

        [Display(Name = "Required documents (one per line)")]
        public string Documents { get; set; } = string.Empty;
    }
}

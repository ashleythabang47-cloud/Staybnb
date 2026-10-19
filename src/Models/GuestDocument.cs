using System.ComponentModel.DataAnnotations;
using Staybnb.Models.Enums;

namespace Staybnb.Models
{
    public class GuestDocument
    {
        public int Id { get; set; }
        public int GuestCheckInId { get; set; }
        public GuestCheckIn? GuestCheckIn { get; set; }
        [StringLength(100)] public string DocumentType { get; set; } = string.Empty;
        [StringLength(200)] public string FileName { get; set; } = string.Empty;
        public DocumentStatus Status { get; set; } = DocumentStatus.Pending;
    }
}

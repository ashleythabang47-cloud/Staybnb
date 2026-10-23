using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Staybnb.Data;
using Staybnb.Models;

namespace Staybnb.Controllers
{
    // Guest ID documents are private: only the guest, the property's host and admins can open them
    [Authorize]
    public class GuestDocumentsController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IWebHostEnvironment _env;

        public GuestDocumentsController(
            ApplicationDbContext db,
            UserManager<ApplicationUser> userManager,
            IWebHostEnvironment env)
        {
            _db = db;
            _userManager = userManager;
            _env = env;
        }

        public async Task<IActionResult> Download(int id)
        {
            var doc = await _db.GuestDocuments
                .Include(d => d.GuestCheckIn).ThenInclude(c => c!.Booking).ThenInclude(b => b!.Property)
                .FirstOrDefaultAsync(d => d.Id == id);

            if (doc == null)
                return NotFound();

            var booking = doc.GuestCheckIn!.Booking!;
            var userId = _userManager.GetUserId(User);

            var allowed = booking.GuestId == userId
                       || booking.Property!.HostId == userId
                       || User.IsInRole("Admin")
                       || User.IsInRole("SuperAdmin");

            if (!allowed)
                return Forbid();

            var path = Path.Combine(_env.ContentRootPath, "App_Data", "guest-documents", doc.FileName);
            if (!System.IO.File.Exists(path))
                return NotFound();

            var contentType = Path.GetExtension(doc.FileName).ToLowerInvariant() switch
            {
                ".pdf" => "application/pdf",
                ".png" => "image/png",
                _ => "image/jpeg"
            };

            return PhysicalFile(path, contentType);
        }
    }
}

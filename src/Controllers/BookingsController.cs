using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Staybnb.Data;
using Staybnb.Models;
using Staybnb.Models.Enums;
using Staybnb.Services;

namespace Staybnb.Controllers
{
    [Authorize(Roles = "Guest")]
    public class BookingsController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IActivityLogService _log;
        private readonly INotificationService _notifications;

        public BookingsController(
            ApplicationDbContext db,
            UserManager<ApplicationUser> userManager,
            IActivityLogService log,
            INotificationService notifications)
        {
            _db = db;
            _userManager = userManager;
            _log = log;
            _notifications = notifications;
        }

        // ---------- My bookings ----------

        public async Task<IActionResult> Index()
        {
            var guestId = _userManager.GetUserId(User)!;

            var bookings = await _db.Bookings
                .Include(b => b.Property).ThenInclude(p => p!.Images)
                .Include(b => b.GuestCheckIn)
                .Where(b => b.GuestId == guestId)
                .OrderByDescending(b => b.Id)
                .ToListAsync();

            return View(bookings);
        }

        // ---------- Request a booking ----------

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(int propertyId, DateTime checkIn, DateTime checkOut, int guests)
        {
            var guestId = _userManager.GetUserId(User)!;

            var property = await _db.Properties.FirstOrDefaultAsync(p => p.Id == propertyId && p.IsActive);
            if (property == null)
                return NotFound();

            checkIn = checkIn.Date;
            checkOut = checkOut.Date;

            string? error = null;

            if (checkIn < DateTime.Today)
                error = "Check-in cannot be in the past.";
            else if (checkOut <= checkIn)
                error = "Check-out must be after check-in.";
            else if (guests < 1 || guests > property.MaxGuests)
                error = $"This property allows between 1 and {property.MaxGuests} guests.";
            else
            {
                // Dates already taken by an approved booking cannot be booked again
                var overlap = await _db.Bookings.AnyAsync(b =>
                    b.PropertyId == propertyId &&
                    (b.Status == BookingStatus.Approved || b.Status == BookingStatus.CheckedIn) &&
                    b.CheckInDate < checkOut && b.CheckOutDate > checkIn);

                if (overlap)
                    error = "Those dates are no longer available.";
            }

            if (error != null)
            {
                TempData["Error"] = error;
                return RedirectToAction("Details", "Properties", new { id = propertyId });
            }

            // TotalPrice = (PricePerNight x Nights) + CleaningFee + ServiceFee
            var nights = (checkOut - checkIn).Days;
            var price = PricingCalculator.Calculate(property.PricePerNight, property.CleaningFee, nights);

            var booking = new Booking
            {
                PropertyId = propertyId,
                GuestId = guestId,
                CheckInDate = checkIn,
                CheckOutDate = checkOut,
                NumberOfGuests = guests,
                TotalPrice = price.Total,
                Status = BookingStatus.Pending,
                Payment = new Payment
                {
                    Amount = price.Total,
                    Status = PaymentStatus.Pending,
                    CreatedAt = DateTime.UtcNow
                }
            };

            _db.Bookings.Add(booking);
            await _db.SaveChangesAsync();

            await _log.LogAsync(guestId, $"Booking #{booking.Id} requested for '{property.Title}'", ActivityType.BookingStatusChange);
            await _notifications.NotifyAsync(guestId, "Booking request sent",
                $"Your request for '{property.Title}' is waiting for the host to respond.", NotificationType.Booking);
            await _notifications.NotifyAsync(property.HostId, "New booking request",
                $"You have a new booking request for '{property.Title}' ({checkIn:dd MMM} to {checkOut:dd MMM yyyy}).",
                NotificationType.Booking);

            TempData["Success"] = "Booking request sent. The host will approve or reject it.";
            return RedirectToAction(nameof(Index));
        }

        // ---------- Cancel a pending request ----------

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Cancel(int id)
        {
            var guestId = _userManager.GetUserId(User)!;

            var booking = await _db.Bookings
                .Include(b => b.Property)
                .FirstOrDefaultAsync(b => b.Id == id && b.GuestId == guestId);

            if (booking == null)
                return NotFound();

            if (booking.Status != BookingStatus.Pending)
            {
                TempData["Error"] = "Only pending requests can be cancelled.";
                return RedirectToAction(nameof(Index));
            }

            booking.Status = BookingStatus.Cancelled;
            await _db.SaveChangesAsync();

            await _log.LogAsync(guestId, $"Booking #{booking.Id} cancelled by guest", ActivityType.BookingStatusChange);
            if (booking.Property != null)
            {
                await _notifications.NotifyAsync(booking.Property.HostId, "Booking request cancelled",
                    $"A guest cancelled their request for '{booking.Property.Title}'.", NotificationType.Booking);
            }

            TempData["Success"] = "Booking request cancelled.";
            return RedirectToAction(nameof(Index));
        }
    }
}

using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Staybnb.Models;
using Staybnb.Models.Enums;
using Staybnb.Services;
using Staybnb.ViewModels;

namespace Staybnb.Data
{
    // Demo data so every dashboard has something to show on a fresh database.
    // Runs once (it does nothing if the demo host already exists) and can be switched off
    // with "SeedDemoData": false in appsettings.json.
    public static class DemoDataSeeder
    {
        public const string AdminEmail = "admin@staybnb.com";
        public const string HostEmail = "host@staybnb.com";
        public const string GuestEmail = "guest@staybnb.com";
        public const string ApplicantEmail = "applicant@staybnb.com";

        public static async Task SeedAsync(IServiceProvider services)
        {
            var db = services.GetRequiredService<ApplicationDbContext>();
            var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();

            if (await userManager.FindByEmailAsync(HostEmail) != null)
                return;

            // ---------- Users ----------
            var admin = await CreateUserAsync(userManager, AdminEmail, "Admin@12345", "Lunga", "Aphane", "Admin");
            var host = await CreateUserAsync(userManager, HostEmail, "Host@12345", "Elsie", "Masuku", "Host");
            var guest = await CreateUserAsync(userManager, GuestEmail, "Guest@12345", "Banele", "Mthembu", "Guest");
            var applicant = await CreateUserAsync(userManager, ApplicantEmail, "Guest@12345", "Trevor", "Mlotshwa", "Guest");

            var amenities = await db.Amenities.ToListAsync();
            List<Amenity> Pick(params string[] names) =>
                amenities.Where(a => names.Contains(a.Name)).ToList();

            // ---------- Approved properties (owned by the demo host) ----------
            var cabin = NewProperty(host, "Cozy Forest Cabin", "Durban", "Cabin", 3100, 250, 8,
                "Escape to our cozy forest cabin, tucked away among towering trees and birdsong. A peaceful retreat for guests seeking quiet, comfort and a deep connection with nature.",
                "12 Forest Road, Durban", true, new[] { "cabin1.jpg", "cabin2.jpg" },
                Pick("WiFi", "Kitchen", "Washer", "Dryer", "Air Conditioning", "Heating"));

            var apartment = NewProperty(host, "Lovely City Apartment", "Umhlanga", "Apartment", 1780, 200, 5,
                "A bright modern apartment close to the beach, restaurants and shops, with sea views from the balcony.",
                "45 Ocean Drive, Umhlanga", true, new[] { "apt1.jpg", "apt2.jpg" },
                Pick("WiFi", "Kitchen", "Air Conditioning", "TV", "Parking"));

            var villa = NewProperty(host, "Luxury Beachfront Villa", "Cape Town", "Villa", 2100, 350, 6,
                "Wake up to the sound of the waves in this beachfront villa with a private pool and open-plan living.",
                "8 Beach Road, Cape Town", true, new[] { "villa1.jpg", "villa2.jpg" },
                Pick("WiFi", "Kitchen", "Swimming Pool", "Parking", "TV"));

            // ---------- A pending application for the admin to review ----------
            var guesthouse = NewProperty(applicant, "Bushveld Guesthouse", "Nelspruit", "Guesthouse", 1250, 150, 4,
                "A quiet guesthouse on the edge of the bushveld, ideal for a weekend away.",
                "21 Acacia Street, Nelspruit", false, new[] { "guesthouse1.jpg" },
                Pick("WiFi", "Parking"));

            db.Properties.AddRange(cabin, apartment, villa, guesthouse);
            await db.SaveChangesAsync();

            db.HostApplications.AddRange(
                new HostApplication { ApplicationUserId = host.Id, PropertyId = cabin.Id, Status = ApplicationStatus.Approved, AppliedAt = DateTime.UtcNow.AddDays(-60) },
                new HostApplication { ApplicationUserId = host.Id, PropertyId = apartment.Id, Status = ApplicationStatus.Approved, AppliedAt = DateTime.UtcNow.AddDays(-45) },
                new HostApplication { ApplicationUserId = host.Id, PropertyId = villa.Id, Status = ApplicationStatus.Approved, AppliedAt = DateTime.UtcNow.AddDays(-30) },
                new HostApplication { ApplicationUserId = applicant.Id, PropertyId = guesthouse.Id, Status = ApplicationStatus.Pending, AppliedAt = DateTime.UtcNow.AddDays(-1) }
            );

            // ---------- Check-in rules and required documents ----------
            foreach (var p in new[] { cabin, apartment, villa })
            {
                db.CheckInProcesses.Add(new CheckInProcess
                {
                    PropertyId = p.Id,
                    Title = $"Check-in for {p.Title}",
                    StepsJson = JsonSerializer.Serialize(new CheckInConfig
                    {
                        Rules = new List<string> { "No smoking indoors", "Quiet hours start at 22:00", "Check-out by 10:00" },
                        Documents = new List<string> { "ID or Passport" }
                    }),
                    CreatedAt = DateTime.UtcNow
                });
            }

            // ---------- Bookings (all three statuses a marker will want to see) ----------
            var today = DateTime.Today;

            // Past stay: completed and paid
            var past = NewBooking(cabin, guest, today.AddDays(-20), today.AddDays(-18), 2, BookingStatus.Completed, PaymentStatus.Paid);
            // Upcoming and approved: the guest can try the check-in flow
            var approved = NewBooking(apartment, guest, today.AddDays(10), today.AddDays(12), 2, BookingStatus.Approved, PaymentStatus.Paid);
            // Waiting for the host: the host can try approve / reject
            var pending = NewBooking(villa, guest, today.AddDays(30), today.AddDays(33), 4, BookingStatus.Pending, PaymentStatus.Pending);

            db.Bookings.AddRange(past, approved, pending);

            // ---------- Reviews ----------
            db.Reviews.AddRange(
                new Review { PropertyId = cabin.Id, ReviewerId = guest.Id, Rating = 5, Comment = "Peaceful, clean and exactly as described. We will be back.", CreatedAt = DateTime.UtcNow.AddDays(-17) },
                new Review { PropertyId = cabin.Id, ReviewerId = applicant.Id, Rating = 4, Comment = "Lovely cabin and a very responsive host.", CreatedAt = DateTime.UtcNow.AddDays(-40) }
            );

            // ---------- Messages ----------
            db.Messages.AddRange(
                new Message { SenderId = guest.Id, ReceiverId = host.Id, Content = "Hi Elsie, is early check-in possible for the apartment?", Timestamp = DateTime.UtcNow.AddHours(-5), IsRead = false },
                new Message { SenderId = host.Id, ReceiverId = guest.Id, Content = "Hi Banele, welcome! Early check-in from 12:00 is fine, just let me know your arrival time.", Timestamp = DateTime.UtcNow.AddHours(-3), IsRead = false }
            );

            // ---------- Notifications ----------
            db.Notifications.AddRange(
                new Notification { UserId = host.Id, Title = "New booking request", Message = "You have a new booking request for 'Luxury Beachfront Villa'.", Type = NotificationType.Booking, CreatedAt = DateTime.UtcNow.AddHours(-6) },
                new Notification { UserId = guest.Id, Title = "Booking Approved", Message = "Your booking for 'Lovely City Apartment' was approved. You can now start your check-in.", Type = NotificationType.Success, CreatedAt = DateTime.UtcNow.AddHours(-8) },
                new Notification { UserId = admin.Id, Title = "New host application", Message = "A new host application for 'Bushveld Guesthouse' needs review.", Type = NotificationType.Application, CreatedAt = DateTime.UtcNow.AddHours(-24) }
            );

            // ---------- Activity log ----------
            db.ActivityLogs.AddRange(
                new ActivityLog { UserId = host.Id, Action = "Host application submitted: Cozy Forest Cabin", ActivityType = ActivityType.ApplicationSubmitted, CreatedAt = DateTime.UtcNow.AddDays(-60) },
                new ActivityLog { UserId = host.Id, Action = "Role changed: Guest to Host", ActivityType = ActivityType.RoleChange, CreatedAt = DateTime.UtcNow.AddDays(-59) },
                new ActivityLog { UserId = guest.Id, Action = "Booking requested for 'Luxury Beachfront Villa'", ActivityType = ActivityType.BookingStatusChange, CreatedAt = DateTime.UtcNow.AddHours(-6) },
                new ActivityLog { UserId = applicant.Id, Action = "Host application submitted: Bushveld Guesthouse", ActivityType = ActivityType.ApplicationSubmitted, CreatedAt = DateTime.UtcNow.AddDays(-1) }
            );

            await db.SaveChangesAsync();
        }

        private static async Task<ApplicationUser> CreateUserAsync(
            UserManager<ApplicationUser> userManager, string email, string password,
            string firstName, string lastName, string role)
        {
            var user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                FirstName = firstName,
                LastName = lastName,
                CreatedAt = DateTime.UtcNow.AddDays(-70)
            };

            var result = await userManager.CreateAsync(user, password);
            if (!result.Succeeded)
                throw new InvalidOperationException(
                    $"Could not create demo user {email}: {string.Join(", ", result.Errors.Select(e => e.Description))}");

            await userManager.AddToRoleAsync(user, role);
            return user;
        }

        private static HostProperty NewProperty(
            ApplicationUser owner, string title, string city, string type, decimal price, decimal cleaning,
            int maxGuests, string description, string address, bool active, string[] images, List<Amenity> amenities)
        {
            var property = new HostProperty
            {
                Title = title,
                City = city,
                PropertyType = type,
                PricePerNight = price,
                CleaningFee = cleaning,
                MaxGuests = maxGuests,
                Description = description,
                Address = address,
                IsActive = active,
                HostId = owner.Id,
                Amenities = amenities
            };

            foreach (var image in images)
                property.Images.Add(new PropertyImage { ImageUrl = "/images/demo/" + image });

            return property;
        }

        private static Booking NewBooking(
            HostProperty property, ApplicationUser guest, DateTime checkIn, DateTime checkOut,
            int guests, BookingStatus status, PaymentStatus paymentStatus)
        {
            var nights = (checkOut - checkIn).Days;
            var price = PricingCalculator.Calculate(property.PricePerNight, property.CleaningFee, nights);

            return new Booking
            {
                Property = property,
                GuestId = guest.Id,
                CheckInDate = checkIn,
                CheckOutDate = checkOut,
                NumberOfGuests = guests,
                TotalPrice = price.Total,
                Status = status,
                Payment = new Payment
                {
                    Amount = price.Total,
                    Status = paymentStatus,
                    CreatedAt = DateTime.UtcNow
                }
            };
        }
    }
}

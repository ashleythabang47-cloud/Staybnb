using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Staybnb.Models;

namespace Staybnb.Data
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<HostProperty> Properties { get; set; }
        public DbSet<PropertyImage> PropertyImages { get; set; }
        public DbSet<Amenity> Amenities { get; set; }
        public DbSet<Booking> Bookings { get; set; }
        public DbSet<Payment> Payments { get; set; }
        public DbSet<Review> Reviews { get; set; }
        public DbSet<WishlistItem> WishlistItems { get; set; }
        public DbSet<Message> Messages { get; set; }
        public DbSet<ActivityLog> ActivityLogs { get; set; }
        public DbSet<Notification> Notifications { get; set; }
        public DbSet<HostApplication> HostApplications { get; set; }
        public DbSet<CheckInProcess> CheckInProcesses { get; set; }
        public DbSet<GuestCheckIn> GuestCheckIns { get; set; }
        public DbSet<GuestDocument> GuestDocuments { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // Money columns
            builder.Entity<HostProperty>().Property(p => p.PricePerNight).HasPrecision(18, 2);
            builder.Entity<HostProperty>().Property(p => p.CleaningFee).HasPrecision(18, 2);
            builder.Entity<Booking>().Property(b => b.TotalPrice).HasPrecision(18, 2);
            builder.Entity<Payment>().Property(p => p.Amount).HasPrecision(18, 2);

            // One-to-many (Restrict avoids SQL Server "multiple cascade paths" errors)
            builder.Entity<HostProperty>()
                .HasOne(p => p.Host).WithMany(u => u.Properties)
                .HasForeignKey(p => p.HostId).OnDelete(DeleteBehavior.Restrict);

            builder.Entity<Booking>()
                .HasOne(b => b.Guest).WithMany(u => u.Bookings)
                .HasForeignKey(b => b.GuestId).OnDelete(DeleteBehavior.Restrict);

            builder.Entity<Booking>()
                .HasOne(b => b.Property).WithMany(p => p.Bookings)
                .HasForeignKey(b => b.PropertyId).OnDelete(DeleteBehavior.Restrict);

            builder.Entity<Review>()
                .HasOne(r => r.Reviewer).WithMany(u => u.Reviews)
                .HasForeignKey(r => r.ReviewerId).OnDelete(DeleteBehavior.Restrict);

            builder.Entity<Review>()
                .HasOne(r => r.Property).WithMany(p => p.Reviews)
                .HasForeignKey(r => r.PropertyId).OnDelete(DeleteBehavior.Cascade);

            // One-to-one
            builder.Entity<Payment>()
                .HasOne(p => p.Booking).WithOne(b => b.Payment)
                .HasForeignKey<Payment>(p => p.BookingId);

            builder.Entity<GuestCheckIn>()
                .HasOne(g => g.Booking).WithOne(b => b.GuestCheckIn)
                .HasForeignKey<GuestCheckIn>(g => g.BookingId);

            builder.Entity<CheckInProcess>()
                .HasOne(c => c.Property).WithOne(p => p.CheckInProcess)
                .HasForeignKey<CheckInProcess>(c => c.PropertyId);

            builder.Entity<GuestCheckIn>()
                .HasOne(g => g.CheckInProcess).WithMany()
                .HasForeignKey(g => g.CheckInProcessId).OnDelete(DeleteBehavior.Restrict);

            // Message has two foreign keys to the same table
            builder.Entity<Message>()
                .HasOne(m => m.Sender).WithMany()
                .HasForeignKey(m => m.SenderId).OnDelete(DeleteBehavior.Restrict);

            builder.Entity<Message>()
                .HasOne(m => m.Receiver).WithMany()
                .HasForeignKey(m => m.ReceiverId).OnDelete(DeleteBehavior.Restrict);

            builder.Entity<HostApplication>()
                .HasOne(h => h.ApplicationUser).WithMany()
                .HasForeignKey(h => h.ApplicationUserId).OnDelete(DeleteBehavior.Restrict);
        }
    }
}

using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Staybnb.Models;

namespace Staybnb.Data
{
    public static class RoleSeeder
    {
        public static readonly string[] Roles = { "SuperAdmin", "Admin", "Host", "Guest" };

        public const string SuperAdminEmail = "superadmin@staybnb.com";
        public const string SuperAdminPassword = "Admin@12345";

        public static async Task SeedAsync(IServiceProvider services)
        {
            var db = services.GetRequiredService<ApplicationDbContext>();
            var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
            var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();

            // Create the database / apply migrations if they have not been applied yet
            await db.Database.MigrateAsync();

            // 1. Roles
            foreach (var role in Roles)
            {
                if (!await roleManager.RoleExistsAsync(role))
                {
                    var result = await roleManager.CreateAsync(new IdentityRole(role));
                    if (!result.Succeeded)
                        throw new InvalidOperationException(
                            $"Could not create role {role}: {string.Join(", ", result.Errors.Select(e => e.Description))}");
                }
            }

            // 2. SuperAdmin user
            var superAdmin = await userManager.FindByEmailAsync(SuperAdminEmail);
            if (superAdmin == null)
            {
                superAdmin = new ApplicationUser
                {
                    UserName = SuperAdminEmail,
                    Email = SuperAdminEmail,
                    EmailConfirmed = true,
                    FirstName = "Super",
                    LastName = "Admin",
                    CreatedAt = DateTime.UtcNow
                };

                var created = await userManager.CreateAsync(superAdmin, SuperAdminPassword);
                if (!created.Succeeded)
                    throw new InvalidOperationException(
                        $"Could not create SuperAdmin: {string.Join(", ", created.Errors.Select(e => e.Description))}");
            }

            if (!await userManager.IsInRoleAsync(superAdmin, "SuperAdmin"))
                await userManager.AddToRoleAsync(superAdmin, "SuperAdmin");

            // 3. Amenities (used later by property listings)
            if (!await db.Amenities.AnyAsync())
            {
                db.Amenities.AddRange(
                    new Amenity { Name = "WiFi", IconClass = "bi-wifi" },
                    new Amenity { Name = "Kitchen", IconClass = "bi-egg-fried" },
                    new Amenity { Name = "Washer", IconClass = "bi-droplet" },
                    new Amenity { Name = "Dryer", IconClass = "bi-wind" },
                    new Amenity { Name = "Air Conditioning", IconClass = "bi-snow" },
                    new Amenity { Name = "Heating", IconClass = "bi-thermometer-sun" },
                    new Amenity { Name = "Parking", IconClass = "bi-p-square" },
                    new Amenity { Name = "Swimming Pool", IconClass = "bi-water" },
                    new Amenity { Name = "TV", IconClass = "bi-tv" }
                );
                await db.SaveChangesAsync();
            }
        }
    }
}

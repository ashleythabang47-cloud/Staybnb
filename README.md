# Staybnb: Airbnb 

Staybnb is a managed property rental platform built with **ASP.NET Core MVC (.NET 8)**, **Entity Framework Core** and **SQL Server**. It models the full user lifecycle: a user registers as a **Guest**, applies to become a **Host**, and **Administrators** review applications and keep the marketplace secure.

## Requirements

- .NET 8 SDK
- SQL Server or SQL Server LocalDB (installed with Visual Studio)

## How to run

1. Open a terminal in the `src` folder (the one containing `Staybnb.csproj`).
2. Check the connection string in `src/appsettings.json` (default: LocalDB, database `StaybnbDb`).
3. Run:

```
dotnet run --launch-profile https
```

On first start the application **creates the database from the EF Core migrations and seeds it automatically** (roles, SuperAdmin, amenities and demo data), so no manual SQL script is needed.

To start again from a clean database:

```
dotnet ef database drop --force
dotnet run --launch-profile https
```

## Demo accounts

| Role | Email | Password |
|---|---|---|
| SuperAdmin | superadmin@staybnb.com | Admin@12345 |
| Admin | admin@staybnb.com | Admin@12345 |
| Host | host@staybnb.com | Host@12345 |
| Guest | guest@staybnb.com | Guest@12345 |
| Guest (has a pending host application) | applicant@staybnb.com | Guest@12345 |

These are demo accounts created by the seeders (`Data/RoleSeeder.cs` and `Data/DemoDataSeeder.cs`). Demo data can be switched off with `"SeedDemoData": false` in `appsettings.json`.

## Features by role

**Guest**: register, browse active listings with city and type filters, view property details, live price quote, request bookings, cancel pending requests, complete a 3-step check-in (house rules, document upload, review and submit), message hosts, apply to become a Host.

**Host**: dashboard with statistics, manage properties (edit, activate/deactivate), configure the check-in rules and required documents per property, approve or reject booking requests, verify or reject guest check-in documents.

**Admin**: dashboard, review host applications (approval swaps the Guest role for Host), view the activity log.

**SuperAdmin**: everything an Admin can do, plus promote Guests to Admin (and remove the Admin role).

**Everyone logged in**: notification center with unread bell counter, messaging inbox with read/unread status.

## Key rules

- New registrants are always **Guests**. The built-in Identity register page is disabled, so every registration goes through `AccountController`.
- On approval of a host application the user **gains Host and loses Guest** (`AdminController.Approve`).
- A host application cannot be submitted without at least one property image (validated on the server).
- `TotalPrice = (PricePerNight x Nights) + CleaningFee + ServiceFee` (service fee 12%), in `Services/PricingCalculator.cs`.
- Booking status flow: Pending, Approved, CheckedIn (plus Rejected, Cancelled, Completed).
- Guest ID documents are stored **outside `wwwroot`** (`App_Data/guest-documents`) and served only to the guest, the property's host and admins.
- Role protection uses `[Authorize(Roles = "...")]` on controllers and actions, and every host/guest action also checks ownership of the record.
- Important actions (logins, role changes, application decisions, booking status changes, check-in events, messages) are written to the `ActivityLogs` table.

## Project structure (src)

```
Controllers/     Account, Admin, Host, HostApplications, Properties, Bookings,
                 CheckIn, GuestDocuments, Messages, Notifications, Home
Data/            ApplicationDbContext, RoleSeeder, DemoDataSeeder, Migrations
Models/          Entities and Enums
Services/        ActivityLogService, NotificationService, PricingCalculator
ViewModels/      Form and page models
ViewComponents/  Navbar notification and message counters
Views/           Razor views per controller
```

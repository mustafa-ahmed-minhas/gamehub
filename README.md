# GameHub

A web-based management system for a sports arena, covering court booking, membership
management, CRM, sales, invoicing, payments and reporting — with separate public,
customer and administrative experiences.

---

## Project Overview

GameHub is a server-rendered ASP.NET Core MVC application that manages the full
operational lifecycle of a sports arena:

- **Public site** — marketing home page, court catalogue, how-it-works, inquiries
- **Online booking** — court selection, live slot availability, checkout and payment
- **Customer portal** — account profile, booking history, memberships, invoices,
  payments and notifications
- **Admin back office** — dashboard, arena setup (sports, facilities, courts, pricing,
  schedules), customers, bookings, memberships, payments, invoices, sales
  quotations, sales orders, CRM (leads, opportunities, activity, follow-ups), reports
  and system settings

## Tech Stack

| Layer | Technology |
|---|---|
| Framework | ASP.NET Core MVC on **.NET 10** (`net10.0`) |
| Views | Razor (`.cshtml`), 6 layouts |
| ORM | Entity Framework Core **10.0.10** (`UseSqlServer`) |
| Database | SQL Server / SQL Server Express LocalDB |
| Authentication | ASP.NET Core Cookie Authentication, dual scheme (admin + customer) |
| Session | Distributed Memory Cache + Session |
| Frontend | Hand-written CSS and JavaScript in `wwwroot` (no build step) |
| Third-party UI | Bootstrap 5.3.3, Bootstrap Icons, Chart.js 4.4.9, FullCalendar 6, jQuery Validation (loaded from CDN) |

## Prerequisites

1. **.NET 10 SDK** (10.0.301 or later)
   Verify with `dotnet --list-sdks`
2. **SQL Server Express LocalDB** (Windows only) — used by the default connection string
   Verify with `sqllocaldb info`
3. **Git**
4. Internet access — NuGet restore, the local `dotnet-ef` tool restore, and CDN assets
   (Bootstrap, Chart.js, FullCalendar, Google Fonts) are fetched at runtime

> The default connection string is
> `Server=(localdb)\MSSQLLocalDB;Database=GameHubFYPDb;Trusted_Connection=True;...`
> To use a different database, override it without editing any tracked file:
> `set ConnectionStrings__DefaultConnection=<your connection string>`

## Setup

```bash
git clone <repository-url> GameHub
cd GameHub
dotnet restore
dotnet tool restore
```

`dotnet tool restore` installs the repository-pinned `dotnet-ef` tool declared in
`.config/dotnet-tools.json`. No global tool installation is required.

## Database Migrations

Apply the EF Core migrations to create/update the `GameHubFYPDb` database:

```bash
dotnet ef database update
```

## Run

```bash
dotnet run --launch-profile https
```

Then open **https://localhost:7001** (or `http://localhost:5113`).

On first run you may need to trust the local HTTPS development certificate:

```bash
dotnet dev-certs https --trust
```

### Development environment requirement

**`ASPNETCORE_ENVIRONMENT` must be `Development`.** The demo data seeder
(`Data/DatabaseSeeder.cs`) only executes when the environment is `Development`
(see `Program.cs`). Running with `ASPNETCORE_ENVIRONMENT=Production` leaves the
database without users, and the application cannot be logged into.

The `Properties/launchSettings.json` profiles (`http` and `https`) both set this
value, so `dotnet run` with either launch profile is already correct.

## Default Demo Login

| Role | Email | Password |
|---|---|---|
| Super Admin | `admin@gamehub.pk` | `Admin@123` |

> Change this password immediately before any real deployment.

---

## Repository Structure

```
GameHub.sln                  Visual Studio solution
GameHub.csproj               Project file
Program.cs                   Application host and middleware pipeline
.config/dotnet-tools.json     Local tool manifest (dotnet-ef)
Controllers/                 HTTP endpoints, grouped by module
Models/
  Entities/                  EF Core entity classes
  Enums/                     Domain enumerations
  ViewModels/                View and form models
  Checkout/                  Booking and membership checkout sessions
Views/                       Razor views and shared layouts
Services/
  Interfaces/                Service contracts
  Implementations/           Service implementations
Filters/                     Authorization and validation attributes
Helpers/                     Password, token, enum and UI helpers
Data/                        DbContext and demo data seeder
Migrations/                  EF Core migrations and model snapshot
wwwroot/
  css/  js/                  Static stylesheets and scripts
  images/                    Image asset folders
  uploads/                   Customer-uploaded files (runtime only, not in Git)
```

## Notes for the Client

- Customer-uploaded files are written to `wwwroot/uploads/` at runtime and are
  **not** tracked in Git. The folder structure is preserved with `.gitkeep` files
  so the paths exist after cloning.
- Build output, logs, IDE settings, screenshots and internal documentation are
  excluded from the repository via `.gitignore`.
- Line endings are normalised to LF via `.gitattributes`.

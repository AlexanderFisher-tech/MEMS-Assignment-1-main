# Civic Guide — MEMS

COMP4976 Assignment 1. ASP.NET Core MVC on .NET 10, EF Core Code First, SQLite, Identity, and local Bootstrap assets. Implements the municipal election directory, scoped administration, and anonymous printable voting guides. Seed candidates are fictional.

> **COMP4977 Web API (Part 1):** the JWT-secured JSON API for the iOS app lives in `src/Mems.Api` (tests in `tests/Mems.Api.Tests`). Run it with `dotnet run --project src/Mems.Api --launch-profile http` (Swagger at http://localhost:5080/swagger) and deploy it with `./scripts/deploy-api-azure.sh <app-name>`. See [PART1_DOCUMENTATION.md](PART1_DOCUMENTATION.md) for the architecture, ERD, endpoint contracts, and Azure steps.

## Run locally (PowerShell, from this folder)

```powershell
./scripts/setup.ps1
dotnet run --project src/Mems.Web --launch-profile http
```

Open http://localhost:5069. The setup script creates the ignored `src/Mems.Web/appsettings.Development.json` with an absolute SQLite connection string, restores dependencies, builds, and applies migrations. Startup applies pending migrations and seeds data idempotently. To configure manually, copy `appsettings.Development.example.json` to `appsettings.Development.json`, create `App_Data`, and set an absolute database path before running EF commands.

Development-only accounts: `super` (Super Admin) and `pm` (Pitt Meadows Admin); password for both is the assignment's `P@$$w0rd`. These credentials are never displayed on the public site. Do not expose Development mode to the internet. Production does not create these accounts unless explicitly configured with a unique password. Never upload the local database, settings, or key directory.

```powershell
dotnet restore MEMS.sln
dotnet build MEMS.sln --no-restore
dotnet test MEMS.sln --no-restore
dotnet ef migrations list --project src/Mems.Web
dotnet publish src/Mems.Web -c Release -o artifacts/publish
```

Publishing here creates a local folder only. It does not deploy to Azure. Run `dotnet tool restore` before EF commands on a new checkout. The .NET 10 SDK and NuGet network access are required. Package versions are pinned; SkiaSharp provides raster decoding/re-encoding, with native Windows, macOS, and Linux packages.

## Features and access

- Public: municipality/position browsing; candidate photos, biographies, name search, municipality/position filters, and pagination.
- Samples: 24 unique fictional names, 80-150 word biographies, and locally stored synthetic portraits. Existing original samples upgrade once without resetting the database; later administrator edits/uploads and deleted samples are preserved. See [portrait provenance and upgrade policy](docs/demo-portraits.md).
- Guide: anonymous session choices, seat limits, removal/clear, and print styles. Browser Print can save to PDF. Session choices expire after eight idle hours and reset on app restart. Changes to seats and deleted candidates are reflected when reopening the guide.
- Admin: scoped dashboard and full position/candidate CRUD; Super Admin also manages municipalities and logos. Reads and mutations enforce municipality ownership. New municipalities receive linked Identity administrator groups. A position with candidates cannot move to another municipality. Delete/reassign dependent records before deleting their parents; administrators with municipality memberships prevent municipality deletion.
- Validation: MVC annotations and local unobtrusive validation scripts; parent ID checks, safe HTTP(S) URLs, anti-forgery protection, Identity lockout and login throttling. Uploads accept decoded JPEG/PNG up to 2 MB / 16 megapixels, reject mismatched content, and re-encode as PNG under random names outside `wwwroot`.

Super Admin role membership grants system-wide access. Municipality admins need the `Municipality Admin` role plus a municipality-specific role whose `MunicipalityId` is set. The seed creates `Pitt Meadows Admin` for `pm`. Other groups are seeded for other municipalities. Account self-registration and a role-assignment UI are outside the mandatory feature set; add users/memberships through a trusted Identity maintenance process, never through public input.

## Validation and submission

The xUnit integration suite uses an isolated SQLite database and real MVC requests, authentication cookies, anti-forgery tokens, multipart uploads, and session cookies. See [tests](tests/Mems.Tests/ApplicationTests.cs), [rubric checklist](docs/checklist.md), and [required submission README](docs/readme.txt).

Optional browser checks: `npm install --prefix .tools/browser @playwright/test`, then with the app running, `node scripts/browser-check.cjs`. The script uses installed Edge or Chrome (override `MEMS_BROWSER_PATH`) and writes screenshots and a print PDF under ignored `artifacts/browser`. Browser checks modify only their own temporary candidate record.

Run `node scripts/print-check.cjs` to verify all 24 updated profiles and generate multi-page Letter/A4 guides with 20 valid selections in a separate anonymous session. No database records are changed by this print check. Guides show full biographies on screen and compact portraits/identity details on paper; use the browser's paper-size selector and turn off browser headers/footers for the cleanest PDF.

Azure deployment preparation is in [deploy/README.md](deploy/README.md). No cloud resources have been created, and no deployment or GitHub push is authorized yet. Complete student names/contributions and live URL placeholders before submitting. The separate report is deliberately omitted as requested.

# AGENTS.md - COMP4977 Web API Backend

## Project Overview
This repository holds the ASP.NET Core Web API backend for the COMP4977 iOS app (Municipal Elections Companion), Assignment 2.
- **Tech Stack:** .NET 10, ASP.NET Core Web API, Entity Framework Core, SQLite, ASP.NET Core Identity, JWT Bearer Authentication.
- **Architecture:** Stateless JSON REST API serving camelCase DTOs with absolute image URLs. The iOS app stores candidate selections in Firebase Firestore itself; the API has no Firebase code.
- **Projects:** `src/Mems.Api` (the API) and `tests/Mems.Api.Tests`. `src/Mems.Web` and `tests/Mems.Tests` are the partner team's MVC site and are not part of the mobile backend.

## Core Rules for Code Changes
1. **Never break JWT or API endpoints:** All `/api/...` routes must remain fully functional.
2. **Municipality scoping:** Election-data lists (`/api/candidates`, `/api/candidates/search`, `/api/positions`, `/api/offices`, `/api/elections`) require `?city=` or `?municipalityId=` and must never return another municipality's records. `/api/municipalities` stays unfiltered.
3. **Zero-Config Deployment:** Database migrations and seeding must happen automatically on application startup (`Program.cs`) so Azure deployment requires no manual CLI commands.
4. **No Circular References:** Ensure Entity Framework models use DTOs to prevent JSON serialization cycles.
5. **Authentication:** Public browsing endpoints (`/api/municipalities`, `/api/elections`, `/api/positions`, `/api/offices`, `/api/candidates`) must remain unauthenticated. All `/api/savedcandidates` endpoints require `[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]`.

## Common Commands
- Build: `dotnet build`
- Run Tests: `dotnet test`
- Run Locally: `dotnet run --project src/Mems.Api --launch-profile http` (Swagger at http://localhost:5080/swagger)

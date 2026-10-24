# AGENTS.md - COMP4977 Web API Backend

## Project Overview
This repository is the ASP.NET Core Web API backend companion for the COMP4977 iOS app (Municipal Elections Companion). 
- **Tech Stack:** .NET 10, ASP.NET Core Web API, Entity Framework Core, SQLite, ASP.NET Core Identity, JWT Bearer Authentication.
- **Architecture:** Stateless JSON REST API serving camelCase DTOs with absolute image URLs.

## Core Rules for Code Changes
1. **Never break JWT or API endpoints:** All `/api/...` routes must remain fully functional.
2. **Zero-Config Deployment:** Database migrations and seeding must happen automatically on application startup (`Program.cs`) so Azure deployment requires no manual CLI commands.
3. **No Circular References:** Ensure Entity Framework models use DTOs to prevent JSON serialization cycles.
4. **Authentication:** Public browsing endpoints (`/api/municipalities`, `/api/positions`, `/api/candidates`) must remain unauthenticated. All `/api/savedcandidates` endpoints require `[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]`.

## Common Commands
- Build: `dotnet build`
- Run Tests: `dotnet test`
- Run Locally: `dotnet run --project src/Mems.Api` (or matching project folder path)

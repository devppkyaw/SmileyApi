# CLAUDE.md

Instructions for Claude when working on this codebase.

## Stack
- ASP.NET Core Minimal API (C#)
- Entity Framework Core with SQL Server
- BackgroundService for XML sync worker

## Project Structure
- `src/SmileyApi.Api/` — API endpoints, middleware, worker, static landing page
- `src/SmileyApi.Core/` — domain models and interfaces (no EF dependency)
- `src/SmileyApi.Infrastructure/` — EF Core DbContext, migrations, repositories

## Common Commands
```bash
# Run the API locally
dotnet run --project src/SmileyApi.Api

# Add a new EF migration
dotnet ef migrations add <Name> --project src/SmileyApi.Infrastructure --startup-project src/SmileyApi.Api

# Apply migrations
dotnet ef database update --project src/SmileyApi.Infrastructure --startup-project src/SmileyApi.Api

# Run tests
dotnet test
```

## Key Rules
- **API keys:** always store as SHA-256 hash — never plaintext, never logged
- **Worker:** `XmlSyncWorker` is a `BackgroundService` inside `SmileyApi.Api/Workers/`. Do not move it to a separate project yet.
- **XML source URL:** `https://www.foedevarestyrelsen.dk/Media/638212360788086849/Smiley_xml.xml`
- **DbContext in worker:** always resolve via `IServiceScopeFactory` — never inject `SmileyDbContext` directly into the worker
- **Bulk inserts:** never insert XML sync rows one-by-one with EF. Use `EFCore.BulkExtensions` or raw `MERGE` SQL
- **Natural key:** use `navnelbnr` (Fødevarestyrelsen's ID) for upsert diffing — not CVR, which can be missing
- **Geo (MVP):** lat/lng stored as `float` columns, Haversine in raw SQL. Do not add `geography` column yet.
- **Error responses:** always return `{ "error": { "code": "...", "message": "..." } }` — no other shape
- **No frontend framework:** landing page is plain HTML/CSS in `wwwroot/` — do not introduce React or similar

## What Is Post-MVP (Do Not Build Yet)
- Webhooks and Hangfire
- Postal-code neighbourhood scores
- Reliability trend scoring
- CVR enrichment
- Stripe billing
- Splitting the worker into a separate project

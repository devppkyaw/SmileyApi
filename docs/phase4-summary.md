# Phase 4 Summary — Landing Page

**Date:** 2026-05-16  
**Branch:** main  

---

## What Was Built

Phase 4 adds the public-facing landing page at the root URL (`/`) and a `POST /v1/leads` endpoint that captures access requests into the database. The page is a single static HTML file served directly by ASP.NET Core's static file middleware — no frontend framework, no build step, no separate hosting.

---

## Files Changed

### New Files

| File | Purpose |
|---|---|
| `src/SmileyApi.Api/wwwroot/index.html` | Full landing page (replaces 13-line placeholder) |
| `src/SmileyApi.Api/wwwroot/style.css` | Dark developer theme with CSS custom properties and responsive grid |
| `src/SmileyApi.Api/Endpoints/LeadsEndpoint.cs` | `POST /v1/leads` — validates and saves access requests to DB |
| `src/SmileyApi.Core/Models/AccessRequest.cs` | `AccessRequest` entity model |
| `src/SmileyApi.Infrastructure/Migrations/20260516xxxxxx_AddAccessRequests.cs` | Migration: `AccessRequests` table |

### Modified Files

| File | Change |
|---|---|
| `src/SmileyApi.Infrastructure/Data/SmileyDbContext.cs` | Added `DbSet<AccessRequest>` and column config (max lengths) |
| `src/SmileyApi.Api/Endpoints/` | Added `LeadsEndpoint.cs` registration |
| `src/SmileyApi.Api/Middleware/ApiKeyMiddleware.cs` | Added `/v1/leads` to `PublicPaths` so it bypasses auth |
| `src/SmileyApi.Api/Program.cs` | Added `UseExceptionHandler`, `UseDefaultFiles()`, guarded `AddApplicationInsightsTelemetry()`, wired `MapLeadsEndpoint()` |

---

## Architecture

```
HTTP GET /
  │
  ├── UseExceptionHandler — wraps entire pipeline; logs + returns { error } on any unhandled exception
  ├── UseDefaultFiles     — rewrites "/" → "/index.html"
  ├── UseStaticFiles      — serves wwwroot/index.html, style.css
  └── (pipeline short-circuits — never reaches middleware)

HTTP POST /v1/leads
  │
  ├── UseExceptionHandler — wraps entire pipeline
  ├── UseDefaultFiles     — no match (not a directory)
  ├── UseStaticFiles      — no match (not a file)
  ├── ApiKeyMiddleware    — skips: "/v1/leads" is in PublicPaths
  ├── RateLimiter         — skips: not in "api-key-tier" group
  └── LeadsEndpoint
        ├── Validates Name, Email, UseCase (required)
        ├── Writes AccessRequest row to DB
        └── Returns 200 { message } or 400 { error }
```

---

## Landing Page Sections

| Section | Description |
|---|---|
| Hero | Headline, subheadline, "Request API Access" CTA scrolling to the form |
| Problem | Two sentences — raw XML, no API — sets up the value proposition |
| Feature grid | 6 cards (3-col, responsive). Daily fresh data, CVR/name/geo lookup, and full history are live. Webhooks, postal-code scores, and reliability trend are marked "Coming soon". |
| Endpoints reference | Table of all 4 live endpoints with method badge, path, auth requirement, and description |
| Example responses | Syntax-highlighted JSON for `GET /v1/establishments/{cvr}` and `GET /v1/establishments/{cvr}/history`. Field names match the actual `EstablishmentDto` and `InspectionDto` records. |
| Pricing | Free (100 req/day) vs Pro (10,000 req/day, "Coming Soon"). Free CTA links to the access form. |
| Request Access | Form (Name, Email, Company, Use Case). Submits via `fetch` to `POST /v1/leads`. Inline `<script>` — no JS file. |
| Footer | Fødevarestyrelsen data credit + contact email |

**Tech:** Plain HTML5 + CSS. [Prism.js](https://prismjs.com/) via CDN (Tomorrow Night theme) for JSON syntax highlighting. `scroll-behavior: smooth` on `html` handles CTA anchor scroll — no JS needed for that.

---

## Key Decisions

**`POST /v1/leads` backend endpoint, not a `mailto:` link.**  
A `mailto:` link fails silently on corporate machines and mobile devices where no email client is configured. A backend endpoint captures every submission into `AccessRequests` regardless of the visitor's mail setup, giving a queryable list of leads to provision keys from. It adds only one model, one endpoint, and one migration — the same EF Core + Minimal API pattern already established in Phase 2.

**Separate `style.css` instead of inline styles.**  
The CSS file is ~280 lines and the HTML is ~200 lines — keeping them separate makes both readable. The browser caches the stylesheet independently. There is no build step, so the separation costs nothing.

**Direct `SmileyDbContext` injection in `LeadsEndpoint`, not `IServiceScopeFactory`.**  
`IServiceScopeFactory` is needed in `XmlSyncWorker` because it is a singleton `BackgroundService`. Minimal API handlers are scoped per request, so `DbContext` can be injected directly — exactly the same way a controller would use it.

**`UseDefaultFiles()` must precede `UseStaticFiles()`.**  
`UseStaticFiles()` serves files by exact path. A request for `/` has no exact match in `wwwroot/`, so it passes through to `ApiKeyMiddleware` and returns 401. `UseDefaultFiles()` rewrites `/` to `/index.html` before `UseStaticFiles()` runs, so the static file middleware can serve it and short-circuit the pipeline.

**`AddApplicationInsightsTelemetry()` guarded by connection string presence.**  
The call crashed on startup locally because Application Insights requires a connection string that only exists in the Azure environment. The guard (`if (!string.IsNullOrEmpty(aiConnectionString))`) mirrors the existing Key Vault guard pattern — the telemetry service registers only when its configuration is present, and local development continues to work without Azure credentials.

**Global exception handler at the top of the pipeline.**  
Without it, any unhandled exception (e.g. a DB error) returned a bare 500 with no body and nothing logged. `app.UseExceptionHandler` placed immediately after `builder.Build()` wraps the entire pipeline. It logs the full exception via `ILogger<Program>` (console in dev, Application Insights in prod) and returns `{"error":{"code":"internal_error","message":"An unexpected error occurred."}}` — consistent with the project's standard error shape. No per-endpoint try/catch needed.

**Three post-MVP features advertised as "Coming soon" on the landing page.**  
Webhooks, postal-code scores, and reliability trend scoring are explicitly excluded from the MVP per `CLAUDE.md`. Marking them "Coming soon" on the landing page is honest and still communicates the product roadmap to early visitors.

---

## Database Changes

| Change | Detail |
|---|---|
| `AccessRequests` table | New table — stores name, email, company (nullable), use case, submitted timestamp |
| `Name`, `Email`, `Company` | `nvarchar(256)` |
| `UseCase` | `nvarchar(2000)` |

---

## Bug Fixes (discovered during phase)

**`UseDefaultFiles()` missing (root URL returned 401).**  
The placeholder `index.html` was never tested at `/` — it was only reachable at `/index.html`. Adding `app.UseDefaultFiles()` before `app.UseStaticFiles()` in `Program.cs` fixed the root URL.

**`AddApplicationInsightsTelemetry()` crashed on local startup.**  
The unconditional call threw `InvalidOperationException: A connection string was not found` when running locally without Azure configuration. Fixed by guarding the call on the presence of `ApplicationInsights:ConnectionString` or `APPLICATIONINSIGHTS_CONNECTION_STRING`.

**`POST /v1/leads` returned 500 — `AccessRequests` table missing.**  
The `AddAccessRequests` EF migration was generated but `dotnet ef database update` was never run locally. The `AccessRequests` table did not exist, causing `SqlException: Invalid object name 'AccessRequests'` on every form submission. Fixed by running `dotnet ef database update --project src\SmileyApi.Infrastructure --startup-project src\SmileyApi.Api`. In non-dev environments this is a non-issue because `Program.cs` calls `Database.MigrateAsync()` on startup.

---

## Milestone Status

> **Phase 4 milestone: Public-facing landing page live at `/`, access requests captured in database** ✅

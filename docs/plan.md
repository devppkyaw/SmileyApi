# Smiley API — Project Planning Document

> **Status:** Pre-build planning  
> **Stack:** ASP.NET Core / C# · Entity Framework Core · SQL Server  
> **Hosting:** Azure (cheapest tier)  
> **Goal:** A modern, developer-friendly REST API on top of Fødevarestyrelsen's open Smiley Scheme data

---

## Table of Contents

1. [What We Are Building](#1-what-we-are-building)
2. [MVP Scope](#2-mvp-scope)
3. [Development Phases](#3-development-phases)
4. [Project & Folder Structure](#4-project--folder-structure)
5. [Database Schema](#5-database-schema)
6. [API Endpoints](#6-api-endpoints)
7. [Example API Responses](#7-example-api-responses)
8. [Daily Sync Worker](#8-daily-sync-worker)
9. [Authentication & API Keys](#9-authentication--api-keys)
10. [Landing Page Plan](#10-landing-page-plan)
11. [Hosting Plan (Azure)](#11-hosting-plan-azure)
12. [Risks & Mitigations](#12-risks--mitigations)
13. [Open Decisions](#13-open-decisions)

---

## 1. What We Are Building

### The Problem
Fødevarestyrelsen publishes a ~32 MB XML file daily with inspection results for ~50,000 food establishments across Denmark. The data is free and open, but raw — there is no modern API other developers can integrate with. Existing consumer apps (iOS app, Chrome extension) parse it for end users but expose no API surface.

### The Solution
A REST API that sits on top of that data and adds:

- Clean, predictable endpoints (by CVR, name, address, GPS)
- Full inspection history per establishment (not just the last 3)
- Geo/postal-code search powered by PostGIS-equivalent queries in SQL Server
- Webhooks: get notified when a smiley score changes
- Neighbourhood/postal-code aggregated scores
- Reliability trend scoring per establishment

### Who Pays For It (Target Customers)
| Segment | Why They Need It |
|---|---|
| Food delivery platforms (Just Eat, Wolt) | Legally required to display smiley scores in Denmark |
| Restaurant booking platforms (The Fork) | Trust signal for consumers |
| Review platforms (TripAdvisor) | Enrichment data |
| Journalists & researchers | Food safety analytics |
| Developers | Building food-related apps in Denmark |

### Competitive Gap
No modern, well-documented, developer-facing API with history, webhooks, and geo queries exists today. The closest competitor (an old 2015 middleware wrapper) is likely dead.

---

## 2. MVP Scope

The MVP must be shippable in days, not weeks. Everything else is Post-MVP.

### MVP Includes
- [ ] Daily XML sync worker (download → parse → diff → store)
- [ ] 4 core GET endpoints (by CVR, by name search, nearby, history)
- [ ] API key authentication (simple — generate a key, validate on request)
- [ ] SQL Server database with `establishments` and `inspections` tables
- [ ] Rate limiting per API key
- [ ] Basic interactive docs at `/docs` (Scalar or Swagger UI)
- [ ] One-page landing site with endpoint examples and a "Request Access" link

### Post-MVP (Explicitly Excluded from MVP)
- Webhooks / Hangfire delivery
- Postal-code neighbourhood scores
- Reliability trend scoring
- CVR enrichment from company registry
- Stripe billing
- Bulk area queries

---

## 3. Development Phases

### Phase 0 — Setup (Day 1, ~2 hours)
- Create solution and project structure (see Section 4)
- Set up SQL Server locally (LocalDB for dev, Azure SQL for prod)
- Configure EF Core + migrations
- Confirm you can connect to the Fødevarestyrelsen XML endpoint

**Milestone:** Empty database, running API returning 200 OK on `/health`

---

### Phase 1 — Data Ingestion (Day 1–2, ~4 hours)
- Build `XmlSyncWorker` (a .NET Background Service)
- Parse the XML into `Establishment` and `Inspection` EF entities
- Implement diff logic: insert new, update changed, append new inspection rows
- Run locally, verify ~50,000 rows land in your DB

**Milestone:** Full DB populated from one XML sync run

---

### Phase 2 — Core API Endpoints (Day 2–3, ~6 hours)
- Implement 4 MVP endpoints (see Section 6)
- Add API key middleware (simple header validation)
- Add rate limiting (ASP.NET Core built-in rate limiting, .NET 7+)
- Wire up Scalar/Swagger docs

**Milestone:** All 4 endpoints return real data with API key auth

---

### Phase 3 — Deploy to Azure (Day 3–4, ~3 hours)
- Provision Azure SQL (Basic tier, ~$5/month)
- Provision Azure App Service (Free F1 tier to start, or B1 ~$13/month if needed)
- Set up GitHub Actions CI/CD pipeline (deploy on push to `main`)
- Run first sync against production DB

**Milestone:** Live URL, real data, reachable from outside your machine

---

### Phase 4 — Landing Page (Day 4–5, ~3 hours)
- Single HTML page (no frontend framework needed)
- Sections: hero, features, endpoints, example responses, pricing teaser, request access form
- Host on same Azure App Service (just serve static files from `/wwwroot`)

**Milestone:** Public-facing page explaining the product

---

### Phase 5 — Webhooks (Post-MVP, Week 2+)
- Add `webhook_subscriptions` table
- Add Hangfire or a simple hosted service queue
- On each sync diff, fire callbacks for changed scores with retry logic

---

### Phase 6 — Monetisation (Post-MVP, when you have users)
- Decide on model (freemium tiers vs. per-request)
- Integrate Stripe for API key purchase/management
- Add usage tracking table per API key

---

## 4. Project & Folder Structure

```
SmileyApi/                          ← Solution root
│
├── SmileyApi.sln
│
├── src/
│   ├── SmileyApi.Api/              ← ASP.NET Core Minimal API (the web project)
│   │   ├── Endpoints/
│   │   │   ├── EstablishmentEndpoints.cs
│   │   │   └── WebhookEndpoints.cs
│   │   ├── Middleware/
│   │   │   └── ApiKeyMiddleware.cs
│   │   ├── Workers/                ← BackgroundService lives here (split later)
│   │   │   ├── XmlSyncWorker.cs
│   │   │   └── FodevareXmlParser.cs
│   │   ├── wwwroot/                ← Landing page static files
│   │   │   ├── index.html
│   │   │   ├── style.css
│   │   │   └── script.js
│   │   ├── Program.cs
│   │   └── appsettings.json
│   │
│   ├── SmileyApi.Core/             ← Domain models, interfaces (no EF dependency)
│   │   ├── Models/
│   │   │   ├── Establishment.cs
│   │   │   ├── Inspection.cs
│   │   │   ├── WebhookSubscription.cs
│   │   │   └── ApiKey.cs
│   │   └── Interfaces/
│   │       ├── IEstablishmentRepository.cs
│   │       └── IApiKeyService.cs
│   │
│   └── SmileyApi.Infrastructure/   ← EF Core, DB, repository implementations
│       ├── Data/
│       │   ├── SmileyDbContext.cs
│       │   └── Migrations/
│       ├── Repositories/
│       │   └── EstablishmentRepository.cs
│       └── Services/
│           └── ApiKeyService.cs
│
├── tests/
│   └── SmileyApi.Api.Tests/
│
└── .github/
    └── workflows/
        └── deploy.yml
```

> **When you're ready to split the worker out later**, it's a clean move: create `SmileyApi.Worker/`, copy `Workers/` folder into it, add a `Program.cs`, and reference `SmileyApi.Infrastructure`. The `BackgroundService` code doesn't need to change.

**Why this split?**
- `Core` has zero external dependencies — safe to unit test without a DB
- `Infrastructure` owns all EF + SQL Server concerns
- `Worker` can be deployed as a separate Azure WebJob or run inside the same App Service (simpler for MVP)
- `Api` stays thin — just HTTP routing and middleware

---

## 5. Database Schema

### `api_keys`
| Column | Type | Notes |
|---|---|---|
| `id` | int PK | |
| `key_hash` | nvarchar(64) | SHA-256 of the actual key — never store plaintext |
| `owner_email` | nvarchar(256) | |
| `tier` | nvarchar(32) | `free`, `pro` |
| `requests_today` | int | Reset nightly |
| `created_at` | datetime2 | |
| `is_active` | bit | |

### `establishments`
| Column | Type | Notes |
|---|---|---|
| `id` | int PK | |
| `navnelbnr` | int UNIQUE | Fødevarestyrelsen's own ID — use as natural key for diffs |
| `cvr_number` | nvarchar(20) | |
| `name` | nvarchar(512) | |
| `address` | nvarchar(512) | |
| `postal_code` | nvarchar(10) | |
| `city` | nvarchar(256) | |
| `industry_code` | nvarchar(32) | |
| `industry_name` | nvarchar(256) | |
| `geo_lat` | float | For geo queries — use SQL Server `geography` type in v2 |
| `geo_lng` | float | |
| `report_url` | nvarchar(1024) | |
| `latest_score` | int | Denormalised for fast filtering (1–4) |
| `first_seen_at` | datetime2 | |
| `updated_at` | datetime2 | |

> **Note:** SQL Server does support spatial types (`geography`). For MVP, simple `lat/lng float` columns + Haversine formula in a raw SQL query is enough. Add a proper `geography` column in v2.

### `inspections`
| Column | Type | Notes |
|---|---|---|
| `id` | int PK | |
| `establishment_id` | int FK → establishments | |
| `smiley_score` | int | 1=happy, 2=medium, 3=sad, 4=very sad |
| `inspected_on` | date | Date of the actual inspection |
| `recorded_at` | datetime2 | When we first saw this in the XML |

### `webhook_subscriptions` (Post-MVP)
| Column | Type | Notes |
|---|---|---|
| `id` | int PK | |
| `api_key_id` | int FK → api_keys | |
| `establishment_id` | int FK → establishments | |
| `callback_url` | nvarchar(1024) | |
| `created_at` | datetime2 | |

---

## 6. API Endpoints

### MVP Endpoints

```
GET  /v1/establishments/{cvr}
GET  /v1/establishments/search?q={query}&page={n}&limit={n}
GET  /v1/establishments/nearby?lat={lat}&lng={lng}&radius={km}
GET  /v1/establishments/{cvr}/history
GET  /health
```

### Post-MVP Endpoints

```
GET  /v1/areas/{postalCode}/score
POST /v1/webhooks
DELETE /v1/webhooks/{id}
```

### Auth Header (all endpoints)
```
X-Api-Key: your_api_key_here
```

### Rate Limits (MVP suggestion)
| Tier | Limit |
|---|---|
| Free | 100 requests/day |
| Pro | 10,000 requests/day |

---

## 7. Example API Responses

### `GET /v1/establishments/12345678`

```json
{
  "navnelbnr": 1001234,
  "cvr": "12345678",
  "name": "Café Nørrebro",
  "address": "Nørrebrogade 45",
  "postalCode": "2200",
  "city": "København N",
  "industryCode": "56101",
  "industryName": "Restauranter",
  "latestScore": 1,
  "latestScoreLabel": "Glad",
  "geo": {
    "lat": 55.6867,
    "lng": 12.5545
  },
  "reportUrl": "https://www.findsmiley.dk/...",
  "lastInspectedOn": "2024-11-03"
}
```

### `GET /v1/establishments/12345678/history`

```json
{
  "cvr": "12345678",
  "name": "Café Nørrebro",
  "inspections": [
    { "score": 1, "scoreLabel": "Glad", "inspectedOn": "2024-11-03" },
    { "score": 2, "scoreLabel": "Ej glad", "inspectedOn": "2024-03-15" },
    { "score": 1, "scoreLabel": "Glad", "inspectedOn": "2023-08-22" }
  ]
}
```

### `GET /v1/establishments/search?q=pizza+københavn`

```json
{
  "total": 48,
  "page": 1,
  "limit": 20,
  "results": [
    {
      "cvr": "87654321",
      "name": "Pizza Express København",
      "address": "Strøget 12, 1000 København K",
      "latestScore": 1,
      "latestScoreLabel": "Glad"
    }
  ]
}
```

### `GET /v1/establishments/nearby?lat=55.6761&lng=12.5683&radius=1`

```json
{
  "queryLat": 55.6761,
  "queryLng": 12.5683,
  "radiusKm": 1,
  "total": 34,
  "results": [
    {
      "cvr": "11223344",
      "name": "Sushi Bar Vesterbro",
      "address": "Vesterbrogade 3",
      "latestScore": 1,
      "distanceKm": 0.23
    }
  ]
}
```

### Error Response (all endpoints, consistent shape)

```json
{
  "error": {
    "code": "ESTABLISHMENT_NOT_FOUND",
    "message": "No establishment found for CVR 99999999"
  }
}
```

---

## 8. Daily Sync Worker

### How It Works

```
[Azure Timer Trigger / Background Service]
  ↓
Download XML from Fødevarestyrelsen (HTTPS GET, ~32MB)
  ↓
Parse XML → List<EstablishmentXmlRow>
  ↓
For each row:
  - Does navnelbnr exist in DB?
    NO  → Insert new establishment + first inspection row
    YES → Compare fields
          - Any field changed? → Update establishment
          - New smiley score?  → Insert new inspection row
                               → [Post-MVP] Enqueue webhook delivery
  ↓
Log: X new, Y updated, Z unchanged
```

### XML Source
- URL: `https://www.foedevarestyrelsen.dk/Media/638212360788086849/Smiley_xml.xml`
- The ZIP contains a single XML file
- Fields map 1:1 to the `establishments` schema above

### Worker Design Choice — **DECIDED: Inside API process**
The sync worker runs as a `BackgroundService` registered inside `SmileyApi.Api`. One deploy unit, one App Service. Split to a separate project post-MVP when needed.

**Implementation pattern:**
```csharp
// SmileyApi.Api/Workers/XmlSyncWorker.cs
public class XmlSyncWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<XmlSyncWorker> _logger;
    private static readonly SemaphoreSlim _lock = new(1, 1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Run once at startup, then every 24 hours
        while (!stoppingToken.IsCancellationRequested)
        {
            if (await _lock.WaitAsync(0)) // non-blocking — skip if already running
            {
                try { await RunSyncAsync(stoppingToken); }
                finally { _lock.Release(); }
            }
            await Task.Delay(TimeSpan.FromHours(24), stoppingToken);
        }
    }
}

// Program.cs — one line to register it
builder.Services.AddHostedService<XmlSyncWorker>();
```

**Why `IServiceScopeFactory` instead of injecting DbContext directly:**  
`BackgroundService` is a singleton. `DbContext` is scoped. You must create a scope inside the worker to resolve EF correctly:
```csharp
using var scope = _scopeFactory.CreateScope();
var db = scope.ServiceProvider.GetRequiredService<SmileyDbContext>();
```

### Performance Expectation
- Parsing 50,000 XML rows: ~2–5 seconds
- DB upsert with EF Core batch: ~30–60 seconds on Basic Azure SQL
- Total sync window: under 2 minutes

---

## 9. Authentication & API Keys

### How Keys Are Generated (MVP)
1. Developer sends email to you (or fills "Request Access" form on landing page)
2. You run a simple admin endpoint or console command: `POST /admin/api-keys` → returns a generated key
3. You email the key to the developer
4. Key is stored as `SHA-256(key)` in the `api_keys` table — never the plaintext key

### How Keys Are Validated (Per Request)
```
Request arrives with header: X-Api-Key: abc123...
  ↓
Middleware hashes the value: SHA-256("abc123...")
  ↓
Looks up hash in api_keys table
  ↓
Key found + is_active = true?  → Continue, attach tier to HttpContext
Key not found / inactive?       → 401 Unauthorized
Rate limit exceeded?            → 429 Too Many Requests
```

### Post-MVP: Self-Serve Keys
Add a simple sign-up flow + Stripe payment that auto-generates and emails the key. For MVP, manual provisioning is fine.

---

## 10. Landing Page Plan

The landing page lives at the root URL (`/`) and is served as a static `index.html` from `/wwwroot`. No framework needed — plain HTML + CSS.

### Sections

**1. Hero**
- Headline: "The Smiley Scheme API — built for developers"
- Sub: "Clean REST endpoints for Denmark's official food inspection data. History, geo search, and webhooks."
- CTA button: "Request API Access" → scrolls to contact form

**2. The Problem (2 sentences)**
- Fødevarestyrelsen publishes the data as raw XML. There's no API.

**3. Feature Grid (3 columns)**
- 🔍 Lookup by CVR, name, or GPS
- 📜 Full inspection history
- 📬 Webhooks on score changes
- 🗺️ Postal-code area scores
- 📊 Reliability trend scoring
- ⚡ Daily fresh data

**4. Endpoints Reference**
- Table with method, path, description
- Rendered in a code-block style box

**5. Example Responses**
- Tabbed or stacked code blocks showing JSON for each endpoint
- Syntax-highlighted with a lightweight library (Prism.js via CDN)

**6. Pricing (teaser)**
- "Free tier available. Paid plans coming soon."
- Simple 2-column card: Free vs. Pro (with placeholder limits)

**7. Request Access**
- Simple form: Name, Email, Company, Use Case
- On submit: sends a mailto: link or a POST to a lightweight endpoint that stores the lead in DB
- No complex form service needed for MVP

**8. Footer**
- Data source credit: Fødevarestyrelsen
- GitHub link (optional)
- Contact email

### Tech for Landing Page
- Plain HTML5 + CSS (no React, no Vue)
- [Prism.js](https://prismjs.com/) via CDN for code highlighting
- Served directly by ASP.NET Core's static file middleware — zero extra cost

---

## 11. Hosting Plan (Azure)

### Recommended Azure Resources (MVP)

| Resource | Tier | Est. Monthly Cost |
|---|---|---|
| Azure App Service (API + landing page) | B1 (~1 core, 1.75 GB RAM) | ~€13/month |
| Azure SQL Database | Basic (5 DTU, 2 GB) | ~€4/month |
| **Total** | | **~€17/month** |

> The Free (F1) App Service tier has no "always on" and the app sleeps after 20 min idle. Use B1 from the start if the API needs to be reliable. If budget is the blocker, start on F1 and upgrade when you have users.

### Deployment Strategy
- GitHub Actions workflow: on push to `main` → build → publish → deploy to Azure App Service
- Connection strings stored as Azure App Service environment variables (not in code)
- The Worker runs as a hosted service inside the same App Service process for MVP

### What You Do NOT Need to Know
- No Docker required for MVP
- No Kubernetes, no load balancers
- Azure App Service abstracts all of that — you deploy a zip/publish folder

---

## 12. Risks & Mitigations

| Risk | Likelihood | Impact | Mitigation |
|---|---|---|---|
| **XML format changes** — Fødevarestyrelsen changes column names or structure | Medium | High | Write a schema-validation step at the top of the sync. Alert (log + email) if fields are missing; don't crash the whole sync. |
| **CVR is not always present** — some establishments lack a CVR number | High | Medium | Use `navnelbnr` (Fødevarestyrelsen's own ID) as the primary natural key. CVR is optional/nullable. |
| **50,000-row EF upsert is slow** | Medium | Low | Use `ExecuteSqlRaw` with `MERGE` statements or EF Extensions (`EFCore.BulkExtensions`) for the sync. Avoid inserting row-by-row. |
| **Duplicate inspection rows** | Medium | Medium | Add a UNIQUE constraint on `(establishment_id, inspected_on)` — database-enforced deduplication. |
| **Geo search without PostGIS** | Low | Low | SQL Server has native `geography` type. For MVP, Haversine in SQL is sufficient. |
| **Legal / terms of use** | Low | High | Data is under Danish open data license. Must cite Fødevarestyrelsen as source. Do NOT claim the data is yours. Include attribution in API responses or docs. |
| **Azure F1 sleep** | High (on free tier) | Medium | Upgrade to B1, or use an uptime pinger service as a temporary workaround. |
| **XML download fails or is slow** | Low | Low | Wrap download in retry logic (3 attempts, exponential backoff). If all fail, skip sync and log. Do not delete existing data. |
| **API key in plain text if DB is breached** | Low | High | Hash all keys with SHA-256 before storage. Never log the raw key. |

---

## 13. Open Decisions

These are deliberately left open — decide before or during Phase 3:

| Decision | Options | Recommendation |
|---|---|---|
| **Monetisation model** | Freemium, per-request, flat subscription | Start with freemium (free tier + one Pro tier). Add Stripe later. |
| **Worker hosting** | ~~Inside API process vs. separate Azure WebJob~~ | ✅ **DECIDED:** Inside API as `BackgroundService`. Split post-MVP. |
| **CVR enrichment** | Call CVR API live vs. cache CVR data | Post-MVP. CVR API has rate limits — cache to a DB table. |
| **Geo column type** | Float lat/lng + Haversine vs. SQL Server `geography` | Float + Haversine for MVP. Migrate to `geography` post-MVP. |
| **XML source URL** | Verify exact current URL before building | Confirm at: https://www.findsmiley.dk/Statistik |
| **Webhook delivery** | Hangfire vs. hosted queue vs. Azure Service Bus | Hangfire on SQL Server for post-MVP (no extra Azure service needed). |
| **API versioning** | URL path (`/v1/`) vs. header | URL path — simplest for consumers. |

---

*Last updated: pre-build planning stage*  
*Data source: Fødevarestyrelsen — citeret i henhold til dansk åben datallicens*

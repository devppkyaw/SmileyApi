# Phase 2 Summary — Core API Endpoints, Auth & Rate Limiting

**Date:** 2026-05-16  
**Branch:** main  

---

## What Was Built

Phase 2 implements the full public-facing API layer: four REST endpoints, API key authentication via `X-Api-Key` header (SHA-256 hashed), per-tier rate limiting (Free: 100 req/day, Pro: 10,000 req/day), and a temporary admin endpoint for local key generation.

---

## Files Changed

### New Files

| File | Purpose |
|---|---|
| `docs/phase2-summary.md` | This document |

### Modified Files

| File | Change |
|---|---|
| `src/SmileyApi.Core/Models/ApiKey.cs` | Added `LastResetAt` property for daily counter reset tracking |
| `src/SmileyApi.Infrastructure/Services/ApiKeyService.cs` | Fully implemented — SHA-256 hashing, daily counter reset, cryptographic key generation |
| `src/SmileyApi.Infrastructure/Repositories/EstablishmentRepository.cs` | Fully implemented — all 4 query methods (CVR lookup, LIKE search, Haversine geo query, inspection history) |
| `src/SmileyApi.Api/Middleware/ApiKeyMiddleware.cs` | Fully implemented — header extraction, scoped service resolution, 401 responses |
| `src/SmileyApi.Api/Endpoints/EstablishmentEndpoints.cs` | Fully implemented — 4 endpoints + DTOs + `/admin/keys` dev endpoint |
| `src/SmileyApi.Api/Program.cs` | Added `AddRateLimiter`, `UseMiddleware<ApiKeyMiddleware>`, `UseRateLimiter` |
| `src/SmileyApi.Infrastructure/Migrations/20260516103024_AddApiKeyLastResetAt.cs` | Migration: `LastResetAt` column + `IX_ApiKeys_KeyHash`, `IX_Establishments_GeoLat`, `IX_Establishments_GeoLng` indexes |

---

## Architecture

```
HTTP Request
  │
  ├── UseStaticFiles         — wwwroot assets bypass auth
  ├── ApiKeyMiddleware       — validates X-Api-Key header
  │     └── IServiceScopeFactory → ApiKeyService (scoped)
  │           └── SHA-256 hash → DB lookup → RequestsToday++
  ├── RateLimiter            — FixedWindow per API key ID
  │     └── Free: 100/day  |  Pro: 10,000/day
  │
  └── Endpoints
        ├── POST /admin/keys               — dev: generate a new key
        ├── GET  /v1/establishments/{cvr}  — single establishment + latest inspection
        ├── GET  /v1/establishments/search — LIKE search, paginated
        ├── GET  /v1/establishments/nearby — Haversine geo query (raw SQL, TOP 50)
        └── GET  /v1/establishments/{cvr}/history — full inspection history
```

---

## Key Decisions

**`ApiKeyMiddleware` resolves `ApiKeyService` via `IServiceScopeFactory`.**  
`ApiKeyMiddleware` is a singleton pipeline component; `ApiKeyService` is scoped (needs a `DbContext` per request). Injecting the scoped service directly into the middleware constructor would throw at startup. The same `IServiceScopeFactory` pattern used by `XmlSyncWorker` in Phase 1 was applied here.

**`LastResetAt` added to `ApiKey` to enable real daily resets.**  
The original `ApiKey` model had a `RequestsToday` counter but no date to compare against, so the counter would have grown unbounded. `LastResetAt` is checked in `ValidateAsync` — if its date is before today UTC, the counter resets before incrementing. The rate limiter enforces the hard limit; `RequestsToday` provides the auditable usage metric.

**Rate limiter reads `Items["ApiKey"]` set by middleware.**  
The ASP.NET Core rate limiter's `AddPolicy` factory lambda runs per request. Because `ApiKeyMiddleware` runs first in the pipeline and attaches the validated `ApiKey` to `HttpContext.Items`, the rate limiter can partition by `ApiKey.Id` and apply the correct tier limit without a second DB lookup.

**Haversine formula in raw SQL with bounding box pre-filter.**  
EF Core has no spatial awareness for `float` lat/lng columns. The `/nearby` query uses `FromSqlRaw` with a parameterized T-SQL Haversine formula. A bounding-box `WHERE` clause (`GeoLat BETWEEN`, `GeoLng BETWEEN`) acts as a cheap pre-filter, allowing the new `GeoLat`/`GeoLng` indexes to eliminate most rows before the full Haversine calculation runs.

**LIKE search escapes SQL wildcard characters in user input.**  
`EF.Functions.Like` is safe from injection via EF parameterization, but `%`, `_`, and `[` in the user's query string are meaningful LIKE wildcards. The search pattern is sanitized by replacing them with their escaped equivalents (`[%]`, `[_]`, `[[]`) before building the pattern string.

**`/admin/keys` endpoint is unauthenticated by design (local dev only).**  
There is no key-generation endpoint in the 4 MVP endpoints. A temporary `POST /admin/keys` endpoint was added to allow creating keys during local development via Scalar UI. The `ApiKeyMiddleware` explicitly skips `/admin/*` paths. This endpoint must be removed or protected before deploying to a public environment.

---

## Endpoints

| Method | Path | Auth | Description |
|---|---|---|---|
| `POST` | `/admin/keys` | None | Generate a new API key (dev only) |
| `GET` | `/v1/establishments/{cvr}` | Required | Establishment by CVR + latest inspection |
| `GET` | `/v1/establishments/search?q=&page=&limit=` | Required | Name/address/city search, paginated (max 100) |
| `GET` | `/v1/establishments/nearby?lat=&lng=&radius=` | Required | Geo search by Haversine, radius 0–50 km, top 50 results |
| `GET` | `/v1/establishments/{cvr}/history` | Required | Full inspection history ordered by date desc |

**Error response shape (all errors):**
```json
{ "error": { "code": "not_found", "message": "..." } }
```

---

## Database Changes

| Change | Detail |
|---|---|
| `ApiKeys.LastResetAt` | `datetime2 NOT NULL DEFAULT GETUTCDATE()` |
| `IX_ApiKeys_KeyHash` | Non-clustered index — speeds up per-request key lookup |
| `IX_Establishments_GeoLat` | Non-clustered index — enables bounding box pre-filter for `/nearby` |
| `IX_Establishments_GeoLng` | Non-clustered index — enables bounding box pre-filter for `/nearby` |

---

## Milestone Status

> **Phase 2 milestone: All 4 API endpoints live, auth and rate limiting enforced** ✅

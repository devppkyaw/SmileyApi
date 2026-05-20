# Smiley API — Business Tier Development Plan
**Based on:** `docs/Revised_Tier_20_05.md` vs current codebase (Phase 5 complete)
**Date:** 2026-05-20

---

## Context

The revised product decision (Revised_Tier_20_05.md) changes the tier architecture in two critical ways:

1. **Navnelbnr replaces CVR as the widget lookup key.** One CVR maps to 100+ locations; Navnelbnr identifies a single physical location. The current `WidgetEndpoints.cs` still looks up by `CvrNumber` — this is wrong and must be fixed first.
2. **2 tiers instead of 3.** Old plan: Open / Registered / Paid. New plan: **Free** (score badge + history sparkline, no registration required for embed) and **Pro** (everything + multi-location dashboard, email alerts, webhooks, advanced analytics, developer API). Custom widget styling is removed entirely (Fødevarestyrelsen regulatory requirement).

Revenue focus is B2B API contracts with restaurant chains and food platforms — not per-restaurant widget subscriptions. This shifts priority toward a polished developer API and multi-location management.

---

## Key Divergences: Revised Plan vs Current Code

| Area | Current Code | Revised Plan | Action |
|---|---|---|---|
| Widget lookup key | `?cvr=` → `CvrNumber` | `?navnelbnr=` → `Navnelbnr` | Fix widget endpoint + embed code |
| Tier count | 3-tier model planned | 2-tier (Free / Pro) | Simplify Business.Tier enum |
| History sparkline | Registered tier only | **Free tier** (no registration) | Move to open widget endpoint |
| Custom widget styles | Planned (paid) | **Removed** (regulatory) | Do not implement |
| BusinessCvrs lookup | `CvrNumber` planned | **Navnelbnr** | Use Navnelbnr in new table |
| Webhooks | Developer API key only | Pro tier business feature | Extend to Business auth |
| Score-change alerts | Webhooks only | **Pro only** — email alerts + webhooks | ACS email, Pro-gated |

---

## Tier Feature Matrix

| Feature | Free | Pro |
|---|---|---|
| Score badge + official report link | Yes | Yes |
| Score history sparkline | Yes | Yes |
| Multiple locations | — | Yes (CVR bulk onboard) |
| Score-change email alerts | — | Yes |
| Webhooks | — | Yes |
| Advanced analytics | — | Yes |
| Developer API | — | Yes |
| Setup | One line of code (no account) | Account required |

---

## Phase A — Navnelbnr Widget Fix (critical, do first)
**Risk: breaking change to embed API. No live customers yet, right time to fix.**

### Tasks
1. **`WidgetEndpoints.cs`** — change `?cvr=` param to `?navnelbnr=` and query `e.Navnelbnr == navnelbnr.Trim()` instead of `e.CvrNumber`.
2. Update error messages (`"missing_cvr"` → `"missing_navnelbnr"`, `"No establishment found for this CVR"` → `"No establishment found for this Navnelbnr"`).
3. **`widget.js`** — switch from `?cvr=` query param on script src to `data-navnelbnr` attribute; update fetch URL to use `?navnelbnr=`.
4. **`index.html`** — update embed code demo, update tier table to 2-tier model (Free/Pro), remove widget styles row.

### Files
- `src/SmileyApi.Api/Endpoints/WidgetEndpoints.cs`
- `src/SmileyApi.Api/wwwroot/widget.js`
- `src/SmileyApi.Api/wwwroot/index.html`

New embed code format:
```html
<script src="https://api.smiley.dk/widget.js" data-navnelbnr="1234567"></script>
```

---

## Phase B — History Sparkline in Free Widget
**Revised plan: history sparkline is now a free-tier feature, no registration required.**

### Tasks
1. Extend the anonymous widget endpoint (Phase A) to also return the last 5 inspection scores + dates.
2. Update the response shape: add `history: [{score, date}]` array.
3. Update `widget.js` to render a simple inline sparkline (5 data points, CSS-only or minimal SVG — no external charting library).

### Files
- `src/SmileyApi.Api/Endpoints/WidgetEndpoints.cs`
- `src/SmileyApi.Api/wwwroot/widget.js`

---

## Phase C — Database: Business + BusinessNavnelbnrs Tables

### New Models (`SmileyApi.Core/Models/`)
**`Business.cs`**
```
Id (int PK), BusinessId (nvarchar(32) unique — public non-secret, e.g. biz_abc123),
Email (nvarchar(256)), CompanyName (nvarchar(256)), Tier (nvarchar(16): "free"|"pro"),
IsEmailVerified (bit), MagicLinkToken (nvarchar(64)?), MagicLinkTokenExpiry (datetime2?),
StripeCustomerId (nvarchar(64)?), StripeSubscriptionId (nvarchar(64)?),
CreatedAt (datetime2), VerifiedAt (datetime2?)
```

**`BusinessNavnelbnr.cs`** — use Navnelbnr (not CvrNumber) per revised plan
```
Id (int PK), BusinessId (int FK → Businesses.Id), Navnelbnr (nvarchar(20)), AddedAt (datetime2)
```

### Tasks
1. Create both model files in `SmileyApi.Core/Models/`.
2. Add `DbSet<Business>` and `DbSet<BusinessNavnelbnr>` to `SmileyDbContext.cs`.
3. Add EF migration (migration name: `AddBusinessTables`).
4. Index: `BusinessNavnelbnr.Navnelbnr`.

### Files
- `src/SmileyApi.Core/Models/Business.cs` (new)
- `src/SmileyApi.Core/Models/BusinessNavnelbnr.cs` (new)
- `src/SmileyApi.Infrastructure/Data/SmileyDbContext.cs`
- `src/SmileyApi.Infrastructure/Data/Migrations/` (new migration)

---

## Phase D — Business Registration & Auth Endpoints

### New file: `BusinessEndpoints.cs`

| Endpoint | Description |
|---|---|
| `POST /v1/business/register` | Email + company name → create unverified Business, send verification email |
| `GET /v1/business/verify?token=` | Set `IsEmailVerified = true`, activate `BusinessId` |
| `POST /v1/business/login` | Email → generate 15-min magic link token, send email |
| `GET /v1/business/login/verify?token=` | Validate token, issue session cookie (`HttpOnly`, `SameSite=Strict`) |
| `POST /v1/business/logout` | Clear session cookie |
| `GET /v1/business/me` | Return current business profile (session-gated) |
| `POST /v1/business/locations` | Add single Navnelbnr to account (session-gated) |
| `POST /v1/business/locations/by-cvr` | **Pro only** — takes a CVR, queries `Establishments` where `CvrNumber = cvr`, bulk-inserts all matching Navnelbnrs into `BusinessNavnelbnrs` (skips duplicates) |
| `DELETE /v1/business/locations/{navnelbnr}` | Remove Navnelbnr from account (session-gated) |
| `GET /v1/business/locations` | List Navnelbnrs with current score (session-gated) |

### Session Auth
- ASP.NET Core built-in cookie session (`AddSession` + `AddDistributedMemoryCache`).
- Session key: `business_id` (int). Helper extension `HttpContext.GetBusinessId()`.
- No JWT — this is for browser dashboard, not API-to-API.

### New Service: `IBusinessService` / `BusinessService`
- `RegisterAsync(email, companyName)` — idempotent (existing unverified: resend email)
- `VerifyEmailAsync(token)` — returns Business or null
- `RequestMagicLinkAsync(email)` — generate `Guid.NewGuid()` token, store, send email
- `VerifyMagicLinkAsync(token)` — validate + expiry check, return Business

### Files
- `src/SmileyApi.Api/Endpoints/BusinessEndpoints.cs` (new)
- `src/SmileyApi.Core/Interfaces/IBusinessService.cs` (new)
- `src/SmileyApi.Infrastructure/Services/BusinessService.cs` (new)
- `src/SmileyApi.Api/Program.cs` (wire session middleware + endpoints)

---

## Phase E — Email Service (ACS)

### Interface: `IEmailService`
```csharp
Task SendVerificationEmailAsync(string to, string token);
Task SendMagicLinkEmailAsync(string to, string token);
Task SendScoreAlertEmailAsync(string to, string establishmentName, int newScore);
```

### Implementation: `AcsEmailService : IEmailService`
- NuGet: `Azure.Communication.Email`
- Config: `"Acs:ConnectionString"` in appsettings + Key Vault secret in prod
- From address: `noreply@smiley.dk` (or configured sender)

### Bicep
- Add `Microsoft.Communication/communicationServices` resource to `infra/`
- Add `Microsoft.Communication/emailServices` + domain resource

### Score-change Email Alerts (Pro only)
- In `EstablishmentSyncService.cs` — after MERGE OUTPUT collects changed Navnelbnrs, query `BusinessNavnelbnrs` joined with `Businesses` where `Tier = "pro"`, send email to each qualifying Business's `Email` via `IEmailService`.
- Free tier receives no score-change emails.

### Files
- `src/SmileyApi.Core/Interfaces/IEmailService.cs` (new)
- `src/SmileyApi.Infrastructure/Services/AcsEmailService.cs` (new)
- `src/SmileyApi.Infrastructure/Services/EstablishmentSyncService.cs` (extend score-change handler)
- `src/SmileyApi.Api/Program.cs` (register `IEmailService`)
- `infra/` Bicep files (ACS resources)

---

## Phase F — Stripe Integration

### Endpoints: `StripeEndpoints.cs`
| Endpoint | Description |
|---|---|
| `POST /v1/business/checkout` | Create Stripe Checkout session for Pro upgrade (session-gated) |
| `POST /v1/business/portal` | Create Stripe Customer Portal session (session-gated) |
| `POST /v1/stripe/webhook` | Public — validate Stripe signature, handle events |

### Stripe Events to Handle
- `checkout.session.completed` → set `Business.Tier = "pro"`, store `StripeCustomerId` + `StripeSubscriptionId`
- `customer.subscription.deleted` → revert `Business.Tier = "free"`, clear subscription fields

### Interface: `IStripeService`
- `CreateCheckoutSessionAsync(businessId, email, returnUrl)` → session URL
- `CreatePortalSessionAsync(stripeCustomerId, returnUrl)` → portal URL
- NuGet: `Stripe.net`
- Config: `"Stripe:SecretKey"`, `"Stripe:WebhookSecret"`, `"Stripe:PriceId"`

### Files
- `src/SmileyApi.Api/Endpoints/StripeEndpoints.cs` (new)
- `src/SmileyApi.Core/Interfaces/IStripeService.cs` (new)
- `src/SmileyApi.Infrastructure/Services/StripeService.cs` (new)
- `src/SmileyApi.Api/Program.cs` (register IStripeService, map endpoints)

---

## Phase G — Registered Widget Endpoint (BusinessId auth)

Extend `WidgetEndpoints.cs` to handle `?businessId=&navnelbnr=`:

1. Look up `businessId` → confirm Business exists and `IsEmailVerified = true`. If not found/unverified, fall back to anonymous free-tier response.
2. Read `Business.Tier` — no Navnelbnr ownership check needed. Inspection data is public; `businessId` is non-secret. The only purpose of BusinessId here is to determine tier.
3. Return: score, history, ReportUrl + `tier` field (`"free"` or `"pro"`).

The `tier` field in the response drives `widget.js` rendering — no client-side logic reads `businessId`.

> `BusinessNavnelbnrs` is NOT consulted here — it exists only for the dashboard location list and score-change email targeting.

### Files
- `src/SmileyApi.Api/Endpoints/WidgetEndpoints.cs`

---

## Phase H — Dashboard & Onboarding UI

### Pages to make functional (`wwwroot/`)
| Page | Work Needed |
|---|---|
| `index.html` | Live demo widget, tier comparison (Free/Pro only), two CTAs |
| `register.html` | Business registration form → `POST /v1/business/register` |
| `login.html` | Email input → `POST /v1/business/login` + "check your email" state |
| `dashboard.html` | Location list, per-location embed code, add/remove Navnelbnr, tier badge, upgrade button |

### Onboarding Flow

**Free tier** (single location):
1. User types CVR, name, or address → calls `GET /v1/establishments/search?q=`
2. Paginated results with live filtering
3. Map confirmation (if `GeoLat`/`GeoLng` available) or address text
4. User selects one location → Navnelbnr stored → embed code shown

**Pro tier** (CVR bulk onboard):
1. User enters a CVR number → calls `POST /v1/business/locations/by-cvr`
2. Backend queries `Establishments` by `CvrNumber`, auto-adds **all** associated Navnelbnrs to the account
3. Dashboard immediately shows all locations with individual embed codes
4. User can remove individual locations if needed
- Handles multi-location chains (e.g. McDonald's with 100+ outlets) in one step

### Files
- `src/SmileyApi.Api/wwwroot/index.html`
- `src/SmileyApi.Api/wwwroot/register.html`
- `src/SmileyApi.Api/wwwroot/login.html`
- `src/SmileyApi.Api/wwwroot/dashboard.html`
- `src/SmileyApi.Api/wwwroot/widget.js` (BusinessId rendering branch)

---

## Phase I — Developer API / Pro Webhooks
**Per revised plan: webhooks are a Pro business feature, not just developer API key feature.**

### Tasks
1. Expose webhook management endpoints to Business session auth (not just ApiKey auth).
2. Add `BusinessId` FK to `WebhookSubscriptions` as an alternative to `ApiKeyId`.
3. Dashboard webhook config UI (Pro-only section).

### Files
- `src/SmileyApi.Api/Endpoints/WebhookEndpoints.cs`
- `src/SmileyApi.Core/Models/WebhookSubscription.cs`
- `src/SmileyApi.Infrastructure/Data/Migrations/`

---

## Implementation Order

```
Phase A (Navnelbnr fix)     ← do immediately, unblocks everything
Phase B (History sparkline) ← small, pairs with A
Phase C (DB schema)         ← foundation for D, E, F, G
Phase D (Business auth)     ← depends on C; enables F, G, H
Phase E (ACS Email)         ← depends on D (uses Business email)
Phase F (Stripe)            ← depends on D
Phase G (Registered widget) ← depends on C, D
Phase H (Dashboard UI)      ← depends on D, E, F, G
Phase I (Pro Webhooks)      ← depends on D; can do last
```

---

## What NOT to Build (Per Revised Plan)
- Custom widget colors/branding — regulatory requirement prevents this
- Per-restaurant widget subscription pricing — focus is B2B API contracts
- Analytics table — deferred (not in revised plan scope)
- Domain allowlisting — deferred open question
- Admin UI — keep using existing dev-only admin endpoints

---

## Verification Checklist

1. **Navnelbnr embed**: `<script data-navnelbnr="1234567">` renders badge + sparkline
2. **Anonymous embed disabled**: `Widget:AllowAnonymousEmbed: false` → open requests return 403
3. **Registration flow**: POST register → email received → click link → `IsEmailVerified = true` in DB
4. **Magic link login**: request link → click → session cookie set → dashboard loads
5. **Location add/remove**: add single Navnelbnr via dashboard → appears in `BusinessNavnelbnrs` → remove works
6. **CVR bulk onboard (Pro)**: enter CVR → all associated Navnelbnrs auto-added in one request
7. **Registered widget**: `?businessId=biz_xxx&navnelbnr=xxx` → returns correct tier field
8. **Score-change email (Pro only)**: simulate score change → email sent to Pro business only; Free business receives no email
9. **Stripe upgrade**: click Upgrade → Checkout → `Tier = pro` in DB
10. **Stripe cancel**: Customer Portal cancel → `Tier = free` reverted
11. **Pro webhook**: Pro business subscribes to score change → webhook fired on next sync

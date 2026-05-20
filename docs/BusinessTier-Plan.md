# Smiley Score Widget & Business Registration Plan

> **Status: DRAFT — decisions below are tentative and will evolve as more areas are explored.**

---

## Context

Customers need a way to display their Danish food inspection score (from Fødevarestyrelsen) on their own website — similar to a Trustpilot badge. The current API uses per-user ApiKey auth with manual admin approval, which is too high-friction for self-service widget embedding. Since all inspection data is publicly available, we can build a frictionless open embed while offering a registration layer for businesses that want more features.

---

## Existing Database (Current State)

**Project:** `C:\Projects\Smiley`

| Table | Key Columns |
|---|---|
| `Establishments` | Id, Navnelbnr (unique), CvrNumber, Name, Address, PostalCode, City, LatestScore, ReportUrl, GeoLat, GeoLng |
| `Inspections` | Id, EstablishmentId (FK), SmileyScore, InspectedOn, RecordedAt |
| `ApiKeys` | Id, KeyHash, OwnerEmail, Tier (free/pro), RequestsToday, IsActive, LastResetAt |
| `AccessRequests` | Id, Name, Email, Company, UseCase, Status (0=pending/1=granted/2=rejected), ApiKeyId (FK) |
| `WebhookSubscriptions` | Id, ApiKeyId (FK), EstablishmentId (FK), CallbackUrl, SecretKey |

**Key fields for widget:**
- `Establishments.CvrNumber` — lookup key customers will use
- `Establishments.LatestScore` — cached score for fast widget load
- `Establishments.ReportUrl` — all widget tiers link here (official report)
- `Inspections.SmileyScore` + `InspectedOn` — used for history chart in registered/paid tiers

---

## Proposed Business Model (DRAFT — subject to change)

### Three-Tier Auth Architecture

#### Tier 1 — Open Embed (no auth, no registration)
- Anyone embeds a script tag with just a CVR number
- Widget shows: **latest score badge** + link to `ReportUrl`
- No BusinessId required; backend serves score by CVR directly
- Rate limited by IP to prevent abuse
- Embed code:
  ```html
  <script src="https://api.smiley.dk/widget.js?cvr=12345678"></script>
  ```

#### Tier 2 — Registered Business (free, self-service)
- Business registers with email + company name
- **Light email verification step** (verification link sent, must click to activate)
- Gets a `BusinessId` (non-secret, safe in public HTML — like a Google Analytics ID)
- Widget shows: **latest score + score history sparkline** + link to `ReportUrl`
- Unlocks: score-change email notifications, basic view analytics, widget customization
- Embed code:
  ```html
  <script src="https://api.smiley.dk/widget.js?businessId=biz_abc123&cvr=12345678"></script>
  ```

#### Tier 3 — Paid Business (premium)
- Upgrades from registered business account via Stripe
- Widget shows: **choice of widget styles** (badge, card, detailed panel, etc.) + link to `ReportUrl`
- Unlocks: multiple CVRs under one account, white-label, advanced analytics, webhooks

#### Tier 4 — Developer ApiKey (existing system — keep as-is, restricted to developers only)
- **Explicitly not for widget embedding** — widget access requires a BusinessId
- Full API access for server-to-server integration only
- Webhooks, bulk queries, programmatic use
- Existing manual approval flow stays (access requests reviewed by admin)
- No changes to existing `ApiKey` / `AccessRequest` tables and flow

---

## Architecture Flexibility Principle

**The tier system must be easy to reconfigure — including removing the free/open tier entirely and making everything paid.**

Design rules to enforce this:
- **All tier checks live in one place** — a `TierPolicy` service or a single middleware, never duplicated across endpoints
- **The open/anonymous CVR path is feature-flagged** — a single `appsettings.json` key (`Widget:AllowAnonymousEmbed: true/false`) gates whether unauthenticated `?cvr=` requests are served or rejected
- **Widget JS is tier-aware at runtime** — it reads a `tier` field from the widget data API response; the JS rendering logic branches on that value, not on which query param was passed
- **`Business.Tier` is the single source of truth** — adding a new tier (e.g. `enterprise`) only requires adding a value here and updating the policy, not touching widget code
- **Payment gating is a wrapper** — a future Stripe/payment check slots in as a condition on `Business.Tier`, not a rewrite of business logic

This means switching from "free open embed exists" to "everything requires a BusinessId" is a one-line config change + removing the anonymous route registration.

---

## New Database Objects Needed (DRAFT)

### `Businesses` table (new)
| Column | Type | Notes |
|---|---|---|
| Id | int PK | |
| BusinessId | nvarchar(32) | Public non-secret ID (e.g. `biz_abc123`), unique indexed |
| Email | nvarchar(256) | |
| CompanyName | nvarchar(256) | |
| Tier | nvarchar(16) | `free` / `paid` |
| IsEmailVerified | bit | Must be true before BusinessId is active |
| MagicLinkToken | nvarchar(64), nullable | Reused for email verification + magic login |
| MagicLinkTokenExpiry | datetime2, nullable | 24h for verification, 15min for login |
| StripeCustomerId | nvarchar(64), nullable | |
| StripeSubscriptionId | nvarchar(64), nullable | |
| CreatedAt | datetime2 | |
| VerifiedAt | datetime2, nullable | |

### `BusinessCvrs` table (new — join table)
| Column | Type | Notes |
|---|---|---|
| Id | int PK | |
| BusinessId | int FK → Businesses.Id | |
| CvrNumber | nvarchar(20) | References Establishments.CvrNumber |
| AddedAt | datetime2 | |

> One business account supports multiple CVR numbers. Each CVR gets its own widget embed code using the same `BusinessId`. The widget data endpoint resolves `?businessId=biz_abc123&cvr=12345678`, validating the CVR belongs to that business account.

---

## Widget Feature Matrix (DRAFT)

| Feature | Open (no auth) | Registered (free) | Paid |
|---|---|---|---|
| Latest score badge | Yes | Yes | Yes |
| Link to ReportUrl | Yes | Yes | Yes |
| Score history sparkline | No | Yes | Yes |
| Multiple widget styles | No | No | Yes |
| Score-change notifications | No | Email | Email + Webhook |
| View analytics | No | Basic | Advanced |
| Custom branding/colors | No | Limited | Full |
| Multiple CVRs | No | Yes | Yes |

---

## What Needs to Be Built

### Backend
1. **`Businesses` + `BusinessCvrs` tables + EF migration**
2. **Business registration endpoint** — `POST /v1/business/register` (email + company name → creates unverified record, sends verification email)
3. **Email verification endpoint** — `GET /v1/business/verify?token=xxx` (activates BusinessId, sets `IsEmailVerified = true`)
4. **Magic link login endpoint** — `POST /v1/business/login` (email → generates token, sends login email); `GET /v1/business/login/verify?token=xxx` (validates token, issues session cookie)
5. **CVR management endpoints** — `POST /v1/business/cvrs` (add CVR to account), `DELETE /v1/business/cvrs/{cvr}` (remove CVR)
6. **Widget data endpoint** — `GET /widget/score?businessId={id}&cvr={cvr}` (validates CVR belongs to business, returns score + history JSON); `GET /widget/score?cvr={cvr}` (open tier, latest score only, if anonymous embed enabled)
7. **Widget JS file** — `GET /widget.js` (served as static JS, self-contained)

### Frontend Pages
8. **Landing page** (`index.html`) — redesigned to lead with widget offering:
   - Live demo widget badge (using a real CVR)
   - Tier comparison table (open / registered / paid)
   - Two CTAs: "Register your business" + "Developer API access"
   - Simple embed code snippet
9. **Registration page** (`register.html`) — business sign-up form (email + company name)
10. **Login page** (`login.html`) — email input → magic link sent
11. **Customer dashboard** (`dashboard.html`) — protected, session-gated:
    - List of registered CVRs with live score badge preview per CVR
    - Ready-to-copy `<script>` embed snippet per CVR
    - "Add CVR" form
    - Remove CVR button
    - Account tier badge + "Upgrade to Paid" / "Manage subscription" button

### Payment — Stripe

**Model:** Monthly flat-fee subscription per business account (price set in Stripe dashboard, e.g. 99 DKK/month)

**Upgrade flow:**
1. Customer clicks "Upgrade to Paid" on dashboard
2. Backend creates a Stripe Checkout session → redirects to Stripe-hosted page
3. Customer pays → Stripe fires `checkout.session.completed` webhook
4. Webhook handler sets `Business.Tier = paid` + stores `StripeCustomerId` + `StripeSubscriptionId`
5. Customer lands back on dashboard showing paid features

**Cancellation (no UI to build):**
1. Customer clicks "Manage subscription" on dashboard
2. Backend creates a Stripe Customer Portal session → redirects to Stripe-hosted portal
3. Customer cancels on Stripe's page
4. Stripe fires `customer.subscription.deleted` webhook → `Business.Tier` reverts to `free`

**New backend pieces:**
- `POST /v1/business/checkout` — creates Stripe Checkout session (session-gated)
- `POST /v1/business/portal` — creates Stripe Customer Portal session (session-gated)
- `POST /v1/stripe/webhook` — public, validates Stripe signature, handles `checkout.session.completed` + `customer.subscription.deleted`
- `IStripeService` + `StripeService` in Infrastructure (NuGet: `Stripe.net`)

### Email — Azure Communication Services
- **NuGet:** `Azure.Communication.Email`
- **Config:** ACS connection string in `appsettings.json` / Azure Key Vault secret
- **Bicep:** add `communicationServices` + `emailServices` resources to existing IaC templates
- **Interface:** `IEmailService` abstraction so the provider can be swapped without touching business logic
- **Implementation:** `AcsEmailService : IEmailService` in `SmileyApi.Infrastructure/Services/`
- Verification email: token expires in 24h
- Magic link login email: token expires in 15 minutes

---

## Areas Still to Explore / Open Decisions

- [x] Email sending infrastructure — Azure Communication Services (ACS), new resource to add to Bicep
- [x] Widget JS delivery — static file served from wwwroot
- [x] Paid tier payment — Stripe Checkout, monthly flat-fee subscription, Stripe Customer Portal for management
- [ ] **[EXPLORE LATER]** Widget analytics storage — new table or third-party (Plausible, etc.)?
- [ ] **[EXPLORE LATER]** Domain allowlisting — should BusinessId only work on registered domains to prevent ID theft?
- [ ] Rate limiting for open embed (IP-based) — threshold values?
- [ ] Admin UI for managing business registrations?

---

## Files to Modify / Create

| File | Change |
|---|---|
| `SmileyApi.Core/Models/Business.cs` | New entity |
| `SmileyApi.Core/Models/BusinessCvr.cs` | New entity |
| `SmileyApi.Infrastructure/Data/SmileyDbContext.cs` | Add `DbSet<Business>`, `DbSet<BusinessCvr>` |
| `SmileyApi.Infrastructure/Migrations/` | New migration |
| `SmileyApi.Api/Endpoints/BusinessEndpoints.cs` | New: register, verify, login, CVR management |
| `SmileyApi.Api/Endpoints/WidgetEndpoints.cs` | New: widget data endpoint |
| `SmileyApi.Api/Endpoints/StripeWebhookEndpoint.cs` | New: webhook handler for subscription events |
| `SmileyApi.Api/wwwroot/widget.js` | New: embeddable widget script |
| `SmileyApi.Api/wwwroot/index.html` | Redesign: widget-first landing page |
| `SmileyApi.Api/wwwroot/register.html` | New: business registration form |
| `SmileyApi.Api/wwwroot/login.html` | New: magic link login page |
| `SmileyApi.Api/wwwroot/dashboard.html` | New: customer dashboard (CVR list + embed codes) |
| `SmileyApi.Core/Interfaces/IStripeService.cs` | New: Stripe service abstraction |
| `SmileyApi.Infrastructure/Services/StripeService.cs` | New: Stripe Checkout + Portal session creation |
| `SmileyApi.Core/Interfaces/IEmailService.cs` | New: email service abstraction |
| `SmileyApi.Infrastructure/Services/AcsEmailService.cs` | New: ACS implementation |
| `SmileyApi.Infrastructure/Bicep/` | Add ACS email + communication services resources |
| `SmileyApi.Api/Program.cs` | Wire up new endpoints, session middleware, email service |

---

## Verification Plan

1. Open embed: paste `<script src=".../widget.js?cvr=12345678">` in a plain HTML file, verify badge renders with score and ReportUrl link
2. Registration flow: POST to `/v1/business/register`, check email received, click link, verify `IsEmailVerified = true` in DB
3. Magic link login: request login link, click, verify session cookie issued and dashboard loads
4. CVR management: add + remove CVRs from dashboard, verify `BusinessCvrs` table updates
5. Registered widget: use `?businessId=biz_xxx&cvr=xxx`, verify history sparkline appears
6. Stripe upgrade: click "Upgrade", complete Stripe Checkout, verify `Tier = paid` in DB
7. Stripe cancellation: use Customer Portal to cancel, verify `Tier = free` reverts
8. Invalid CVR: verify graceful fallback (no badge rendered, no JS errors)
9. Anonymous embed disabled: set `Widget:AllowAnonymousEmbed: false`, verify open `?cvr=` requests return 403

# Smilr — Product Overview

**Domain:** smilrhq.dk  
**Stack:** .NET 9, Azure Container Apps, Azure SQL  
**Status:** Live in production (North Europe)

---

## What is Smilr?

Smilr is a SaaS API and embeddable widget platform that surfaces Danish food inspection scores (the "smiley" ratings from Fødevarestyrelsen) for restaurants, cafes, and food businesses. It syncs official public data daily and exposes it via a REST API and an embeddable JS badge — think "Trustpilot badge for food hygiene scores."

---

## Data Source

- **Provider:** Fødevarestyrelsen (Danish Veterinary and Food Administration)
- **Format:** Plain XML, downloaded and parsed every 24 hours (streamed via `HttpClient` + `XmlReader`, no ZIP step)
- **Sync method:** Background worker using streaming XML parser; upserts ~30k+ establishment records via bulk SQL MERGE
- **Score skip:** Sync is skipped on restart if data is less than 20 hours old (production only)
- **Manual override:** `POST /admin/sync` (requires `X-Admin-Key`) forces an immediate full resync, bypassing the 20-hour skip — see `docs/ProjectNotes.md`

---

## REST API

All endpoints require an `X-Api-Key` header (developer use only — not for widget embedding).

| Endpoint | Description |
|---|---|
| `GET /v1/establishments/{cvr}` | Lookup by CVR number, includes current score |
| `GET /v1/establishments/search?q=&page=&limit=` | Full-text LIKE search, max 100 results |
| `GET /v1/establishments/nearby?lat=&lng=&radius=` | Haversine geo search, 0–50 km, top 50 |
| `GET /v1/establishments/{cvr}/history` | Full inspection history, descending |

**Auth:** SHA-256 hashed API keys. Two tiers:

| Tier | Daily request limit |
|---|---|
| Free | 100 requests/day |
| Pro | 10,000 requests/day |

---

## Embeddable Widget

A lightweight JS snippet businesses paste into their own website to display their current smiley score.

- **Lookup key:** `?navnelbnr=` (establishment number — unique per location)
- **Open tier:** No account needed; shows latest score only
- **Registered tier (Business account):** Multi-CVR support, history sparkline, score badges
- **Links:** All widgets link to the official `ReportUrl` on findsmiley.dk
- **Feature flag:** `Widget:AllowAnonymousEmbed` — one-line toggle to require login for all embeds

---

## Business Accounts & Tier System

Self-service registration with magic-link email auth (no passwords).

| Feature | Free | Pro |
|---|---|---|
| Widget embed | Single CVR | Multiple CVRs |
| Score change email alerts | No | Yes (after each sync) |
| Customer dashboard | Basic | Full |
| API access | Separate (dev API key) | Separate (dev API key) |

**Registration flow:** Email → magic link → session cookie → dashboard  
**Dashboard (`dashboard.html`):** CVR list, embed code per location, tier badge, upgrade/manage buttons

---

## Payments

- **Provider:** Stripe (Checkout + Customer Portal)
- **Model:** Flat monthly subscription (no usage-based billing)
- **Events handled:** `checkout.session.completed` → upgrades to Pro; `customer.subscription.deleted` → downgrades to Free
- **No custom payment UI** — fully delegated to Stripe-hosted pages

---

## Webhooks (Developer API)

Real-time push notifications when an establishment's smiley score changes.

- **Trigger:** Detected during daily XML sync via SQL MERGE OUTPUT (old score vs. new score)
- **Delivery:** Hangfire job queue (auto-retry on failure), SQL Server-backed
- **Payload:** `smiley_score_changed` event with `currentScore`, `previousScore`, establishment details, `occurredAt`
- **Security:** HMAC-SHA256 signed — `X-Smiley-Signature: sha256=<hex>`; secret returned only on subscription creation

**Webhook endpoints:**

| Endpoint | Description |
|---|---|
| `POST /v1/webhooks` | Subscribe (returns secret once) |
| `DELETE /v1/webhooks/{id}` | Unsubscribe |
| `GET /v1/webhooks` | List subscriptions for API key |

---

## Email

- **Provider:** Azure Communication Services (transactional only)
- **Sent emails:** Magic-link login, score-change alerts (Pro businesses only)
- **Sender:** Azure Managed Domain (custom domain `smilrhq.dk` pending DNS verification)
- **Dev mode:** Emails redirect to developer override address

---

## Infrastructure

| Component | Service |
|---|---|
| App hosting | Azure Container Apps (consumption plan, North Europe) |
| Database | Azure SQL (Basic tier) |
| Secrets | Azure Key Vault (managed identity) |
| Observability | Application Insights + Log Analytics |
| Email | Azure Communication Services |
| CI/CD | GitHub Actions → ghcr.io → Bicep deploy |
| IaC | Bicep (modular: containerapps, sql, keyvault, acs) |

**Estimated monthly cost:** ~$8–12 (Container Apps + SQL Basic)

---

## Roadmap (Remaining Phases)

| Phase | Description |
|---|---|
| G | Registered widget endpoint: returns tier field so widget can render tier-gated features |
| H | Full static pages: `register.html`, `login.html`, `terms.html`, polished dashboard |
| I | Pro webhooks accessible via Business session auth (not just developer API key) |

---

## Competitive Context

- **Data:** 100% public, officially sourced — no scraping risk
- **Differentiation:** Only platform packaging this data as a self-service embeddable widget + API
- **Target users:** Restaurant owners, food review platforms, B2B data consumers, health/hygiene apps
- **Geography:** Denmark-only data (Fødevarestyrelsen coverage)

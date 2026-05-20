# SmileyApi — Architecture & Product Decisions

> Captured from product/architecture discussion. Use this as context when exploring the codebase in Claude Code.

---

## 1. What SmileyApi Is

An embeddable Danish food inspection score widget platform. Food businesses in Denmark are **legally required** to display their Fødevarestyrelsen smiley score online (mandatory since Oct 2023, all online channels). The official solution (findsmiley.dk) only provides a static text link — no embeddable badge exists. SmileyApi fills that gap.

---

## 2. Competitive Landscape

| Competitor | What they do | Threat level |
|---|---|---|
| eSmiley / GladSmiley | Internal HACCP/egenkontrol software | None — different market |
| FoodHygieneCheck.com | Consumer-facing lookup site | None — not a B2B widget |
| Wolt / Just Eat | Built smiley display internally for their own platform | Indirect — they solved it for themselves only |
| Fødevarestyrelsen | Offers a lup-icon link, nothing embeddable | None |

**No direct competitor exists** for a self-service embeddable widget product.

---

## 3. Fødevarestyrelsen Design Constraints

Widget visual customisation is **largely off the table**. Their public data terms require:

- Displayed smiley must **always** match the establishment's current actual smiley
- Official smiley **design requirements** must be respected
- Fødevarestyrelsen must be **cited as source**
- Their **logo may not be used**

**Implication:** "Custom widget styles/colours" cannot be a paid upsell. Remove from tier feature matrix.

---

## 4. Identifier Architecture — Critical Decision

### The Danish identifier hierarchy

```
CVR  (1)                 → legal company entity ("Flammen A/S")
  └── P-nummer  (many)   → each physical location
        └── Navnelbnr (1 per location) → Fødevarestyrelsen's own ID (KOR register)
              └── Smiley score + inspection history
```

### Key facts

- Fødevarestyrelsen does **NOT** identify establishments by CVR. They use **Navnelbnr** from their internal Kontrol Objekt Register (KOR).
- CVR is only stored alongside Navnelbnr for reference.
- One CVR can map to **many** Navnelbnr values (tested: one CVR returned 118 establishments — large chains like Netto, McDonald's etc.).
- Each Navnelbnr has its own independent smiley score and inspection history.
- P-nummer always maps 1:1 with a physical address and links to both CVR and Navnelbnr.

### Decision

> **Navnelbnr is the widget lookup key. CVR is only used for onboarding UX.**

---

## 5. Embed Code Design

```html
<!-- Free / anonymous — no registration required -->
<script src="https://api.smiley.dk/widget.js?navnelbnr=717608"></script>

<!-- Registered or paid business -->
<script src="https://api.smiley.dk/widget.js?businessId=biz_abc123&navnelbnr=717608"></script>
```

- `navnelbnr` always identifies the **exact physical location** — unambiguous
- `businessId` layers on top to unlock features — does not change lookup logic
- Same backend resolution code path for all tiers
- Tier only controls **what features are returned** in the response (score only vs. history, analytics, etc.)
- Future upgrade: embed code **never needs to change** — same `navnelbnr` just returns richer data

---

## 6. `BusinessCvrs` Table — Schema Correction

Current schema uses `CvrNumber` as the lookup key. This is wrong for multi-location businesses.

### Corrected schema

| Column | Type | Notes |
|---|---|---|
| Id | int PK | |
| BusinessId | int FK → Businesses.Id | |
| **Navnelbnr** | nvarchar(20) | **Primary widget/lookup key** — FK to Establishments.Navnelbnr |
| CvrNumber | nvarchar(20) | Keep for onboarding reference only |
| PNumber | nvarchar(20), nullable | Store for completeness |
| AddedAt | datetime2 | |

---

## 7. Tier Strategy — Revised

Original 3-tier plan (Open / Registered / Paid) reconsidered. Small single-location restaurants will not pay. They are the free tier, adoption volume, and word-of-mouth — do not try to monetise them directly.

### Revised feature matrix

| Feature | Free (open embed) | Pro (paid) |
|---|---|---|
| Score badge widget | ✅ | ✅ |
| Link to ReportUrl | ✅ | ✅ |
| Score history sparkline | ✅ | ✅ |
| Email score-change alerts | ✅ | ✅ |
| Multiple locations dashboard | ❌ | ✅ |
| Webhook integrations | ❌ | ✅ |
| Advanced analytics | ❌ | ✅ |
| Developer API access | ❌ | ✅ |
| Custom widget styles/colours | ❌ | ❌ (Fødevarestyrelsen rules) |

### Real paid customers

- **Restaurant chains / franchise operators** (5–100+ locations) — need to monitor all locations, score-change alerts across the portfolio
- **Food delivery platforms** (Wolt, Just Eat) — need reliable API + Navnelbnr lookup to keep their listings current
- **Web agencies** — managing smiley compliance for multiple restaurant clients
- **Hospitality / canteen / care home operators** — same multi-location logic

### The developer API is the real revenue product

Platforms like Wolt built their own Navnelbnr integration from scratch. Your API lets others skip that work. B2B API contract > widget subscription from small restaurants.

---

## 8. Onboarding Flow — All Tiers

CVR lookup is a **search tool**, not a direct resolver. One CVR can return 100+ establishments for large chains — neither a map nor a dropdown works alone at that scale.

### Recommended flow

```
Step 1 — Entry (flexible)
  User enters CVR  →  or  →  User searches by name / address / city

Step 2 — Filter within results
  Show paginated list with live search filter: "Filter by city or address..."
  Each row: Name · Address · City · [Select]

Step 3 — Confirmation
  User clicks Select
  If GeoLat/GeoLng present  → show single-pin map ("Is this your location?")
  If GeoLat/GeoLng missing  → show address text confirmation only
  Confirm → Navnelbnr captured → embed code generated

Step 4 — Embed code
  Free user  → copy <script> snippet, done (no account needed)
  Registered → Navnelbnr stored in BusinessCvrs, snippet in dashboard
```

### Map picker decision

- Map is a **confirmation tool** for a single already-selected location
- Map is **not** a selection tool across N pins (unusable at 118 pins)
- Use Google Maps JS API Advanced Markers with custom SVG smiley glyph
- Already have `GeoLat` / `GeoLng` on `Establishments` — no extra data needed
- Fallback to address text for rows with missing geo data

---

## 9. Geo Data Quality Issue

**Finding:** One CVR returned 118 establishments, some with missing `GeoLat`/`GeoLng`.

**Implication:** Geo data cannot be a hard dependency anywhere in the UI or backend.

**Recommended fix:** Background geocoding job using existing `Name`, `Address`, `PostalCode`, `City` fields (Google Maps Geocoding API or free alternative). One-time enrichment pass + include geocoding in the ongoing XML sync job for new/updated records.

---

## 10. Landing Page — Zero-Friction Embed Generator

Add a **CVR lookup tool** on the public landing page:
- User enters CVR → search/filter → select location → embed code shown immediately
- **No email, no registration required** for free tier
- Demonstrates value in under 60 seconds
- Strong conversion hook for registration upsell ("Want score-change alerts? Register free →")

---

## 11. Google Maps Integration — Backlog

Not a core feature. Viable options when ready:

1. **Dashboard map** — show business owner their registered locations with smiley pins. Uses existing `GeoLat`/`GeoLng` + Google Maps JS API Advanced Markers (custom SVG glyph).
2. **Public consumer explorer map** — all Danish food establishments plotted with smiley pins. Drives organic traffic. Uses same geo data.

**Not viable:** Direct injection into Google Maps for third parties — not technically possible. Trustpilot stars appearing "on Google Maps" is a misconception; they appear in Google Ads Seller Ratings only.

**Status: backlog — implement after core widget is shipped.**

---

## 12. Existing Database (Reference)

```
Establishments  — Id, Navnelbnr, CvrNumber, Name, Address, PostalCode, City,
                  LatestScore, ReportUrl, GeoLat, GeoLng

Inspections     — Id, EstablishmentId (FK), SmileyScore, InspectedOn, RecordedAt

ApiKeys         — Id, KeyHash, OwnerEmail, Tier, RequestsToday, IsActive, LastResetAt

AccessRequests  — Id, Name, Email, Company, UseCase, Status, ApiKeyId (FK)

WebhookSubscriptions — Id, ApiKeyId (FK), EstablishmentId (FK), CallbackUrl, SecretKey
```

**New tables needed:** `Businesses`, `BusinessCvrs` (corrected schema — see §6)

---

## 13. Open Questions / Next Exploration Areas

- [ ] Geocoding job — how many rows are missing `GeoLat`/`GeoLng`? Worth quantifying before building the fix.
- [ ] Should `Navnelbnr` replace `EstablishmentId` (FK) in `BusinessCvrs`, or join via `Establishments.Id`? Consistency with existing FK conventions.
- [ ] Rate limiting for anonymous free embed — what IP-based threshold is safe?
- [ ] Should the landing page CVR lookup tool be a separate lightweight endpoint, or reuse the widget data endpoint?
- [ ] Backlog: Google Maps dashboard + public explorer map (see §11)

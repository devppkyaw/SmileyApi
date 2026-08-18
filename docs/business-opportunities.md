# Smilr — Non-Subscription Business Opportunities

Ideas for monetizing the Smilr dataset/infrastructure without requiring a restaurant to create a Business account. Captured 2026-08-11.

## Decision (2026-08-18)

Goal is growing **Business subscriptions**, not standalone API revenue. Priority: **directory site first**, marketplace listing deprioritized (it drives API/developer revenue, not subscriptions — doesn't serve the stated goal).

**Suggested sequence:**
1. Finish Phase H static pages (`register.html`, `login.html`, `terms.html`, polished dashboard) — the directory's "Claim this listing" CTA needs a solid landing destination, so this should land first or in parallel.
2. Build a minimal public directory: search + nearby + per-establishment page, reusing existing `/search`, `/nearby`, `/{cvr}/history` logic behind a new anonymous/cached route group. Add the "Claim this listing" CTA on every establishment page linking into the Phase H registration flow.
3. Launch early, even before every page is polished — SEO indexing takes weeks/months, so time-in-index matters more than initial polish.
4. Monitor claim-through rate (directory visits → registrations) as the core success metric.
5. Once claim conversion is validated, invest in Phase G (registered widget tier field) and Phase I (Pro webhooks via session) to strengthen the Pro value proposition for everyone the directory brings in.
6. Chrome extension and API marketplace listing stay as optional, later additions — not on the critical path to subscription growth.

---

## 1. Consumer-facing directory site ("Smilr Finder") — PRIORITIZED

A public, no-login search/browse experience over the same data already served by the dev API.

**Core loop:** user searches by area/postcode or "near me" → sees a list of establishments with current smiley score + trend → taps through to a per-establishment page with full inspection history.

**Reuses existing backend:** `/v1/establishments/search`, `/nearby`, and `/{cvr}/history` logic already exists — this is a new *public, anonymous, cached* route group in front of the same service/repository layer, not a new data pipeline. No `X-Api-Key` required for these routes; rate-limit by IP instead.

**Why it matters beyond traffic:**
- Programmatic SEO — ~30k establishments = ~30k indexable pages. This is the single biggest lever; directory/rating sites (Yelp, Trustpilot) grew this exact way.
- Funnels back into the existing subscription business: a "Claim this listing" CTA on each establishment page is the natural top-of-funnel into the current Business/Pro registration flow — so this doesn't compete with the subscription product, it feeds it. This is the primary reason it's prioritized over the marketplace listing.

**Monetization:** the claim-listing funnel into Business subscriptions is the main goal now; display ads and affiliate deals remain secondary options once there's traffic.

**Effort:** mostly frontend + one new "public" controller with caching (Redis or in-memory + short TTL) and IP rate limiting. Low backend risk since it's read-only and reuses existing queries. Depends on Phase H static pages being ready as the claim destination.

**Extension idea (later):** a small Chrome extension that overlays the smiley badge on Google Maps results for Denmark searches — thin content script calling the new public endpoint by name/CVR match. Good discovery channel for the directory site, not urgent.

---

## 2. API marketplace listing (e.g. RapidAPI) — DEPRIORITIZED

List the existing Free/Pro developer API tiers on a marketplace instead of (or alongside) direct signup.

**Reuses:** current API-key auth (SHA-256 hashed keys) and rate-limit tiers — marketplace just becomes another key-issuance channel.

**Effort:** mostly packaging — OpenAPI spec, example requests/responses, mapping existing Free (100/day) and Pro (10,000/day) tiers to marketplace pricing plans.

**Why deprioritized:** this monetizes data consumers (developers), not restaurants — it doesn't grow Business subscriptions, which is the stated priority. Worth revisiting later as a separate revenue stream once the subscription funnel is established.

---

## Relationship to existing roadmap (Phases G–I)

G–I (registered widget tier field, static pages, session-based Pro webhooks) are all about the *Business* side. The directory site depends on Phase H (static pages) for its claim-listing destination; G and I are follow-ons that deepen the Pro offering once the directory is driving signups.

**Open decisions:**
- Own domain/subdomain for the consumer site vs. path on smilrhq.dk (affects SEO strategy)
- Caching/infra approach to keep public anonymous traffic from hitting Azure SQL directly at scale

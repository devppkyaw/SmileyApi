# Smilr — Business Opportunities & Positioning

Ideas for growing Smilr beyond the core widget. Captured 2026-08-11, revised 2026-08-18.

## Decision (2026-08-18, revised): API + Business analytics first, directory repositioned as SEO/acquisition channel

**Why revised:** findsmiley.dk (the official government site) already offers, for free: a shareable/embeddable link per establishment, an email subscribe feature per establishment, and a QR code physically posted at every business's entrance linking straight to their report. Competing on "look up a restaurant's smiley score" is not a winnable or necessary fight — that job is already done, for free, with a built-in physical distribution advantage (QR code on every door) that Smilr can't match.

**Where findsmiley.dk structurally can't/won't compete — this is where Smilr's value should concentrate:**
- **Developer API as a real product.** findsmiley.dk's "Hent Smiley-data" is a raw XML dump. Smilr's `/search`, `/nearby`, `/{cvr}/history` with API keys, tiers, rate limits, and webhooks is a polished, documented, integratable product — the kind of thing a delivery app, relocation service, or discovery app can build on without doing their own XML parsing. This is a real, defensible gap.
- **Business-facing analytics/SaaS tooling.** The regulator isn't in the business of helping restaurant owners understand or act on their score — no trend analytics, no competitor/area benchmarking, no multi-location dashboard for chains, no branded (non-government-styled) widget. This is what the Business/Pro subscription should actually be selling — not "your score shows up on a page" (findsmiley already does that for free), but insight and tooling.

**Directory site — repositioned, not dropped:**
Still worth building, but as an SEO/acquisition channel feeding the API and Business products, not as the standalone product. To justify ranking (and avoid being thin/duplicate content vs. findsmiley.dk), each page should surface something findsmiley doesn't: trend charts, area/competitor comparisons, "most improved" framing, etc. — which also reinforces the analytics positioning rather than undercutting it.

**Revised sequence:**
1. Reframe the Business subscription pitch around analytics/tooling (trend analysis, benchmarking, multi-location dashboard, branded widget) rather than visibility alone.
2. Package and promote the developer API as a standalone product (docs, OpenAPI spec, positioning against "you'd otherwise have to parse a 59MB government XML file yourself").
3. Build the directory as a secondary SEO/acquisition layer once it has real differentiated content per page (not just a score mirror) — see URL structure decision and trend-feed idea below.
4. Marketplace listing (RapidAPI-style) remains a good complementary distribution channel for #2, still not urgent.

---

## URL structure decision (2026-08-18)

Original proposed path: `smilrhq.dk/find/{cvrnumber}/{navnelbnr}` — **revise before broad indexing.**

Problems with the original path:
- Two IDs in one URL is redundant. `navnelbnr` is already unique per physical location — that's the correct key for a single establishment page. CVR only becomes useful for a separate *parent* page listing all locations under one business (useful for chains, and a second page type that can rank for "[chain name] locations" searches).
- Numeric-ID-only paths carry no SEO or click-through value. Readable slugs (area + business name) do.

**Decided and implemented (2026-08-19), under the existing `/find/` prefix:**
- Area hub: `GET /find/{area-slug}/` — can rank for "restaurants in [area]" searches, links down to individual establishments (topical clustering, standard pattern for directory SEO). Area-slug is keyed on `Establishment.City` (see "Resolved" note below).
- Establishment page, canonical when the establishment has a City: `GET /find/{area-slug}/{business-slug}-{navnelbnr}`.
- Establishment page, canonical fallback when the establishment has no City: `GET /find/{business-slug}-{navnelbnr}` (no area to nest it under).
- Both slugs are computed on the fly from `Name`/`City` (never persisted); a URL whose slug text doesn't match what's freshly computed 301-redirects to the canonical form — see `FindUrlBuilder.cs`/`FindEndpoints.cs`.
- Optional chain hub page: `/kaede/{cvr}/` — for multi-location businesses, listing all their locations. Not yet built.

An earlier-considered alternative — dropping the `/find/` prefix entirely and making the bare `/{slugAndId}` (no area segment) the sole canonical URL for every establishment, with the area-prefixed form as internal-link-only — was evaluated and **not adopted**; the two-tier canonical-by-City-presence scheme above is what's live. `FindEndpoints.cs`'s old `/find/{cvr}` + `/find/{cvr}/{navnelbnr}` routes (raw CVR/Navnelbnr, no slugs, no area hub) have been fully replaced by this, not left running alongside it.

This shipped before broad indexing, so no legacy-URL redirect layer was needed.

---

## 1. Developer API as a product — PRIORITIZED

Reuses existing `/v1/establishments/search`, `/nearby`, `/{cvr}/history`, and the existing Free/Pro key-tier system. Effort is mostly packaging: OpenAPI spec, docs, example requests, positioning against the raw XML dump as the alternative. Marketplace listing (RapidAPI-style) is a good secondary distribution channel for this once the core docs/positioning exist.

## 2. Business analytics/SaaS tooling — PRIORITIZED

The real value proposition for the paid Business tier, since visibility alone is already free via findsmiley.dk. Concrete ideas: score trend charts over time, benchmarking against nearby/similar establishments, a multi-location dashboard for chains (using the existing multi-CVR Pro support), a widget with brand customization instead of the standard government styling. Depends on Phase G (tier field) and Phase I (session-based Pro webhooks) from the existing roadmap to fully build out.

## 3. Consumer-facing directory site ("Smilr Finder") — SECONDARY, SEO/acquisition role

See URL structure decision above. Each page needs differentiated content (trend/comparison data) beyond a plain score mirror to be worth ranking and to reinforce the analytics positioning. "Claim this listing" CTA still funnels into Business registration. Chrome extension idea remains a later, optional addition.

**Next differentiator to build, once routing lands — "Recently changed" / "Most improved" trend feed:**
Low-effort, high-differentiation: the webhook system already detects `smiley_score_changed` events (old score vs. new score) via SQL MERGE OUTPUT during each sync — this is the exact data needed, already captured, just not yet exposed as a public read surface. Concrete pages:
- `/find/{area-slug}/changes` — live feed of establishments whose score moved in the latest sync, filterable by area. Fresh, frequently-changing content (good for repeat crawling), and something findsmiley's static per-restaurant lookup has no equivalent for.
- "Most improved" / "recently downgraded" leaderboards — same data, framed as a ranked list. Shareable/link-bait potential (local blogs/news linking to "which Copenhagen restaurants improved this month") that a plain lookup tool doesn't generate.
- Area health snapshot — aggregate stats per area ("87% of restaurants in Nørrebro currently have a green smiley") — editorial/data-journalism angle, reference-worthy rather than one-time-use.

All three reuse the same trend-data capability that also feeds the Business analytics dashboard (#2) — one data layer, two audiences (public differentiation + paid tooling).

## 4. API marketplace listing (e.g. RapidAPI) — SECONDARY

Packaging work on top of #1. Distribution channel for developer audience, not a subscription driver directly.

---

## Competitive note: findsmiley.dk (confirmed 2026-08-18)

- QR code posted at ~46,000 business entrances since Dec 2023, linking directly to that business's latest inspection report on findsmiley.dk.
- findsmiley.dk establishment pages already offer: a copyable embed link ("Kopier link til at indsætte på virksomhedens hjemmeside") and an email subscribe field — a free, lightweight version of Smilr's widget + alert features.
- "Hent Smiley-data" offers raw data download (XML), not a polished/documented developer API.
- No news found on API/open-data changes or XML feed changes as of 2026-08-18.
- findsmiley.dk has no trend/change feed, no leaderboard, no area aggregate stats — it's a static per-establishment lookup only. This is the gap the "Recently changed" feed (above) is designed to exploit.

---

## Relationship to existing roadmap (Phases G–I)

Phase G (registered widget tier field) and Phase I (session-based Pro webhooks) directly support the analytics/SaaS positioning (#2) — prioritize these over Phase H polish if forced to choose, since they're now on the critical path to the primary value prop rather than just directory support. Phase H (static pages) still needed as the registration landing destination for both the API signup and the directory's claim-listing CTA.

**Open decisions:**
- Exact set of analytics features for the Business dashboard (trend charts, benchmarking, multi-location view — prioritize which ships first)
- Caching/infra approach to keep public anonymous directory traffic from hitting Azure SQL directly at scale

**Resolved (2026-08-19):** Area-slug taxonomy is City name only (not postcode) — `Establishment.City` is the raw, unnormalized string from the source XML's `<By>` field; there's no lookup/reference table, so the area-slug is `Slugifier.Slugify(City)` computed on the fly, with establishments grouped into the same area whenever their City text slugifies identically. `/find` was restructured onto `/find/{area-slug}/` (hub) and `/find/{area-slug}/{business-slug}-{navnelbnr}` (detail, or `/find/{business-slug}-{navnelbnr}` for the rare establishment with no City) — see `FindEndpoints.cs`/`FindUrlBuilder.cs`.

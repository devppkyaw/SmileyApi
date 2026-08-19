# Smilr — Business Opportunities & Positioning

Ideas for growing Smilr beyond the core widget. Captured 2026-08-11, revised 2026-08-18, 2026-08-19.

## Decision (2026-08-18, revised): API + Business analytics first, directory repositioned as SEO/acquisition channel

**Why revised:** findsmiley.dk (the official government site) already offers, for free: a shareable/embeddable link per establishment, an email subscribe feature per establishment, and a QR code physically posted at every business's entrance linking straight to their report. Competing on "look up a restaurant's smiley score" is not a winnable or necessary fight — that job is already done, for free, with a built-in physical distribution advantage (QR code on every door) that Smilr can't match.

**Where findsmiley.dk structurally can't/won't compete — this is where Smilr's value should concentrate:**
- **Developer API as a real product.** findsmiley.dk's "Hent Smiley-data" is a raw XML dump. Smilr's `/search`, `/nearby`, `/{cvr}/history` with API keys, tiers, rate limits, and webhooks is a polished, documented, integratable product — the kind of thing a delivery app, relocation service, or discovery app can build on without doing their own XML parsing. This is a real, defensible gap.
- **Business-facing analytics/SaaS tooling.** The regulator isn't in the business of helping restaurant owners understand or act on their score — no trend analytics, no competitor/area benchmarking, no multi-location dashboard for chains, no branded (non-government-styled) widget. This is what the Business/Pro subscription should actually be selling — not "your score shows up on a page" (findsmiley already does that for free), but insight and tooling.

**Directory site — repositioned, not dropped:**
Still worth building, but as an SEO/acquisition channel feeding the API and Business products, not as the standalone product. To justify ranking (and avoid being thin/duplicate content vs. findsmiley.dk), each page should surface something findsmiley doesn't: trend charts, area/competitor comparisons, "most improved" framing, category browsing, etc.

**Revised sequence:**
1. Reframe the Business subscription pitch around analytics/tooling (trend analysis, benchmarking, multi-location dashboard, branded widget) rather than visibility alone.
2. Package and promote the developer API as a standalone product (docs, OpenAPI spec, positioning against "you'd otherwise have to parse a 59MB government XML file yourself").
3. Build the directory as a secondary SEO/acquisition layer — area hub pages, category hub pages, and the "recently changed" trend feed (see below) — not a plain score-lookup mirror.
4. Marketplace listing (RapidAPI-style) remains a good complementary distribution channel for #2, still not urgent.

---

## Reference sites (researched 2026-08-19)

Checked for comparable products, not limited to Denmark:

- **[FoodSafe Score API](https://foodsafescoreapi.com/)** — near-direct validation of the API-first pivot: normalizes US inspection data from 400+ jurisdictions into unified scores, sold to developers/delivery platforms/insurers on a tiered API model (free 500 req/mo → $29/mo → $99/mo, with webhook alerts as a paid-tier feature). Confirms this business shape works commercially.
- **[Scores on the Doors](https://www.scoresonthedoors.org.uk/)** (UK) — closest analog to a plain consumer directory wrapping official hygiene data (UK's FHRS scheme). Cautionary, not a model to copy: monetizes mainly through affiliate referrals (training courses, a compliance-tools subscription), not a real subscription or API business — supports the earlier conclusion that a plain wrapper around free government data doesn't monetize well on its own.
- **[Ecolab HDI](https://www.ecolab.com/offerings/ecolab-hdi)** (formerly Hazel Analytics, acquired by Ecolab; previously powered health scores on ~700k Yelp listings) — strong validation of the Business analytics dashboard direction: standardized cross-jurisdiction scoring, peer benchmarking, near-real-time violation alerting, corrective-action tracking, violation-trend mapping. Sold to multi-location chains (250+ brands, 100k+ locations) via sales consultation — larger scale than Smilr's likely initial market (independents/small chains), but validates the feature set is something operators pay for.

---

## URL structure — decision, implementation, and correction (2026-08-18 → 2026-08-19)

Original proposed path: `smilrhq.dk/find/{cvrnumber}/{navnelbnr}` — rejected before broad indexing (redundant IDs, no SEO/CTR value — see reasoning history below if needed).

**Live structure, using unambiguous notation (not the C# route-parameter name "slugAndId"):**
- `/find/{area-slug}/` — hub page, listing establishments in that area. Area-slug is keyed on `Establishment.City` (raw/unnormalized, grouped by identical slugified text — no reference/lookup table).
- `/find/{area-slug}/{business-slug}-{navnelbnr}` — canonical detail page, establishment has a City.
- `/find/{business-slug}-{navnelbnr}` — canonical fallback detail page, establishment has no City (rare).

Both slugs are computed on the fly from `Name`/`City` (never persisted) via `FindUrlBuilder.cs` — the single source of truth used by both link generation and canonical-redirect matching. A request whose slug text doesn't match what's freshly computed 301-redirects to the canonical form (catches stale slugs, a City that changed, or hand-typed/probing URLs).

**Correction to the original routing plan:** the trailing-slash disambiguation originally proposed (distinguishing `/find/{area-slug}/` from a bare `/find/{business-slug}-{navnelbnr}` by the presence of a trailing slash) does not work in ASP.NET Core — confirmed via `AmbiguousMatchException` during implementation. The router treats a literal-trailing-slash template and a single-parameter template as equally-specific candidates for the same request, not as distinguishable.

**What actually works, and is now the standard pattern for this kind of ambiguity in this codebase:** merge the conflicting routes into one (`/find/{segment}`) and disambiguate at runtime by whether the segment matches the `{business-slug}-{navnelbnr}` shape (ends in `-{digits}`, via regex). If it matches, it's a detail page; otherwise it's treated as an area slug. This is the pattern to reuse for any future routing-shape conflict at the same URL depth — see category hub pages below.

CVR is no longer part of any `/find` URL. It survives only as a `/find/search?q={8-digit-cvr}` convenience shortcut (people know their CVR, not their Navnelbnr) that resolves and 301-redirects to the real canonical detail path. An optional chain hub page (`/kaede/{cvr}/`, listing all locations under one CVR) remains a possible future addition, not yet built.

This shipped before broad indexing, so no legacy-URL redirect layer was needed.

---

## Category hub pages — decision (2026-08-19)

**Data constraint found first:** there is no cuisine-level data anywhere in the Fødevarestyrelsen source (checked both `Pixibranche` and the more granular `branche` field directly against the XML). The largest category, `Restauranter, pizzeriaer, kantiner m.m.`, lumps restaurants, pizzerias, and canteens into one bucket of 23,200 establishments (~40% of the dataset) — there is no field distinguishing a pizza place from a sushi place. A `/find/{area-slug}/pizza`-style page is **not buildable from official data** without either guessing from business names (unreliable) or pulling in a second, non-government data source — which would undercut Smilr's "100% public, officially-sourced, no scraping risk" positioning. Cuisine-level search/browsing is out of scope unless that tradeoff is deliberately revisited later.

**What the data does support — 26 real Pixibranche-derived categories**, e.g.:
- Restauranter, pizzeriaer, kantiner m.m. — 23,200
- Dagligvarer (grocery stores) — 11,554
- Hospitals- og institutionskøkkener (institutional kitchens) — 8,808
- Delikatesse og smørrebrød (deli) — 1,485
- Bagere og bagerafdelinger (bakeries) — 1,167
- Slagtere, slagterafdelinger (butchers) — 968
- Fiske- og vildtforretninger (fishmongers) — 343
- (full list of 26 in `docs/Smiley_xml.xml` — re-derive per sync in case Fødevarestyrelsen adds/renames a value)

**Route:** `/find/{area-slug}/{category-slug}` — a listing/hub page only, filtered by area + Pixibranche category.

**Decided: no nested detail URL.** `/find/{area-slug}/{category-slug}/{business-slug}-{navnelbnr}` will not exist. The category hub links out to the existing flat canonical detail page (`/find/{area-slug}/{business-slug}-{navnelbnr}`) — it never hosts its own copy of the establishment page. Reasoning:
- **Identity vs. classification.** A URL should encode "which business, where" (stable identity), not "which browsing path was used to find it." Category is metadata about a business, not part of what makes it that business.
- **Category is measurably less stable than City.** The source data includes transitional/unresolved values (`Virksomheder, detail-branche endnu ikke tildelt` — "not yet assigned"; ownership-change states) — real evidence that category reclassification happens. Baking it into the canonical path would mean every reclassification breaks the business's permanent URL, stacking a second, more volatile source of redirect churn on top of the one already accepted for City.
- **Future-proofing.** Every establishment has exactly one `Pixibranche` value today, but if the taxonomy is ever split finer (or a second dimension like price tier is added later), a flat canonical URL doesn't care how many ways a business can be classified — each new dimension just becomes another hub page linking to the same one URL, rather than requiring a redesign.
- **Keeps canonical-redirect logic simple.** `DetailHandlerAsync`'s canonical check only has to reason about City + Name today; making category part of the canonical path would add Pixibranche as a second, independent trigger for that same redirect logic.

Category context (breadcrumb: Area → Category → Business Name) can still appear in the UI and in `BreadcrumbList` structured data on the detail page for SEO rich-snippet purposes — that's a presentation/schema concern, not a URL concern.

**Routing implementation:** `/find/{area-slug}/{business-slug}-{navnelbnr}` (existing) and `/find/{area-slug}/{category-slug}` (new) are the same two-segment route shape — the same `AmbiguousMatchException` risk already hit once at the one-segment level. Fix: merge into one route, `GET /find/{area-slug}/{segment}`, and reuse the proven disambiguation pattern — if `segment` matches the `-{digits}` suffix shape, it's a detail page; else if it matches one of the 26 known category slugs, it's a category hub; else, 404. Re-validate the category slug list against fresh sync data rather than hardcoding it permanently, in case Fødevarestyrelsen changes Pixibranche values.

**Open (not yet decided): minimum-establishment-count guard.** Area × category is a combinatorial page type (26 categories × N areas); many combinations will have very few or zero establishments (e.g. a small town with no butchers). Those should not be generated as indexable pages (either not generated at all, or `noindex`ed) to avoid thin-content pages diluting the rest of the directory's SEO — exact threshold still to be decided.

---

## Search UX — decision (2026-08-19)

Explored whether the on-site search box should be restructured (e.g. a "what" + "where" two-field split, or a name/address split like Scores on the Doors uses) to support queries like "pizza in Aalborg." **Decided against redesigning the search box** — instead, lean on the category + area hub pages above to do that job via SEO: Google sends people directly to `/find/{area-slug}/{category-slug}` for a query like "restaurants in Aalborg" (noting the cuisine-level limitation above — "pizza" specifically isn't achievable), rather than requiring the on-site search box to parse compound queries. The existing basic search (`/find/search?q=`) stays as-is for direct name/CVR lookups; no priority investment in expanding its query-parsing.

---

## 1. Developer API as a product — PRIORITIZED

Reuses existing `/v1/establishments/search`, `/nearby`, `/{cvr}/history`, and the existing Free/Pro key-tier system. Effort is mostly packaging: OpenAPI spec, docs, example requests, positioning against the raw XML dump as the alternative. Marketplace listing (RapidAPI-style) is a good secondary distribution channel for this once the core docs/positioning exist.

## 2. Business analytics/SaaS tooling — PRIORITIZED

The real value proposition for the paid Business tier, since visibility alone is already free via findsmiley.dk. Concrete ideas, informed by the Ecolab HDI reference above: score trend charts over time, benchmarking against nearby/similar establishments, a multi-location dashboard for chains (using the existing multi-CVR Pro support), a widget with brand customization instead of the standard government styling. Depends on Phase G (tier field) and Phase I (session-based Pro webhooks) from the existing roadmap to fully build out.

## 3. Consumer-facing directory site ("Smilr Finder") — SECONDARY, SEO/acquisition role

See URL structure and category hub sections above (in progress/decided). Each page needs differentiated content beyond a plain score mirror to be worth ranking. "Claim this listing" CTA still funnels into Business registration. Chrome extension idea remains a later, optional addition.

**Next differentiator to build, once category hub pages land — "Recently changed" / "Most improved" trend feed:**
Low-effort, high-differentiation: the webhook system already detects `smiley_score_changed` events (old score vs. new score) via SQL MERGE OUTPUT during each sync — this is the exact data needed, already captured, just not yet exposed as a public read surface. Concrete pages:
- `/find/{area-slug}/changes` — live feed of establishments whose score moved in the latest sync, filterable by area.
- "Most improved" / "recently downgraded" leaderboards — same data, framed as a ranked list. Shareable/link-bait potential.
- Area health snapshot — aggregate stats per area ("87% of restaurants in Nørrebro currently have a green smiley").

All three reuse the same trend-data capability that also feeds the Business analytics dashboard (#2) — one data layer, two audiences.

## 4. API marketplace listing (e.g. RapidAPI) — SECONDARY

Packaging work on top of #1. Distribution channel for developer audience, not a subscription driver directly.

---

## Competitive note: findsmiley.dk (confirmed 2026-08-18)

- QR code posted at ~46,000 business entrances since Dec 2023, linking directly to that business's latest inspection report on findsmiley.dk.
- findsmiley.dk establishment pages already offer: a copyable embed link and an email subscribe field — a free, lightweight version of Smilr's widget + alert features.
- "Hent Smiley-data" offers raw data download (XML), not a polished/documented developer API.
- findsmiley.dk has no trend/change feed, no leaderboard, no area aggregate stats, and no category browsing — static per-establishment lookup only. This is the gap both the category hub pages and the "Recently changed" feed are designed to exploit.

---

## Relationship to existing roadmap (Phases G–I)

Phase G (registered widget tier field) and Phase I (session-based Pro webhooks) directly support the analytics/SaaS positioning (#2) — prioritize these over Phase H polish if forced to choose. Phase H (static pages) still needed as the registration landing destination for both the API signup and the directory's claim-listing CTA.

**Open decisions:**
- Exact set of analytics features for the Business dashboard (trend charts, benchmarking, multi-location view — prioritize which ships first)
- Minimum-establishment-count threshold for generating/indexing an area × category hub page (see Category hub pages section)
- Caching/infra approach to keep public anonymous directory traffic from hitting Azure SQL directly at scale

**Resolved:**
- Area-slug taxonomy: City name only (not postcode), raw/unnormalized, grouped by identical slugified text (2026-08-19)
- Category taxonomy: 26 Pixibranche-derived categories, no cuisine-level granularity possible from official data (2026-08-19)
- URL structure and routing-disambiguation pattern for both area/detail and category/detail conflicts (2026-08-19)
- Search UX: no on-site query-parsing investment; category/area hub pages carry that job via SEO instead (2026-08-19)

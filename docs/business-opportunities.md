# Smilr — Business Opportunities & Positioning

> **Before editing this file:** it's actively updated from two places — Claude Code (local CLI) and a Cowork session in the Claude desktop app — often around the same time. Re-read the current on-disk content before writing; merge your changes into it rather than overwriting wholesale.

Ideas for growing Smilr beyond the core widget. Captured 2026-08-11, revised 2026-08-18, 2026-08-19, 2026-09-20.

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

**What actually works, and is now the standard pattern for this kind of ambiguity in this codebase:** merge the conflicting routes into one (`/find/{segment}`) and disambiguate at runtime by whether the segment matches the `{business-slug}-{navnelbnr}` shape (ends in `-{digits}`, via regex). If it matches, it's a detail page; otherwise it's treated as an area slug. This is the pattern reused for category hub pages below.

CVR is no longer part of any `/find` URL. It survives only as a `/find/search?q={8-digit-cvr}` convenience shortcut (people know their CVR, not their Navnelbnr) that resolves and 301-redirects to the real canonical detail path. An optional chain hub page (`/kaede/{cvr}/`, listing all locations under one CVR) remains a possible future addition, not yet built.

This shipped before broad indexing, so no legacy-URL redirect layer was needed.

---

## Category hub pages — decision and implementation (2026-08-19)

**Data constraint found first:** there is no cuisine-level data anywhere in the Fødevarestyrelsen source (checked both `Pixibranche` and the more granular `branche` field directly against the XML). The largest category, `Restauranter, pizzeriaer, kantiner m.m.`, lumps restaurants, pizzerias, and canteens into one bucket of 23,200 establishments (~40% of the dataset) — there is no field distinguishing a pizza place from a sushi place. A `/find/{area-slug}/pizza`-style page is **not buildable from official data** without either guessing from business names (unreliable) or pulling in a second, non-government data source — which would undercut Smilr's "100% public, officially-sourced, no scraping risk" positioning. Cuisine-level search/browsing is out of scope unless that tradeoff is deliberately revisited later.

**What the data does support — 26 real Pixibranche-derived categories**, e.g.:
- Restauranter, pizzeriaer, kantiner m.m. — 23,200
- Dagligvarer (grocery stores) — 11,554
- Hospitals- og institutionskøkkener (institutional kitchens) — 8,808
- Delikatesse og smørrebrød (deli) — 1,485
- Bagere og bagerafdelinger (bakeries) — 1,167
- Slagtere, slagterafdelinger (butchers) — 968
- Fiske- og vildtforretninger (fishmongers) — 343
- (full list of 26 in `docs/Smiley_xml.xml` — re-derived per sync, not hardcoded — see below)

**Route:** `/find/{area-slug}/{category-slug}` — a listing/hub page only, filtered by area + Pixibranche category.

**Decided: no nested detail URL.** `/find/{area-slug}/{category-slug}/{business-slug}-{navnelbnr}` does not exist. The category hub links out to the existing flat canonical detail page (`/find/{area-slug}/{business-slug}-{navnelbnr}`) — it never hosts its own copy of the establishment page. Reasoning:
- **Identity vs. classification.** A URL should encode "which business, where" (stable identity), not "which browsing path was used to find it." Category is metadata about a business, not part of what makes it that business.
- **Category is measurably less stable than City.** The source data includes transitional/unresolved values (`Virksomheder, detail-branche endnu ikke tildelt` — "not yet assigned"; ownership-change states) — real evidence that category reclassification happens. Baking it into the canonical path would mean every reclassification breaks the business's permanent URL, stacking a second, more volatile source of redirect churn on top of the one already accepted for City.
- **Future-proofing.** Every establishment has exactly one `Pixibranche` value today, but if the taxonomy is ever split finer (or a second dimension like price tier is added later), a flat canonical URL doesn't care how many ways a business can be classified — each new dimension just becomes another hub page linking to the same one URL, rather than requiring a redesign.
- **Keeps canonical-redirect logic simple.** `DetailHandlerAsync`'s canonical check only has to reason about City + Name today; making category part of the canonical path would add Pixibranche as a second, independent trigger for that same redirect logic.

Category context (breadcrumb: Area → Category → Business Name) appears in the UI and in `BreadcrumbList` structured data on the detail page for SEO rich-snippet purposes — a presentation/schema concern, not a URL concern.

**Routing implementation (shipped):** `/find/{area-slug}/{business-slug}-{navnelbnr}` and `/find/{area-slug}/{category-slug}` are the same two-segment route shape — the same `AmbiguousMatchException` risk already hit once at the one-segment level. Fix: merged into one route, `GET /find/{areaSlug}/{segment}`, reusing the proven disambiguation pattern — if `segment` matches the `-{digits}` suffix shape, it's a detail page; else it's looked up against a live category-slug index (`GetCategoryCountsAsync` → `FindEndpoints.GetCategoryIndexAsync`, cached 12h) built fresh from the DB every cache cycle, never hardcoded — so it self-corrects if Fødevarestyrelsen adds/renames/retires a Pixibranche value; an unrecognized segment 404s.

**Minimum-establishment-count guard — resolved and implemented:** render whenever count ≥ 1 (still useful to a direct visitor); add `<meta name="robots" content="noindex,follow">` and exclude from `sitemap.xml` when count < 3; 404 only when count = 0 (`CategorySlugThreshold` in `FindEndpoints.cs`). Verified against real data: a thin combo (count 2) renders with `noindex` and is absent from the sitemap; a combo ≥ 3 is indexed and included; a genuine zero-count combo 404s.

Also implemented: the detail page's Category breadcrumb segment + `BreadcrumbList` JSON-LD (Find → Area → Category → Name), and an area hub "Browse by category" nav section. **Category hub pages are fully shipped** — the directory now supports area hubs, area×category hubs, and canonical establishment detail pages.

---

## Search UX — decision (2026-08-19)

Explored whether the on-site search box should be restructured (e.g. a "what" + "where" two-field split, or a name/address split like Scores on the Doors uses) to support queries like "pizza in Aalborg." **Decided against redesigning the search box** — instead, lean on the category + area hub pages above to do that job via SEO: Google sends people directly to `/find/{area-slug}/{category-slug}` for a query like "restaurants in Aalborg" (noting the cuisine-level limitation above — "pizza" specifically isn't achievable), rather than requiring the on-site search box to parse compound queries. The existing basic search (`/find/search?q=`) stays as-is for direct name/CVR lookups; no priority investment in expanding its query-parsing.

---

## 1. Developer API as a product — PRIORITIZED

Reuses existing `/v1/establishments/search`, `/nearby`, `/{cvr}/history`, and the existing Free/Pro key-tier system. Effort is mostly packaging: OpenAPI spec, docs, example requests, positioning against the raw XML dump as the alternative. Marketplace listing (RapidAPI-style) is a good secondary distribution channel for this once the core docs/positioning exist.

## 2. Business analytics/SaaS tooling — PRIORITIZED

The real value proposition for the paid Business tier, since visibility alone is already free via findsmiley.dk. Concrete ideas, informed by the Ecolab HDI reference above: score trend charts over time, benchmarking against nearby/similar establishments, a multi-location dashboard for chains (using the existing multi-CVR Pro support), a widget with brand customization instead of the standard government styling. Depends on Phase G (tier field) and Phase I (session-based Pro webhooks) from the existing roadmap to fully build out.

**Keep this scoped to monitoring/benchmarking the published result — see eSmiley competitive note below.** Do not drift into internal compliance/HACCP tooling (self-inspection checklists, audit prep, staff training) — that's a different, already-owned lane.

**Dashboard structure (decided 2026-09-20, built on branch `business-overview-dashboard`):** the single tabbed `dashboard.html` (every load fetched the API key plus every inspection row for every location) is split into separate pages linked from a shared `<dash-nav>` menu (`components/nav.js`), each loading only its own data:
- **`/overview.html` — the post-login landing page.** One light call, `GET /v1/business/overview`, computed in SQL: portfolio summary (location count, CVR count, average score, score distribution) plus **recent score changes** (last 90 days, this business's locations only) and a **needs-attention** panel (see Risk level below). Login, Stripe checkout/portal return and claim-listing redirects all land here; `?claim_cvr=` / `?upgraded=1` handling lives here.
- **`/locations.html`** — the old Locations tab: add/remove by CVR, group by CVR, embed code, history. Now server-paged and server-searched (`GET /v1/business/locations?page&pageSize&q&sort`), so a chain with hundreds of locations no longer ships every inspection row to the browser.
- **`/developer-api.html`** — the old Developer API tab; the key is only fetched here. Pro/Enterprise only (plus grandfathered Free accounts that already hold a key), gated via `canUseDeveloperApi` on `/v1/business/me`.
- `/dashboard.html` remains as a thin redirect to the overview so old bookmarks and emailed links keep working.

**Benchmarking (decided 2026-09-20, Pro/Enterprise only, built on branch `business-benchmarking`):** "how do my locations compare with similar establishments", the analytics feature findsmiley.dk and eSmiley don't offer. It is loaded lazily (`GET /v1/business/benchmark`, and `GET /v1/business/locations/{navnelbnr}/benchmark` for one location) so the Overview stays one light call; both return 403 `pro_required` for Free accounts, which see a "part of the Pro plan" note with no checkout button (the Free upgrade CTA is deliberately removed — see `removed-features-bring-back-list.md` §9).
- **Peer group:** same `Pixibranche` category in the same `City` (scored, non-delisted, excluding the location itself). Fewer than **10** peers falls back to the same category **nationwide**, labelled as such; locations with no score, City or real category are excluded and reported ("N of M benchmarked").
- **How it's reported:** scores are discrete (1–4) and ~90% of establishments hold a 1, so a single percentile is misleading ("tied with 90% of peers"). The comparison instead shows the share of peers with the **same**, a **better** and a **worse** score, plus the peer average and the peers' share with score 1. The Overview rolls this up (your average vs. peers', your share with score 1 vs. peers', and how many locations score better than / in line with / worse than their peer average, with a 0.05 in-line tolerance) and lists the locations furthest below their peers; the Locations page has a per-location "Compare" modal.
- Logic lives in `BenchmarkCalculator` (Core, unit-tested); peer distributions come from `GetPeerScoreDistributionsAsync` / `GetNationalCategoryDistributionsAsync` on `EstablishmentRepository` (the nationwide one is cached for 12h).

**Risk level and "Needs attention" (decided 2026-09-20, built on branch `overview-needs-attention`):** the panel used to list only locations whose *current* score was 3 or 4, so it was blank for most accounts even while "Recent score changes" showed a decline. One shared definition, `RiskCalculator` (Core, unit-tested), now drives both the Overview panel and the Locations tab (risk badges, "Risk: most concerning first" sort, a "Needs attention" filter that the Overview's "+N more" link opens):
- **AtRisk** = current score 3 or 4, **or** the two most recent score changes are both downgrades and both within the last 90 days. **Watch** = the location's *most recent* score change is a downgrade within the last 90 days (and not AtRisk). **Ok** = otherwise. A downgrade is a move to a higher score number (1 = best).
- **The panel lists a location if any of:** current score 3/4; its latest score change is a downgrade within 90 days (any decline, e.g. 1→2 — the main gap in the old rule; see the recovery note below); or no score yet ("awaiting first inspection"). No score yet is a *reason*, not a risk level (there is nothing to judge). Delisted establishments are never flagged.
- **Display:** sorted 3/4 first (4 before 3), then recent decline (newest first), then no score yet; capped at 5 with "+N more →" to `/locations.html?attention=1&sort=risk`; every row carries reason tags ("Score 4", "Declined 18/09", "Declined twice in 90 days", "Awaiting first inspection"). With nothing to flag it shows a positive line ("Nothing needs attention — all N locations are current. Last change: …") built from the same data as "Recent score changes", never blank space.
- **Deliberately not built: "overdue for reinspection".** Fødevarestyrelsen's inspection cadence per category isn't in the data, so calling something "overdue" would be a guess. Revisit only if a real cadence source turns up.
- **A location that declined and has since recovered is not flagged** (revised 2026-09-20, after a location that went 1 → 2 on 29/06 and back to 1 on 27/08 kept showing "Declined 29/06" with a current score of 1). Only the *latest* score change counts, so the panel shows locations whose latest change was a decline from a better score. Same-score re-inspections don't change that: a 1 → 2 decline stays flagged, while nothing newer moves the score, until it ages out of the 90 days.

Staged after this: **score trend charts** (the one remaining analytics idea above). Not in the overview yet.

## 3. Consumer-facing directory site ("Smilr Finder") — SECONDARY, SEO/acquisition role

See URL structure and category hub sections above — both implemented and shipped. Each page needs differentiated content beyond a plain score mirror to be worth ranking. "Claim this listing" CTA still funnels into Business registration. Chrome extension idea remains a later, optional addition.

**Next differentiator to build, now that category hub pages have landed — "Recently changed" / "Most improved" trend feed:**
Low-effort, high-differentiation: the webhook system already detects `smiley_score_changed` events (old score vs. new score) via SQL MERGE OUTPUT during each sync — this is the exact data needed, already captured, just not yet exposed as a public read surface. Concrete pages:
- `/find/{area-slug}/changes` — live feed of establishments whose score moved in the latest sync, filterable by area.
- "Most improved" / "recently downgraded" leaderboards — same data, framed as a ranked list. Shareable/link-bait potential.
- Area health snapshot — aggregate stats per area ("87% of restaurants in Nørrebro currently have a green smiley").

All three reuse the same trend-data capability that also feeds the Business analytics dashboard (#2) — one data layer, two audiences.

**Design spec received (2026-08-19):** a detailed spec + mockups for a related but distinct page, `/find/{area-slug}/recently-inspected` (ordered by latest inspection date, not score change), found in `docs/Design/Recently Inspected/`. Recommends building this before the score-change feed above, since it needs no dependency on the webhook change-detection logic (just `ORDER BY LatestInspectionDate DESC`) while validating the same routing/caching/structured-data architecture the `changes` page will reuse. Extends the reserved-segment routing pattern with `recently-inspected` and `changes` as literal segments checked before category-slug matching (same shape as the shipped category disambiguation above). **Shipped 2026-08-22** (PR #1) — see the "Find & homepage" work; the "not yet implemented" state this paragraph used to describe is out of date.

**Design spec drafted for `/find/{area-slug}/changes` (2026-08-22):** written for Claude Design to mock up, found in `docs/Design/Changes/`. Follows the same section structure and depth as the `recently-inspected` spec above, adapted for score-change semantics. Key points not shared with `recently-inspected`:
- A business appears **only** if its score changed between two consecutive inspections — a first-ever inspection or a same-score re-inspection does not qualify (that's `recently-inspected` territory).
- **Real data dependency found while writing the spec, worth flagging before estimating effort:** `recently-inspected` only needs `LatestInspectionDate`, a property of current state. `changes` needs the *previous* score too, not just the new one. The webhook system's `MERGE OUTPUT` diff already computes old-vs-new, but likely only as a transient payload for the webhook dispatcher — confirm whether it's persisted anywhere queryable. If not, this page depends on adding a small persisted `ScoreChangeLog` (Navnelbnr, PreviousScore, NewScore, ChangeDate), populated by the same `MERGE OUTPUT` step that already fires the webhook. Small addition, but a real prerequisite, not just a query-writing task.
- Uses a rolling 90-day window (not "most recent N ever") so a city doesn't keep showing one stale change indefinitely.
- Flags a tone consideration `recently-inspected` didn't need: "downgraded" is the closest thing on the site to public bad news about a named business — spec calls for neutral, non-ranked, non-leaderboard framing (no "worst restaurants"), with "most improved"/"recently downgraded" rankings explicitly deferred as separate future pages needing their own tone decision.
- **Shipped 2026-08-22**, and the `ScoreChangeLog` prerequisite above turned out not to be needed: score transitions are derived on the fly from the `Inspections` table with a SQL `LAG(SmileyScore) OVER (PARTITION BY EstablishmentId ORDER BY InspectedOn)` window (`EstablishmentRepository.BuildCurrentTransitionsCte`, semantics documented in `ScoreChangeCalculator`). The same query, scoped to one business's locations, now also feeds the Business overview's "recent changes" list (2026-09-20).

## 4. API marketplace listing (e.g. RapidAPI) — SECONDARY

Packaging work on top of #1. Distribution channel for developer audience, not a subscription driver directly.

---

## Competitive note: findsmiley.dk (confirmed 2026-08-18)

- QR code posted at ~46,000 business entrances since Dec 2023, linking directly to that business's latest inspection report on findsmiley.dk.
- findsmiley.dk establishment pages already offer: a copyable embed link and an email subscribe field — a free, lightweight version of Smilr's widget + alert features.
- "Hent Smiley-data" offers raw data download (XML), not a polished/documented developer API.
- findsmiley.dk has no trend/change feed, no leaderboard, no area aggregate stats, and no category browsing — static per-establishment lookup only. This is the gap both the category hub pages and the "Recently changed" feed are designed to exploit.

## Competitive note: eSmiley (confirmed 2026-08-19)

[eSmiley](https://www.esmiley.dk/) is a large, established Danish/Nordic SaaS company — 17,000+ kitchens, enterprise clients including ISS, Danish Crown, and Coop, subscription/quote-based pricing. Sells digital self-inspection and compliance tooling: HACCP checklists, automatic temperature monitoring, cleaning schedules, food-waste tracking, staff training, and consultant guidance. Markets a "Smiley Garanti" — explicitly positioned around helping kitchens achieve a good smiley score.

**Key distinction from Smilr:** eSmiley is preventive/internal — it's the paperwork and process *before* an inspection, aimed at making sure a kitchen passes. It does not display, track, or publish the *official* smiley score itself. Smilr's territory (the widget, the API, the directory, the Business analytics tier) is entirely about the *published result* — after the fact, public-facing, tracked and benchmarked over time. Different points in the same lifecycle, same customer base, similar "smiley" branding, but not the same job — a restaurant could plausibly use both.

**Why this matters for positioning:** eSmiley's scale confirms Danish food businesses genuinely pay for smiley-related tooling (bullish for the Business tier thesis generally), but it also means "help you get a good smiley" is already owned by a well-resourced incumbent with real enterprise relationships. Smilr's Business analytics tier (#2 above) should stay scoped to monitoring/benchmarking the published result, not drift into internal compliance/HACCP territory — that's eSmiley's defended lane, not an open one.

---

## Relationship to existing roadmap (Phases G–I)

Phase G (registered widget tier field) and Phase I (session-based Pro webhooks) directly support the analytics/SaaS positioning (#2) — prioritize these over Phase H polish if forced to choose. Phase H (static pages) still needed as the registration landing destination for both the API signup and the directory's claim-listing CTA.

**Open decisions:**
- Whether/when to build score trend charts (the multi-location view, portfolio summary/changes and benchmarking are done — see Dashboard structure in §2)
- Caching/infra approach to keep public anonymous directory traffic from hitting Azure SQL directly at scale

**Resolved:**
- Area-slug taxonomy: City name only (not postcode), raw/unnormalized, grouped by identical slugified text (2026-08-19)
- Category taxonomy: 26 Pixibranche-derived categories, no cuisine-level granularity possible from official data (2026-08-19)
- URL structure and routing-disambiguation pattern for both area/detail and category/detail conflicts (2026-08-19)
- Search UX: no on-site query-parsing investment; category/area hub pages carry that job via SEO instead (2026-08-19)
- Minimum-establishment-count guard for area × category hub pages: threshold 3, render/noindex/404 tiering (2026-08-19)
- Business analytics tier scope: monitoring/benchmarking the published result only, not internal compliance/HACCP tooling (2026-08-19)
- Adoption/sequencing of `recently-inspected` and `changes`: both built and shipped (2026-08-22); no persisted `ScoreChangeLog` needed — changes are derived from `Inspections` via `LAG` (2026-08-22)
- Business dashboard restructure: overview landing page (portfolio summary + recent changes + needs-attention) with separate Locations and Developer API pages and a shared nav; trend charts and benchmarking staged after it (2026-09-20)
- Next analytics feature: benchmarking (peer group = same category + city, nationwide fallback under 10 peers; same/better/worse shares rather than a percentile; Pro/Enterprise only) chosen ahead of trend charts (2026-09-20)
- "Needs attention" redesign and shared RiskLevel: AtRisk = score 3/4 or two consecutive downgrades both within 90 days; Watch = latest score change is a downgrade within 90 days (a decline the location has since recovered from doesn't count — revised 2026-09-20); panel also lists locations with no score yet; "overdue for reinspection" dropped for lack of cadence data (2026-09-20)

**Shipped (2026-08-19):** area hub pages, area × category hub pages, canonical establishment detail pages with Area/Category breadcrumbs and `BreadcrumbList` JSON-LD, and `sitemap.xml` covering all three page types. Both follow-ups from that sequencing — the "Recently inspected" and "Recently changed" pages (§3) — shipped 2026-08-22.

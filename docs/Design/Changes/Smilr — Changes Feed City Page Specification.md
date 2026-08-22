# Smilr — `/find/{city}/changes`

## 1. Purpose

Create a public, indexable SEO page showing food businesses in a specific city whose official inspection **score changed** at the most recent sync.

Example:

`/find/silkeborg/changes`

Primary objectives:

1. Capture organic searches related to smiley score changes, upgrades, and downgrades in a city.
2. Surface a genuinely new signal Google (and findsmiley.dk) does not publish anywhere: a change feed, not just a current-state lookup.
3. Create a continuously changing page that Google can revisit — arguably the single freshest page type on the whole site, since it is empty of "no news" and only ever shows real movement.
4. Drive visitors into existing canonical establishment detail pages.
5. Increase discovery of Smilr, and specifically demonstrate the trend-tracking capability the Business tier sells.
6. Create another internal-linking layer between city pages, the recently-inspected page, and establishment pages.
7. Eventually provide a natural conversion path into Smilr Business ("get notified the moment *your* score changes" — this page is the public proof that Smilr already tracks that).

This should be a **public feature** and should not require authentication.

**A business appears on this page only if its score changed.** An establishment that was re-inspected and received the *same* score again does not belong here — that is `/find/{city}/recently-inspected` territory, not this page. This is the one rule that defines the entire feature; every other section below exists to support it correctly.

---

# 2. URL

Use:

`/find/{area-slug}/changes`

Examples:

- `/find/silkeborg/changes`
- `/find/aarhus/changes`
- `/find/odense/changes`
- `/find/aalborg/changes`

Reuse the existing city slug generation exactly as `/find/{area-slug}/` and `/find/{area-slug}/recently-inspected` already do.

Do not introduce a new city taxonomy, and do not add a separate slug for "changes" — it is a reserved literal segment under the existing area path (see §29).

---

# 3. What does "changed" mean?

A change is a **transition between two consecutive recorded scores for the same establishment**, where the new score differs from the previous one.

Example:

| Business | Previous score | New score | Change date |
|---|---|---|---:|
| Restaurant A | 2 (yellow) | 1 (green) | 18 Aug 2026 |
| Restaurant B | 1 (green) | 2 (yellow) | 17 Aug 2026 |
| Restaurant C | 2 (yellow) | 3 (red) | 15 Aug 2026 |

**Explicitly not a change:**

- An establishment's **first-ever recorded inspection**. There is no "previous score" to compare against, so it cannot have "changed" — it can only be new. Do not show first inspections here; that's what `/find/{city}/recently-inspected` is for.
- A re-inspection that produced the **same score again**. Same score in, same score out is not a change, no matter how recent the inspection.
- A change to any field other than the smiley score itself (address correction, name update, category reclassification). This page is specifically about the regulatory score, not general record edits.

The page must show, for every entry:

> **Previous score → New score**, and whether that transition was an **improvement** or a **downgrade**.

This distinction (previous vs. new, and direction) must be preserved in the UI, the metadata, and any structured data — it's the entire reason this page is different from a plain list.

---

# 4. Recommended page title

Dynamic:

`Recent Smiley Score Changes in Silkeborg | Smilr`

For Aarhus:

`Recent Smiley Score Changes in Aarhus | Smilr`

Avoid "best" / "worst" language in the title. This is a factual change feed, not a ranking.

---

# 5. Meta description

Dynamic example:

> See which food businesses in Silkeborg recently had their official smiley score change — upgraded or downgraded — based on the latest Fødevarestyrelsen inspection data.

For Aarhus, replace the city.

Keep the wording neutral. Do not use "improved" as a value judgment about the business overall, and do not frame downgrades as call-outs — see §25 on tone.

---

# 6. Canonical URL

Canonical should be:

`https://smilrhq.dk/find/silkeborg/changes`

or whatever production canonical domain is currently in use.

Use the same canonical URL regardless of query parameters, e.g. `?page=2` canonicalizes per the pagination strategy in §13 — same approach already used on `/recently-inspected`.

---

# 7. Page heading

One clear H1:

# Recent smiley score changes in Silkeborg

Under it:

> See the food businesses in Silkeborg whose official inspection score recently changed.

Then:

> Updated from the latest available Fødevarestyrelsen data.

No large generic SEO paragraph. The data itself is the content — same principle as `/recently-inspected`.

---

# 8. Summary section

Immediately below the H1, a small factual summary.

Example:

### Silkeborg

**6 score changes in the latest sync**

**4 improved · 2 downgraded**

**Most recent change:** 18 August 2026

Only show statistics that can be calculated reliably from existing data. Do not invent estimates, and do not compute a "net sentiment" score or anything beyond a plain improved/downgraded count.

---

# 9. Main listing

A list of establishments whose score changed, most recent change first.

Recommended initial page size: **30**. In practice most cities will have far fewer than 30 changes in any given window (see §13 on what happens when a city has very few), so this cap matters less here than on `/recently-inspected` — but keep it for consistency and to bound worst-case page size for larger cities during a heavy resync.

Example:

### Recent changes

**1. Restaurant A**

Silkeborg · Restaurant

**Changed:** 18 August 2026

🟡 Smiley 2 → 🟢 **Smiley 1** — Improved

[View inspection history →]

---

**2. Restaurant B**

Silkeborg · Bakery

**Changed:** 17 August 2026

🟢 Smiley 1 → 🟡 **Smiley 2** — Downgraded

[View inspection history →]

---

The entire card links to the canonical establishment URL, e.g. `/find/silkeborg/restaurant-a-1449060`.

Do not create a second establishment URL under `/changes`.

---

# 10. Information displayed for every establishment

At minimum:

### Business name

Current official establishment name.

### Category

Existing `Pixibranche` category, same as `/recently-inspected`.

### Change date

The date the new score was recorded. Absolute date (`18 August 2026`), never relative ("2 days ago") — same reasoning as `/recently-inspected`: better for SEO, more useful to users, and unambiguous when a sync runs late or is delayed a day.

### Previous score → New score

Show both values, not just the new one. This is the one piece of information unique to this page — losing it would make this indistinguishable from `/recently-inspected`.

### Direction

Improved or downgraded, derived purely from whether the new score number is lower (better) or higher (worse) than the previous one. Represent with color/icon (e.g. ▲ green for improved, ▼ red for downgraded) in addition to the text label — do not rely on color alone (accessibility).

### Link

Link to the existing canonical establishment page, e.g. `/find/silkeborg/restaurant-a-1449060`.

---

# 11. Sort order

Primary sort:

```text
ChangeDate DESC
```

Secondary deterministic sort:

```text
BusinessName ASC
```

Same reasoning as `/recently-inspected`: if multiple establishments changed on the same date, the result must stay stable between requests. Do not sort by magnitude of change, direction, or anything else as the primary key — recency is the entire premise of the page.

---

# 12. What if two changes happened on the same date?

No special treatment necessary — sort alphabetically by establishment name after change date, same pattern as `/recently-inspected` §12.

---

# 13. Pagination

30 per page, using `/find/silkeborg/changes?page=2`.

Because change events are inherently less frequent than inspections (most establishments go months between score changes), expect most cities to have a single page, often a short one. Do not pad the page with unrelated content to make it look fuller — a short, honest list is fine; see §14 for what to do when it's very short or empty.

Same indexing approach as `/recently-inspected`:

- page 1 = indexable (subject to the threshold in §15)
- page 2+ = `noindex,follow`, still crawlable

---

# 14. Empty state

**Unknown/invalid city slug → 404**, same as every other `/find/{area-slug}/...` page.

**Known city, but zero score changes in the current window (see §15 for what "window" means) →** do not render an empty SEO page as if it were indexable content. Show a useful, honest state:

> No smiley score changes have been recorded recently for food businesses in Silkeborg.

and mark the page `noindex,follow` in that state (there is nothing here yet for Google to index, but the page should still be reachable and link out normally). This differs from `/recently-inspected`, where a city can always show *something* as long as it has any inspections at all — a `/changes` page can legitimately be empty for a perfectly normal reason (nothing changed), so the empty state must read as normal, not broken.

---

# 15. What time window counts as "recent"?

This needs to be decided explicitly and applied consistently — unlike `/recently-inspected`, where "most recent N" doesn't need a window because it always has a natural cutoff (however many establishments exist).

Recommended: a **rolling 90-day window** — establishments whose score changed within the last 90 days, most recent first, capped at 30 per page as above.

Why a window at all, rather than "all changes ever, most recent 30": without one, a city with very few establishments and a very old last change (e.g. a single change 14 months ago) would keep showing that one stale entry indefinitely, which looks broken and stops being "recent" in any meaningful sense.

If a city has changes within the window, show them (down to 1). If a city has zero changes within the window, use the empty state in §14 rather than reaching back further to force a non-empty page.

---

# 16. Small-city / low-volume handling

Do not require a minimum of 3 changes to render the page at all — a single genuine change is real, useful content.

A practical rule, adapted from the same philosophy already implemented for area × category pages and `/recently-inspected`:

### 0 changes in the window

Render the empty state from §14. `noindex,follow`.

### 1–2 changes

Render the page. `noindex,follow`.

### 3+ changes

`index,follow`.

This mirrors the existing `CategorySlugThreshold` pattern — reuse that same threshold constant/logic rather than inventing a second one.

---

# 17. Don't only show the list

After the main list, add:

# Explore food inspection scores in Silkeborg

Links:

- All food businesses in Silkeborg
- Recently inspected in Silkeborg
- Restaurants, pizzerias and canteens in Silkeborg
- Bakeries in Silkeborg
- Grocery stores in Silkeborg
- Butchers in Silkeborg

These link to the existing area, area×category, and recently-inspected pages.

---

# 18. Cross-links with `/recently-inspected`

Add, below the main list:

## Recently inspected in Silkeborg

> See all food businesses in Silkeborg with a recent inspection, whether or not their score changed.

[View recently inspected →]

And on `/find/silkeborg/recently-inspected`, per its own spec §17, add the reverse:

## Recently changed in Silkeborg

> See businesses whose inspection score recently changed.

[View recently changed →]

This is a two-way link that should ship in the same release as this page, even though `/recently-inspected` shipped first — it's currently a dead link placeholder on that page waiting for `/changes` to exist.

---

# 19. Add links from the city page

`/find/silkeborg` should link to both:

> **Recently inspected in Silkeborg →**
>
> **Recent score changes in Silkeborg →**

The changes page should link back:

> **View all food businesses in Silkeborg →**

Simple hub-and-spoke structure, same as `/recently-inspected`.

---

# 20. Add breadcrumb

Use:

**Find → Silkeborg → Changes**

Structured data: `BreadcrumbList`.

```text
Find
  >
Silkeborg
  >
Changes
```

---

# 21. Structured data

At minimum: `BreadcrumbList`, matching the existing detail-page and `/recently-inspected` implementation.

`ItemList` for the result list is optional and only worth adding if it can accurately represent the visible list without misrepresenting a score change as a review or rating.

**Do not use `Review`, `AggregateRating`, or any schema.org rating vocabulary anywhere on this page.** This applies with extra force here versus `/recently-inspected`: an "improved/downgraded" framing sits closer to review-shaped language than a plain inspection date does, and it would be easy to accidentally imply a consumer rating where there is only a regulatory score change. The official inspection score is not a customer review, regardless of which direction it moved.

---

# 22. Internal links are extremely important

Every establishment shown links to its canonical detail page.

```text
/find/silkeborg/changes
        |
        +-- Restaurant A  (2 → 1, improved)
        |     |
        |     +-- /find/silkeborg/restaurant-a-1449060
        |
        +-- Restaurant B  (1 → 2, downgraded)
              |
              +-- /find/silkeborg/restaurant-b-123456
```

Same crawl-path value as `/recently-inspected` §21.

---

# 23. Link from the establishment page back to it

On the canonical detail page, add a contextual link only when relevant:

> **This establishment's score recently changed — see all recent changes in Silkeborg →**

Show this only if the establishment's own most recent score change falls within the same window used by §15 (i.e., only link to the changes page from a business that would actually appear on it right now). Don't show it unconditionally the way the `/recently-inspected` link can be, since most establishments most of the time will have no recent change.

---

# 24. Show the change directly on the establishment page

Independent of whether the establishment currently qualifies for §23, the detail page should already show its own score history (existing feature). Where the most recent entry represents a change, make the direction explicit there too:

> **Latest inspection:** 18 August 2026
>
> **Smiley 2 → Smiley 1** — Improved since the previous inspection (12 May 2026)

This is the same underlying data as the changes page, just scoped to one establishment — worth double-checking the existing history view already renders it this way; if not, this is a good small addition to make alongside building the changes page.

---

# 25. Tone: this page needs more care than `/recently-inspected`

`/recently-inspected` is purely neutral — a date is a date. This page inherently contains "downgraded," which is the closest thing on the whole site to bad news about a specific, named small business. Get the tone wrong and this reads as a public shaming feed instead of a factual regulatory record — bad for the businesses involved, and bad for Smilr's credibility with the Business tier it's trying to sell to those same owners.

Guidelines:

- **No ranking, no leaderboard, no "worst restaurants in Silkeborg" framing on this page.** A "most downgraded" leaderboard is a distinct, separate idea (already noted as a future extension in the business-opportunities roadmap) — if it's ever built, it deserves its own explicit tone decision, not an implicit one inherited from this page.
- **Present improvements and downgrades in the same neutral list, in the same visual weight**, differentiated only by a color/icon and the word "improved"/"downgraded" — not by making downgrades bigger, first, or more prominent.
- **Use "downgraded," not "failed," "flagged," or "worst."** Downgraded is accurate and neutral; the others editorialize.
- Do not add any language implying health risk, cleanliness judgment, or advice ("avoid this restaurant"). State only what the regulator's own score records.
- Avoid "best restaurants," "restaurant ratings," or "restaurant reviews" language generally — same reasoning as `/recently-inspected` §25: the dataset is broader than restaurants, and a smiley score is a regulatory inspection result, not a consumer rating.

---

# 26. SEO content should be generated from real data

Avoid a generic paragraph on every city page. Generate a small factual introduction instead:

> **Recent smiley score changes in Silkeborg**
>
> Smilr tracks changes to official food inspection scores in Silkeborg. This page lists food businesses whose smiley score recently changed, most recent first, together with the previous score, the new score, and a link to their full inspection history.

Then dynamically:

> The most recent change in this list was recorded on 18 August 2026.

That's enough — the data itself creates uniqueness, same as `/recently-inspected`.

---

# 27. Page freshness

This is arguably the freshest page type on the site — more so than `/recently-inspected`, which always shows *something* even in a quiet week. A `/changes` page genuinely has nothing to show until a real change occurs, which makes every non-empty appearance on it meaningfully new content, not just a re-sorted list.

Do not fake an "Updated" timestamp unless the underlying data actually changed. Separately show:

> Data synchronized: 19 August 2026

as the genuine sync timestamp, same as `/recently-inspected` §27.

---

# 28. Sitemap

Include indexable `/changes` city pages in `sitemap.xml`:

```text
/find/silkeborg/changes
/find/aarhus/changes
/find/odense/changes
```

Only include pages meeting the indexability threshold in §16. Do not put `noindex` pages (including the empty-state page from §14) into the sitemap.

Because a city can flip between "3+ changes, indexed" and "0 changes, noindex" from one sync to the next, the sitemap generation needs to re-evaluate this threshold every time it's rebuilt — not cache a city's indexability decision from a previous run.

---

# 29. Reserved segment routing

This page was already anticipated in the routing design: `/find/{areaSlug}/{segment}` already reserves `changes` as a literal segment, checked before category-slug matching — the same shape as the shipped `recently-inspected` reservation.

```text
if segment matches {business-slug}-{navnelbnr}
    → establishment detail

else if segment == "recently-inspected"
    → recently-inspected page

else if segment == "changes"
    → changes page

else if segment == valid category slug
    → category page

else
    → 404
```

No new routing pattern needed — this is a matter of implementing the branch that was already designed for.

---

# 30. Database query and data dependency — read this before estimating effort

**This is the one place this page is meaningfully harder to build than `/recently-inspected`, and worth flagging explicitly.**

`/recently-inspected` only needed a single column, `LatestInspectionDate`, which is a property of the establishment's current state — no history required. `/changes` needs something `/recently-inspected` didn't: a record of the *previous* score, not just the current one, plus the date the change happened.

The existing webhook system already computes this — old score vs. new score, detected via SQL `MERGE OUTPUT` during each sync — but confirm whether that comparison is **persisted** anywhere queryable, or whether it only exists transiently as the payload handed to the webhook dispatcher and then discarded. If it's the latter, this page cannot be built directly from `MERGE OUTPUT` alone, because:

- `MERGE OUTPUT` only reflects the most recent sync run — it can tell you what changed *today*, but not what changed 12 days ago, which this page's 90-day window (§15) needs.
- A visitor loading this page at any time other than immediately after a sync needs to read *stored* change history, not a live diff.

**If no persisted change log exists yet**, this page depends on adding one — conceptually:

```text
ScoreChangeLog
  Navnelbnr
  PreviousScore
  NewScore
  ChangeDate
```

populated by the same `MERGE OUTPUT` step that already fires the webhook, just also writing a row instead of only dispatching an event. This is a small addition to an existing sync step, not a new subsystem — but it is a real prerequisite, not just a query-writing exercise, and should be scoped/estimated as such before committing to a build date.

Once that log exists, the query is straightforward:

```sql
SELECT TOP (@pageSize)
    ...
FROM ScoreChangeLog scl
JOIN Establishments e ON e.Navnelbnr = scl.Navnelbnr
WHERE e.CitySlug = @areaSlug
  AND scl.ChangeDate >= @windowStart
ORDER BY
    scl.ChangeDate DESC,
    e.Name ASC;
```

If an establishment changed more than once within the window, decide whether to show only its most recent change (recommended — keeps one row per business, avoids the same name appearing three times in one list) or every change event. Recommended: **one row per establishment, its most recent change in the window only** — same principle as `/recently-inspected` showing current state, not full history, per row.

---

# 31. Indexing

Recommended index, conceptually:

```text
ChangeDate DESC
+
CitySlug (via join or denormalized onto the log table)
+
Name
```

The goal: find score changes in a city within a date window, ordered by recency, without scanning full establishment history per request. Exact schema/index approach depends on whether `ScoreChangeLog` is denormalized with `CitySlug` directly or joined at query time — worth deciding alongside §30.

---

# 32. Caching

Cache this page, same rationale as `/recently-inspected`: the underlying data only changes once per sync.

**Cache duration: 1–6 hours**, invalidated after the daily sync — same pattern already in place:

```text
Sync
 ↓
invalidate changes-page cache (per affected city)
 ↓
first request regenerates page
 ↓
subsequent visitors receive cached result
```

Since changes are sparse, invalidating only the specific cities that actually had a change in that sync (rather than the whole cache) is a reasonable optimization if the sync process already knows which cities were touched — not required for a first version.

---

# 33. Don't make the page dependent on login

Same as `/recently-inspected` §33 — public SEO acquisition page, no account, no cookie wall. Conversion happens later, via the CTA in §34.

---

# 34. Conversion CTA

Small CTA at the bottom, not the top:

## Own a food business?

Get notified automatically the moment your inspection score changes — instead of finding out from a customer.

- Real-time alerts when your score changes
- Monitor multiple locations
- Add an inspection badge to your website

**Learn about Smilr Business →**

This page's CTA can lean slightly harder on the "get notified automatically" angle than `/recently-inspected`'s CTA does — a visible public feed of *other* businesses' score changes is a natural, honest prompt for an owner to want the same tracking on their own.

---

# 35. Analytics to track

Same set as `/recently-inspected` §35, plus one addition specific to this page:

- organic landing sessions
- impressions / clicks / CTR / average position
- establishment-detail clicks
- recently-inspected-page clicks
- category-page clicks
- business CTA clicks
- registration conversions
- **improved vs. downgraded click-through rate**, to see whether one direction drives more engagement than the other (informs whether a future "most improved" page in §37 is worth prioritizing)

---

# 36. Rollout strategy

Launch alongside or shortly after the same initial city set already used for `/recently-inspected` (Copenhagen, Aarhus, Odense, Aalborg, Silkeborg, Esbjerg, Randers, Horsens, Vejle, Kolding) — no need to re-derive a separate list, since the two pages share the same area taxonomy and the same "does this city have enough activity" question.

Expect some of those cities to show the §14 empty state at launch, especially smaller ones, until enough sync cycles have accumulated real change events. That's expected and fine — it's an honest reflection of the data, not a bug.

---

# 37. Future extensions

Once this works, the same architecture (and the same `ScoreChangeLog` from §30) can support:

```text
/find/{city}/most-improved
/find/{city}/recently-downgraded
```

as explicit rankings rather than a plain chronological feed — each one needs its own tone decision per §25 before building, especially `recently-downgraded`.

And global variants:

```text
/find/changes
/find/most-improved
```

But do not build any of these alongside this page. Build `/find/{city}/changes` first, on the plain chronological model in this spec, and use §35's analytics to see whether a ranked variant is worth the added tone risk before building one.

---

# 38. Definition of done

Consider `/find/{city}/changes` complete when:

- [ ] `ScoreChangeLog` (or equivalent persisted previous-score/new-score/change-date record) exists and is populated by the sync process
- [ ] Canonical URL implemented
- [ ] Existing area-slug system reused
- [ ] Reserved `changes` route segment implemented
- [ ] Only genuine score transitions included — first-ever inspections and same-score re-inspections excluded
- [ ] 90-day rolling window applied
- [ ] One row per establishment (most recent change in window), not one row per change event
- [ ] Change date used for primary ordering; deterministic secondary sort by name
- [ ] 30 results per page; pagination implemented
- [ ] Invalid-city handling: 404
- [ ] 0 changes in window → empty state, `noindex,follow`
- [ ] 1–2 changes → render, `noindex,follow`
- [ ] 3+ changes → `index,follow`
- [ ] H1 dynamically contains city
- [ ] Dynamic title and meta description implemented
- [ ] Canonical implemented
- [ ] Breadcrumb + `BreadcrumbList` JSON-LD implemented
- [ ] No `Review`/`AggregateRating` schema anywhere on the page
- [ ] Previous score, new score, and direction (improved/downgraded) shown for every entry
- [ ] Direction shown with both color/icon and text (not color alone)
- [ ] Neutral tone applied throughout — no ranking, no "worst," no leaderboard framing (§25)
- [ ] Establishment cards link to existing canonical detail URLs
- [ ] City page links to changes page; changes page links back to city page
- [ ] Two-way cross-link with `/recently-inspected` implemented on both pages
- [ ] Category/explore links included
- [ ] Business CTA included near bottom
- [ ] Page cached, invalidated on sync
- [ ] Database query optimized/indexed
- [ ] Indexable pages included in sitemap; `noindex` and empty-state pages excluded
- [ ] Search Console monitoring added
- [ ] Click/conversion analytics added, including improved-vs-downgraded CTR

---

## The most important implementation principle

**This page is the public-facing proof of the exact trend-tracking capability the Business tier is meant to sell.** Every visitor who lands here from Google and sees "Smilr caught this business's score change" is, implicitly, seeing a live demo of "get notified when your own score changes." Keep the page itself scrupulously neutral and factual (§25) — the commercial argument should land because the capability is real and visible, not because the page editorializes to make it land.

```text
                    OFFICIAL DATA
                         │
                         ▼
                 Smilr normalized DB
                         │
        ┌────────────────┼────────────────┐
        ▼                ▼                ▼
  Establishment      Recently        ScoreChangeLog
     detail          inspected              │
        │                │                  ▼
        │                │              Changes page
        ▼                ▼                  │
     Google           Google              Google
        │                │                  │
        └────────────────┼──────────────────┘
                          ▼
                     Smilr Business
```

The first version can be small: persist the previous/new score pair the sync already computes → query the last 90 days for a city → order by date → render up to 30 → link to existing detail pages → add SEO metadata, neutral tone, breadcrumb → cache → sitemap. The surrounding sections are what keep that simple query from becoming either a thin page Google ignores or a page that reads as public shaming.

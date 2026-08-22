# Smilr — `/find/{city}/recently-inspected`

## 1. Purpose

Create a public, indexable SEO page showing establishments in a specific city that have been inspected most recently.

Example:

`/find/silkeborg/recently-inspected`

Primary objectives:

1. Capture organic searches related to recent food inspections in a city.
2. Give users a useful alternative to searching establishment-by-establishment.
3. Create a continuously changing page that Google can revisit.
4. Drive visitors into existing canonical establishment detail pages.
5. Increase discovery of Smilr.
6. Create another internal-linking layer between city pages and establishment pages.
7. Eventually provide a natural conversion path into Smilr Business.

This should be a **public feature** and should not require authentication.

---

# 2. URL

Use:

`/find/{area-slug}/recently-inspected`

Examples:

- `/find/silkeborg/recently-inspected`
- `/find/aarhus/recently-inspected`
- `/find/odense/recently-inspected`
- `/find/aalborg/recently-inspected`

The existing city slug generation should be reused.

Do not introduce a new city taxonomy.

The page should use exactly the same area definition as the existing `/find/{area-slug}/` pages.

---

# 3. What does “recently inspected” mean?

The ranking should be based on the **date of the latest inspection**, descending.

Example:

| Business | Latest inspection |
|---|---:|
| Restaurant A | 18 Aug 2026 |
| Restaurant B | 17 Aug 2026 |
| Restaurant C | 15 Aug 2026 |
| Restaurant D | 14 Aug 2026 |

The page should not be based on:

- score change date
- data-sync date
- establishment creation date
- business registration date

It should specifically represent:

> **The date of the latest recorded inspection.**

This distinction should be preserved in both the UI and metadata.

---

# 4. Recommended page title

Dynamic:

`Recently Inspected Food Businesses in Silkeborg | Smilr`

For Aarhus:

`Recently Inspected Food Businesses in Aarhus | Smilr`

Avoid putting “restaurants” in the title because the source data contains many categories beyond restaurants.

---

# 5. Meta description

Dynamic example:

> See the food businesses most recently inspected in Silkeborg. Browse official food inspection scores, inspection dates and historical results on Smilr.

For Aarhus, replace the city.

Keep the wording factual and avoid claims such as “best restaurants” because this page is about inspection recency, not restaurant quality.

---

# 6. Canonical URL

Canonical should be:

`https://smilrhq.dk/find/silkeborg/recently-inspected`

or whatever production canonical domain you are currently using.

The Azure Container Apps URL should not become the canonical SEO URL.

The same canonical URL should be used consistently regardless of query parameters.

For example:

`/find/silkeborg/recently-inspected?page=2`

should canonicalize to the appropriate pagination strategy described below.

---

# 7. Page heading

Use one clear H1:

# Recently inspected food businesses in Silkeborg

Under it:

> See the establishments in Silkeborg with the most recent recorded food inspections.

Then:

> Updated from the latest available Fødevarestyrelsen data.

Do not write a large generic SEO paragraph.

The data itself is the content.

---

# 8. Summary section

Immediately below the H1, show a small factual summary.

Example:

### Silkeborg

**248 food establishments**

**Latest inspection:** 18 August 2026

**Showing:** 30 most recently inspected

Possible additional statistic:

> **XX inspections recorded in the last 30 days**

Only show statistics that can be calculated reliably from your existing data.

Do not invent estimates.

---

# 9. Main listing

The main component should be a list/table of the most recently inspected establishments.

Recommended initial page size:

**30 establishments**

Example:

### Recently inspected

**1. Restaurant Name**

Silkeborg · Restaurant

**Inspected:** 18 August 2026

🟢 **Smiley 1**

[View inspection history →]

---

**2. Another Business**

Silkeborg · Bakery

**Inspected:** 17 August 2026

🟢 **Smiley 1**

[View inspection history →]

---

The entire business card can link to the canonical establishment URL.

Example:

`/find/silkeborg/daruma-ramen-1449060`

Do not create a second establishment URL under `/recently-inspected`.

---

# 10. Information displayed for every establishment

At minimum:

### Business name

Use the current official establishment name.

### Category

Use the existing `Pixibranche` category.

Example:

> Restauranter, pizzeriaer, kantiner m.m.

### Latest inspection date

Display the actual inspection date.

Prefer:

> 23 June 2026

rather than:

> 57 days ago

The absolute date is better for SEO and more useful to users.

### Current smiley / score

Display the current/latest score associated with the inspection.

### Link

Link to the existing canonical establishment page.

Example:

`/find/silkeborg/daruma-ramen-1449060`

---

# 11. Sort order

Primary sort:

```text
LatestInspectionDate DESC
```

Secondary deterministic sort:

```text
BusinessName ASC
```

This is important.

If multiple establishments were inspected on the same date, the result should remain stable.

Do not randomly reorder businesses between requests.

---

# 12. What if two inspections happened on the same date?

No special treatment is necessary.

Example:

**23 June 2026**

- Daruma Ramen
- Restaurant B
- Bakery C

Sort alphabetically by establishment name after the inspection date.

---

# 13. Pagination

I recommend **30 per page**.

Use:

`/find/silkeborg/recently-inspected?page=2`

However, be careful with SEO.

Initially, I would make:

- page 1 = indexable
- page 2+ = `noindex,follow`

unless you later discover meaningful search demand for deeper pagination.

The purpose of this page is to expose the most recent establishments and create internal links, not to generate thousands of nearly identical pagination pages.

Pagination links should still be crawlable.

---

# 14. Empty state

If a city has no establishments, do not render an empty SEO page.

Return:

**404**

This should follow the same general philosophy as your existing category pages.

If the city exists but there are no inspection records available, display a useful state rather than an empty table.

For example:

> No inspection records are currently available for this area.

But if this can only happen because of invalid/unknown city input, use 404.

---

# 15. Small-city handling

Do not require a minimum of 3 establishments here.

If there is:

- 1 establishment → page can still be useful
- 2 establishments → page can still be useful
- 3+ establishments → indexable if the content is sufficiently useful

However, I would use a simple SEO threshold based on whether there is meaningful content.

A practical initial rule:

### 0 establishments

404

### 1–2 establishments

Render page, but:

`noindex,follow`

### 3+ establishments

`index,follow`

This is consistent with the philosophy you've already implemented for your area × category pages.

If the number of cities is small enough, you can later inspect Search Console performance and adjust.

---

# 16. Important: Don't only show the list

This is where I would differentiate the page from a generic database query.

After the main list, add:

# Explore food inspection scores in Silkeborg

Links:

- All food businesses in Silkeborg
- Restaurants, pizzerias and canteens in Silkeborg
- Bakeries in Silkeborg
- Grocery stores in Silkeborg
- Butchers in Silkeborg
- Delis and smørrebrød shops in Silkeborg
- Fishmongers in Silkeborg

These should link to your existing area/category pages.

---

# 17. Add a “Recently changed” cross-link

If `/find/silkeborg/changes` exists, show:

## Recently changed in Silkeborg

> See businesses whose inspection score recently changed.

[View recently changed →]

This creates a strong relationship between two dynamic pages.

---

# 18. Add “Latest inspection” links from city page

The normal city page:

`/find/silkeborg`

should link to:

> **Recently inspected in Silkeborg →**

The recently-inspected page should link back:

> **View all food businesses in Silkeborg →**

This creates a simple SEO hub-and-spoke structure.

---

# 19. Add breadcrumb

Use:

**Find → Silkeborg → Recently inspected**

Structured data:

`BreadcrumbList`

Example hierarchy:

```text
Find
  >
Silkeborg
  >
Recently inspected
```

The final breadcrumb should correspond to the actual page.

---

# 20. Structured data

Use appropriate structured data.

At minimum:

### BreadcrumbList

This is straightforward and matches your existing detail-page implementation.

Potentially also use `ItemList` for the result list if the implementation is clean and accurately represents the visible list.

Do not manufacture ratings or reviews.

The official inspection score is not a customer review.

---

# 21. Internal links are extremely important

Every establishment shown should link to its canonical detail page.

Example:

```text
/find/silkeborg/recently-inspected
        |
        +-- Daruma Ramen
        |     |
        |     +-- /find/silkeborg/daruma-ramen-1449060
        |
        +-- Restaurant B
        |     |
        |     +-- /find/silkeborg/restaurant-b-123456
        |
        +-- Bakery C
              |
              +-- /find/silkeborg/bakery-c-987654
```

This creates another crawl path to every establishment.

---

# 22. Link from every establishment page back to it

On:

`/find/silkeborg/daruma-ramen-1449060`

add a contextual link when appropriate:

> **Recently inspected in Silkeborg**

or:

> **See recently inspected businesses in Silkeborg →**

Do not necessarily show this if the establishment isn't currently near the top of the recently-inspected list.

A simple city-level link is sufficient.

---

# 23. Add “inspection recency” to the establishment page

The detail page should show:

> **Latest inspection**
>
> 23 June 2026
>
> This establishment is among the recently inspected food businesses in Silkeborg.

Then link:

> **See recently inspected businesses in Silkeborg →**

This gives Google a natural two-way relationship between the pages.

---

# 24. Suggested page layout

The final page could look approximately like this:

```text
Breadcrumb
Find → Silkeborg → Recently inspected


H1
Recently inspected food businesses in Silkeborg

See the establishments in Silkeborg with the most recent
recorded food inspections.

Updated: 19 August 2026


┌─────────────────────────────────────────┐
│ 248 food establishments                 │
│ Latest inspection: 18 August 2026       │
│ 30 most recently inspected              │
└─────────────────────────────────────────┘


Recently inspected

1. Daruma Ramen
   Restaurant
   Inspected 23 June 2026
   🟢 Smiley 1
   View details →

2. Business B
   Bakery
   Inspected 21 June 2026
   🟢 Smiley 1
   View details →

3. Business C
   Grocery
   Inspected 20 June 2026
   🟡 Smiley 2
   View details →

...

[Next →]


Recently changed in Silkeborg
See businesses whose score recently changed →


Explore food inspection scores in Silkeborg

Restaurants...
Bakeries...
Grocery stores...
Butchers...
...


Food businesses in Silkeborg
View all →


Data source

Inspection data is based on official
Fødevarestyrelsen data.
```

---

# 25. Do not call it “latest restaurants”

The page should deliberately use language around:

- food businesses
- establishments
- inspections
- inspection scores
- food inspection

rather than:

- best restaurants
- restaurant ratings
- restaurant reviews

The dataset is broader than restaurants, and the Smiley score is a regulatory inspection result rather than a consumer rating.

This keeps the site's positioning accurate.

---

# 26. SEO content should be generated from real data

Avoid adding a generic 500-word paragraph to every city.

Instead generate a small factual introduction.

For example:

> **Recently inspected food businesses in Silkeborg**
>
> Smilr lists food establishments in Silkeborg by the date of their latest recorded food inspection. This page shows the most recently inspected businesses first, together with their current inspection score and links to their inspection history.

Then dynamically:

> The latest recorded inspection in this list took place on 18 August 2026.

That's enough.

The data itself creates uniqueness.

---

# 27. Page freshness

This is an important advantage of this page type.

Every time new inspection data arrives, the page can change.

For example:

Before:

```text
1. Business A — 15 Aug
2. Business B — 14 Aug
3. Business C — 12 Aug
```

After sync:

```text
1. Business X — 19 Aug
2. Business A — 15 Aug
3. Business B — 14 Aug
```

The page naturally becomes fresh without manually publishing content.

Do not fake an “Updated” timestamp unless the underlying data actually changed.

You can separately show:

> Data synchronized: 19 August 2026

if that is genuinely your sync timestamp.

---

# 28. Sitemap

Include indexable `/recently-inspected` city pages in `sitemap.xml`.

For example:

```text
/find/silkeborg/recently-inspected
/find/aarhus/recently-inspected
/find/odense/recently-inspected
```

Only include pages that meet your indexability threshold.

Do not put `noindex` pages into the sitemap.

---

# 29. Avoid generating pages for every possible string

This is important for your routing architecture.

Your existing routing already uses:

```text
/find/{areaSlug}/{segment}
```

and distinguishes category/detail based on the segment.

Add `recently-inspected` as a **reserved segment**.

Conceptually:

```text
/find/{areaSlug}/{segment}
```

Resolution:

```text
if segment matches business-{digits}
    → establishment detail

else if segment == "recently-inspected"
    → recently inspected page

else if segment == "changes"
    → changes page

else if segment == valid category slug
    → category page

else
    → 404
```

This is preferable to introducing another conflicting route.

---

# 30. Database query

The core query should be extremely simple.

Conceptually:

```sql
SELECT TOP (@pageSize)
    ...
FROM Establishments
WHERE CitySlug = @areaSlug
  AND LatestInspectionDate IS NOT NULL
ORDER BY
    LatestInspectionDate DESC,
    Name ASC;
```

If your schema stores inspection history separately, derive the latest inspection using the appropriate existing relationship.

**Do not query the complete history for every establishment just to render this page.**

If necessary, introduce/maintain a readily queryable:

```text
LatestInspectionDate
```

or equivalent indexed field.

---

# 31. Indexing

This query will potentially become very common.

Recommended database index conceptually:

```text
City / CitySlug
+
LatestInspectionDate DESC
+
Name
```

The exact SQL index should follow your existing schema.

The goal is:

> Find establishments in a city and order by latest inspection date without scanning the entire history table.

---

# 32. Caching

I would definitely cache this page/data.

Your uploaded roadmap already identified the concern that anonymous directory traffic should not hit Azure SQL directly at scale.

This page is an excellent candidate for caching.

For example:

**Cache duration: 1–6 hours**

Because the underlying source is synchronized daily, there is little value in hitting SQL for every visitor.

After the daily sync:

```text
Sync
 ↓
invalidate recently-inspected cache
 ↓
first request regenerates page
 ↓
subsequent visitors receive cached result
```

You can later move to more sophisticated cache invalidation if traffic warrants it.

---

# 33. Don't make the page dependent on login

This is an SEO acquisition page.

No account.

No cookie wall.

No “sign up to see results.”

The visitor should immediately see useful information.

The conversion happens later.

---

# 34. Conversion CTA

At the bottom, add a **small** business CTA.

Not at the top.

Example:

## Own a food business?

Monitor your inspection score automatically with Smilr.

- Get notified when your score changes
- Monitor multiple locations
- Add an inspection badge to your website

**Learn about Smilr Business →**

The SEO page should primarily serve the visitor's search intent.

The commercial CTA is secondary.

---

# 35. Analytics to track

For every recently-inspected page, track:

- organic landing sessions
- impressions
- clicks
- CTR
- average position
- establishment-detail clicks
- category-page clicks
- changes-page clicks
- business CTA clicks
- registration conversions

Especially important:

```text
Google
 ↓
/find/silkeborg/recently-inspected
 ↓
/find/silkeborg/daruma-ramen-1449060
 ↓
Claim listing
 ↓
Business signup
```

If this funnel works, the SEO page becomes a customer acquisition channel rather than merely a traffic project.

---

# 36. Rollout strategy

Do **not** necessarily launch thousands of these pages immediately.

Start with cities that have enough data.

For example:

- Copenhagen
- Aarhus
- Odense
- Aalborg
- Silkeborg
- Esbjerg
- Randers
- Horsens
- Vejle
- Kolding

Then monitor Google Search Console.

After indexing:

- inspect impressions
- inspect queries
- see which cities receive traffic
- identify unexpected search terms
- expand the page template based on actual demand

This is much better than guessing every SEO keyword in advance.

---

# 37. Future extensions

Once this works, the same architecture can support:

```text
/find/{city}/recently-inspected
/find/{city}/changes
/find/{city}/most-improved
/find/{city}/recently-downgraded
```

And global:

```text
/find/recently-inspected
/find/changes
/find/most-improved
```

But **do not build all of these simultaneously**.

Build `recently-inspected` first.

Measure it.

Then use exactly the same data/query/cache/page architecture for `changes`.

---

# 38. Definition of done

I would consider `/find/{city}/recently-inspected` complete when:

- [ ] Canonical URL implemented
- [ ] Existing area-slug system reused
- [ ] Reserved `recently-inspected` route implemented
- [ ] Latest inspection date used for ordering
- [ ] Deterministic secondary sort added
- [ ] 30 results per page
- [ ] Pagination implemented
- [ ] Empty/invalid-city handling implemented
- [ ] 0 establishments → 404
- [ ] 1–2 establishments → `noindex,follow`
- [ ] 3+ establishments → `index,follow`
- [ ] H1 dynamically contains city
- [ ] Dynamic title implemented
- [ ] Dynamic meta description implemented
- [ ] Canonical implemented
- [ ] Breadcrumb implemented
- [ ] BreadcrumbList JSON-LD implemented
- [ ] Optional ItemList JSON-LD implemented only if valid
- [ ] Establishment cards link to existing canonical detail URLs
- [ ] Current/latest inspection score displayed
- [ ] Latest inspection date displayed
- [ ] Category displayed
- [ ] City page links to recently-inspected page
- [ ] Recently-inspected page links back to city
- [ ] Category links included
- [ ] Recently-changed cross-link included when available
- [ ] Business CTA included near bottom
- [ ] Page cached
- [ ] Database query optimized/indexed
- [ ] Indexable pages included in sitemap
- [ ] `noindex` pages excluded from sitemap
- [ ] Search Console monitoring added
- [ ] Click/conversion analytics added

---

## The most important implementation principle

**Don't think of this as a new feature page. Think of it as a new SEO page type generated from your existing inspection-history data.**

Your architecture then becomes:

```text
                    OFFICIAL DATA
                         │
                         ▼
                 Smilr normalized DB
                         │
             ┌───────────┼───────────┐
             ▼           ▼           ▼
        Establishment  Recent      Changes
          detail      inspection
             │           │           │
             ▼           ▼           ▼
          Google      Google       Google
             │           │           │
             └───────────┼───────────┘
                         ▼
                    Smilr Business
```

That's much more powerful than making `/recently-inspected` a simple list.

The **first version can actually be quite small technically**: query latest inspection per establishment → order by date → render 30 → link to existing detail pages → add SEO metadata → cache → sitemap. The surrounding sections are what turn that simple query into a page worth ranking.
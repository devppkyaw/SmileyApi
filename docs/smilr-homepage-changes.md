# Smilr Home Page Changes — Implementation Specification

## 1. Purpose

Update the existing Smilr homepage while **keeping the current visual design, styling language, spacing, typography, cards, buttons, colors, and overall layout character**.

The goal is to make the homepage serve two audiences:

1. **Businesses** — businesses that want to show their official food inspection score, monitor locations, and eventually use Pro/Enterprise functionality.
2. **Public/SEO visitors** — people who want to find a Danish food business and check its official inspection information.

The homepage should become the main entry point into the public `/find` SEO/discovery experience without turning the homepage into a redesign.

The existing design should be preserved. This is a **content, navigation, hierarchy, and CTA enhancement**, not a visual redesign.

---

# 2. Navigation changes

## 2.1 Hide Developers from the main menu

Remove **Developers** from the visible main navigation menu.

Do not delete the developer/API functionality or routes.

The change is only to the public navigation.

Developers/API functionality can remain accessible through direct URLs and can be exposed again later when the developer offering is ready.

---

## 2.2 Hide API Docs from the main menu

Remove **API Docs** from the visible main navigation menu.

Do not delete or disable the API documentation route.

The API documentation remains accessible directly and can be linked from the future developer/API section when that product is ready.

---

## 2.3 Keep existing navigation design

Do not redesign the header.

Keep:

- existing logo/brand treatment
- existing navigation typography
- existing spacing
- existing responsive behavior
- existing login CTA
- existing mobile menu behavior

Only remove the two visible navigation items:

- Developers
- API Docs

The resulting navigation should prioritize the public discovery experience and business experience.

Recommended conceptual order:

```text
Smilr | Find | For Businesses | Log in
```

Use the existing visual treatment and exact styling conventions already present in the application.

---

# 3. Homepage hero

Keep the existing hero design and visual hierarchy.

Current positioning is:

> Show your food inspection score on your website

and the supporting message explains that Smilr uses official Fødevarestyrelsen data and stays up to date.

Do not replace the hero with a completely new design.

Instead, preserve the current hero and add a clear path for public visitors.

The homepage should communicate that Smilr is useful both for businesses and for people looking up food inspection information.

---

# 4. Add public “Find a food business” section

Add a prominent section below the existing hero/business introduction using the **same existing design system**.

Suggested content:

## Heading

> Check a food business

## Supporting text

> Search for a restaurant, café, bakery or other food business and see its latest official inspection score.

## Primary CTA

> Find a business

CTA destination:

```text
/find
```

The CTA should use the existing primary button component/style.

---

# 5. Recommended homepage search box

If the existing `/find` search implementation can be reused cleanly, add a search input to the homepage section.

Suggested UI:

```text
Check a food business

Search by business name or CVR

[ Business name or CVR... ] [Search]
```

The search should reuse the existing `/find/search?q=` functionality.

Do not create a second search implementation.

The homepage search should simply send the visitor into the existing Find/search flow.

If adding the search input would require a disproportionate amount of frontend work, implement the “Find a business” CTA first and leave the search box for a follow-up.

---

# 6. Add “Explore food inspections” section

Add a new homepage section below the Find/search section.

Use the existing card/section design language.

## Heading

> Explore food inspections

## Supporting text

> Browse official food inspection information by city, see recently inspected businesses, and follow recent score changes.

This section should expose the SEO/discovery network that has already been implemented.

---

# 7. Popular cities

Within the “Explore food inspections” section, add a small list of popular/high-value cities.

Suggested initial cities:

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

Each city must link to its existing canonical city page:

```text
/find/{area-slug}
```

Use the existing area slug generation/routing logic.

Do not hardcode URLs if the application already has a URL builder/helper for Find pages.

The city list should be concise. It should not become a giant directory on the homepage.

---

# 8. Recently inspected CTA

Add a compact feature/card/link for the recently inspected pages.

Suggested copy:

## Recently inspected

> See food businesses with the most recent recorded inspections.

CTA:

> View recently inspected

Destination:

Prefer a global page if one exists:

```text
/find/recently-inspected
```

If the global page does not yet exist, do not invent the route. Instead, link to `/find` or omit this global CTA until a global page exists.

The already implemented city-level pages remain:

```text
/find/{area-slug}/recently-inspected
```

---

# 9. Recent score changes CTA

Add a compact feature/card/link for recent inspection-score changes.

Suggested copy:

## Recent score changes

> See businesses whose official inspection scores recently changed.

CTA:

> View recent changes

Destination:

Prefer a global page if one exists:

```text
/find/changes
```

If the global page does not yet exist, do not invent the route. Instead, link to `/find` or omit this global CTA until a global page exists.

The already implemented city-level pages remain:

```text
/find/{area-slug}/changes
```

---

# 10. Homepage information architecture

The recommended order is:

```text
1. Existing header/navigation
2. Existing hero
3. Existing business/product explanation
4. NEW — Check a food business
5. NEW — Explore food inspections
   - Popular cities
   - Recently inspected
   - Recent score changes
6. Existing business/product sections
7. Existing pricing/business section
8. Existing footer
```

Do not force this exact order if the existing homepage layout has a strong visual structure that would be disrupted.

The priority is:

- Hero remains prominent.
- Public Find functionality becomes clearly discoverable.
- Existing business messaging remains intact.
- Pricing and commercial sections remain intact but are updated according to the coming-soon requirements below.

---

# 11. Pro plan — Coming Soon

The current Pro plan should remain visually consistent with the existing pricing design.

Do not remove the Pro plan.

Instead, clearly mark it as:

> Coming soon

Recommended supporting copy:

> Pro features are coming soon. Contact us if you're interested and we'll keep you updated.

The Pro CTA should not send users into an unfinished payment/checkout flow.

Recommended CTA:

> Contact us

The exact destination should use the application's existing contact mechanism if one exists.

Do not invent a new email address.

If no contact mechanism exists yet, use the existing contact route or make the CTA non-functional until a contact destination is defined.

---

# 12. Enterprise plan — Coming Soon

Keep the existing Enterprise pricing/card design.

Mark it clearly as:

> Coming soon

Suggested copy:

> Enterprise features are coming soon. Contact us if you're interested in multi-location or larger-scale requirements.

CTA:

> Contact us

Do not send users to an unfinished payment page.

Enterprise should not appear purchasable until the payment/commercial flow is ready.

---

# 13. Free plan

Keep the Free plan available if the existing product already supports it.

Do not label Free as coming soon unless the implementation is also unavailable.

The homepage should make the distinction clear:

```text
Free
Available now

Pro
Coming soon

Enterprise
Coming soon
```

If Free registration is currently operational, the Free CTA should continue to use the existing registration flow.

---

# 14. Pricing presentation

Keep the existing pricing design.

Only update the content/state of Pro and Enterprise.

Recommended visual hierarchy:

### Free

Available now

[Existing free CTA]

### Pro

COMING SOON

[Contact us]

### Enterprise

COMING SOON

[Contact us]

Do not create a new pricing component.

Reuse the existing card, badge, button, typography, and spacing styles.

---

# 15. Business messaging

Keep the existing business-oriented explanation because the current homepage already positions Smilr around:

- official Fødevarestyrelsen data
- website badge/widget
- automatic updates
- score-change alerts
- multiple locations
- business use

The new public Find experience should **complement** this rather than replace it.

The homepage should communicate:

> Smilr helps people find official food inspection information, while businesses can use Smilr to display and monitor their own information.

---

# 16. SEO goals

The homepage should help Google discover the Find network.

Important internal links from the homepage should include:

```text
/
/find
/find/{city}
/find/{city}/recently-inspected
/find/{city}/changes
```

Do not link every individual establishment from the homepage.

The homepage should act as the top-level entry point.

---

# 17. Internal linking structure

Target architecture:

```text
Homepage
   │
   ├── /find
   │      │
   │      ├── /find/copenhagen
   │      ├── /find/aarhus
   │      ├── /find/silkeborg
   │      │
   │      ├── /find/silkeborg/recently-inspected
   │      ├── /find/silkeborg/changes
   │      │
   │      └── /find/silkeborg/{business}-{navnelbnr}
   │
   └── Business
          ├── Free
          ├── Pro — Coming soon
          └── Enterprise — Coming soon
```

This gives the public SEO pages a clear path from the homepage.

---

# 18. Canonical and SEO requirements

The homepage should use the production Smilr domain as its canonical URL.

Do not use the Azure Container Apps hostname as the canonical production URL.

All internal homepage links should point to the production/canonical domain through normal application URL generation.

Avoid hardcoded Azure deployment URLs.

The existing Find pages should continue using their existing canonical URL logic.

---

# 19. Robots and indexing

The public Find pages should remain indexable.

Do not add `noindex` to:

- homepage
- `/find`
- city pages
- category pages
- recently-inspected pages with sufficient content
- changes pages with sufficient content
- establishment detail pages

Do not make Pro/Enterprise pages or pricing sections indexable/unindexable solely because they are “coming soon.” They can remain part of the homepage.

The important point is that unfinished checkout/payment URLs should not be exposed as functional purchase destinations.

---

# 20. Sitemap

No special sitemap change is required solely for the homepage changes.

Continue including the existing public Find URLs that are intended to be indexed.

The homepage itself should naturally remain in the sitemap.

If global pages such as:

```text
/find/recently-inspected
/find/changes
```

are implemented later, add them to the sitemap only when they are real, indexable pages.

---

# 21. Search-engine-friendly visible text

Use natural phrases that describe the actual data.

Preferred:

- food inspection
- food inspection score
- food business
- latest inspection
- recently inspected
- inspection score changes
- official inspection data
- Fødevarestyrelsen

Avoid keyword stuffing.

Do not add artificial paragraphs purely for SEO.

The purpose of the homepage is to introduce Smilr and link users/crawlers into the useful data pages.

---

# 22. Accessibility

Keep the existing accessibility patterns.

Ensure:

- Search input has an accessible label.
- Buttons have descriptive text.
- City links have meaningful names.
- Cards are keyboard accessible where applicable.
- Existing focus states remain intact.
- Heading hierarchy remains logical.

The homepage should have one primary H1.

New sections should use H2 headings.

---

# 23. Responsive behavior

Keep the existing responsive design.

On mobile:

- Do not create a wide city-link grid that becomes difficult to use.
- Allow cities to wrap naturally or use the existing responsive card/list pattern.
- Recently inspected and recent changes cards should stack naturally.
- Search input/button should fit the existing mobile layout.
- Navigation changes must work with the existing mobile menu.

No new mobile-specific visual design is required.

---

# 24. Design constraint

This implementation must **not** become a redesign.

Do not change:

- global color palette
- typography system
- border radius
- button style
- card style
- header style
- footer style
- page width
- hero visual treatment
- animation language
- spacing scale

Reuse existing components wherever possible.

The new content should look as though it was part of the original homepage from the beginning.

---

# 25. Analytics

Add tracking for the new homepage paths where the existing analytics system supports it.

Track at least:

- homepage → Find click
- homepage → city click
- homepage → recently inspected click
- homepage → changes click
- homepage → search submission
- homepage → Free registration click
- homepage → Pro contact click
- homepage → Enterprise contact click

The most important funnel is:

```text
Google
  ↓
Find SEO page
  ↓
Business detail
  ↓
Homepage / business CTA
  ↓
Free registration
```

and:

```text
Homepage
  ↓
Find
  ↓
Business detail
```

---

# 26. Acceptance criteria

The implementation is complete when:

- [ ] Existing homepage visual design remains intact.
- [ ] Developers is removed from the visible main navigation.
- [ ] API Docs is removed from the visible main navigation.
- [ ] Existing Developer/API routes are not deleted.
- [ ] `/find` is clearly accessible from the homepage.
- [ ] A “Check a food business” section exists.
- [ ] The section links to `/find`.
- [ ] Existing Find search can be reused from the homepage if practical.
- [ ] “Explore food inspections” section exists.
- [ ] Popular cities are linked to existing city pages.
- [ ] Recently inspected is promoted from the homepage.
- [ ] Recent score changes is promoted from the homepage.
- [ ] No nonexistent global routes are introduced.
- [ ] Pro is clearly marked “Coming soon.”
- [ ] Enterprise is clearly marked “Coming soon.”
- [ ] Pro does not lead to unfinished payment.
- [ ] Enterprise does not lead to unfinished payment.
- [ ] Pro provides a Contact Us path if available.
- [ ] Enterprise provides a Contact Us path if available.
- [ ] Free plan continues to work if currently available.
- [ ] Homepage canonical uses the production domain.
- [ ] No Azure Container Apps URL is hardcoded into public canonical/internal links.
- [ ] Homepage remains responsive.
- [ ] New sections reuse existing components/styles.
- [ ] Heading hierarchy remains valid.
- [ ] New links are crawlable.
- [ ] Existing sitemap behavior remains correct.
- [ ] No existing homepage functionality is removed unintentionally.

---

# 27. Recommended implementation priority

Implement in this order:

### Phase A — Navigation

1. Hide Developers.
2. Hide API Docs.
3. Add Find to the main navigation.

### Phase B — Public discovery

4. Add “Check a food business.”
5. Link to `/find`.
6. Reuse `/find/search` for optional homepage search.

### Phase C — SEO discovery

7. Add “Explore food inspections.”
8. Add popular city links.
9. Add recently inspected link.
10. Add recent changes link.

### Phase D — Commercial state

11. Mark Pro as Coming Soon.
12. Mark Enterprise as Coming Soon.
13. Replace unfinished payment CTAs with Contact Us.

### Phase E — SEO/production validation

14. Verify production canonical domain.
15. Verify no Azure hostname is used in canonical/internal URLs.
16. Verify links are crawlable.
17. Verify homepage is included in sitemap.
18. Verify existing Find pages remain indexable.

---

# 28. Final intended homepage structure

The final homepage should conceptually communicate:

```text
SMILR

[Existing hero]
Official food inspection data for your business.

[Existing business CTA]


CHECK A FOOD BUSINESS

Search by business name or CVR

[ Search __________________ ] [Find]


EXPLORE FOOD INSPECTIONS

Popular cities
Copenhagen · Aarhus · Odense · Aalborg · Silkeborg · ...

[Recently inspected]
See food businesses with the most recent inspections.
[View recently inspected]

[Recent score changes]
See businesses whose official inspection scores recently changed.
[View recent changes]


[Existing Smilr business/product sections]


PRICING

Free
Available now
[Existing CTA]

Pro
COMING SOON
[Contact us]

Enterprise
COMING SOON
[Contact us]


[Existing footer]
```

The key is that the homepage now serves as both:

**B2B product landing page + gateway into Smilr's public SEO data network.**

No visual redesign is required.

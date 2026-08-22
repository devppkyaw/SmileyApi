# Removed / hidden features — bring-back list

Tracks everything unlinked, hidden, or removed from the public site and dashboard while Pro,
Webhooks, and the Developer API aren't ready to be sold/used yet (see `docs/smilr-homepage-changes.md`
for the original homepage spec that started this pass). **Nothing was deleted at the backend/route
level** unless a section below says otherwise — every item here is a front-end restore, not a rebuild,
except where a "Backend prerequisite" note says a dependency (like Stripe secrets) also needs to be in
place before the restored UI would actually work end-to-end.

Each section has the exact original snippet, so it can be pasted back in verbatim.

---

## 1. Site-wide nav — "Developers" / "API Docs"

**Files:** `index.html`, `about.html`, `contact.html`, `developers.html`, `privacy.html`,
`scores.html`, `terms.html`, `src/SmilrApi.Api/Rendering/FindPageRenderer.cs` (`Layout()`)

Original nav-links block on `index.html`:
```html
<a href="#businesses" class="nav-link">For Businesses</a>
<a href="developers.html" class="nav-link">Developers</a>
<a href="/scalar/v1" class="nav-link">API Docs</a>
<a id="navAuthLink" href="/login.html" class="nav-link">Log in</a>
```

Original nav-links block on `about.html` / `contact.html` / `privacy.html` / `scores.html` /
`terms.html` (identical across all five):
```html
<a href="/#businesses" class="nav-link">For Businesses</a>
<a href="/developers.html" class="nav-link">Developers</a>
<a id="navAuthLink" href="/login.html" class="nav-link">Log in</a>
```

Original nav-links block on `developers.html` (self-links, bold "Developers"; "API Docs" pointed at
its own in-page `#endpoints` anchor, not `/scalar/v1`):
```html
<a href="index.html#businesses" class="nav-link">For Businesses</a>
<a href="developers.html" class="nav-link" style="font-weight:600">Developers</a>
<a href="#endpoints" class="nav-link">API Docs</a>
<a id="navAuthLink" href="/login.html" class="nav-link">Log in</a>
```

Original nav on `FindPageRenderer.cs`'s `Layout()` (used by every `/find/*` page):
```html
<a href="/find" class="nav-link">Find a restaurant</a>
<a href="/developers.html" class="nav-link">Developers</a>
<a href="/login.html" class="nav-link">Log in</a>
```

**Restore:** re-add the removed `<a>` line(s) back into each file's nav block. `developers.html`
itself and `/scalar/v1` (mapped in `Program.cs` via `MapScalarApiReference`) were never unreachable —
only the nav links to them were removed.

---

## 2. Homepage hero — "Building an app? Developer API →"

**File:** `index.html`, directly under the hero's `.cta-pair` div

```html
<p style="margin-top:16px;font-size:0.9rem;color:rgba(255,255,255,0.55)">
  Building an app? <a href="developers.html" style="color:rgba(255,255,255,0.85);text-decoration:underline">Developer API →</a>
</p>
```

**Restore:** paste back in directly after the `.cta-pair` div, before the closing `</div>` of
`.hero-content`.

---

## 3. About page — "Developer API →" CTA button

**File:** `about.html`

```html
<div style="margin-top:36px;display:flex;gap:16px;flex-wrap:wrap">
  <a href="/register.html" class="btn-primary">Register your business →</a>
  <a href="/developers.html" class="btn-secondary">Developer API →</a>
</div>
```
(current state only has the `Register your business →` button in that div)

**Restore:** add the `Developer API →` line back into the CTA row.

---

## 4. Pricing table — Pro/Enterprise "Coming soon"

**File:** `index.html`, Tier Comparison (`.tiers`) section — **tagged, not removed**; Pro/Enterprise
rows and CTAs still render, just marked unavailable.

Header row before (`style.css`'s new `.tier-note` class not yet added):
```html
<th>Free<br><small style="font-weight:400;font-size:0.8rem">0 kr/mo</small></th>
<th>Pro<br><small style="font-weight:400;font-size:0.8rem">199 kr/mo</small></th>
<th>Enterprise<br><small style="font-weight:400;font-size:0.8rem">Contact us</small></th>
```
CTA row before (no Pro contact button existed):
```html
<a href="register.html" class="btn-primary">Register for free →</a>
<a href="mailto:info@smilrhq.dk" class="btn-secondary">Enterprise — contact us →</a>
```

**Restore:** remove the three `<span class="tier-note">...</span>` tags from the `<th>` cells and
remove the `Pro — contact us →` button, restoring the two lines above. (`.tier-note`/`.city-tag` CSS
classes can stay either way — harmless if unused elsewhere.)

**Backend prerequisite for a real, purchasable Pro tier** (distinct from just un-tagging the UI):
`Stripe--SecretKey`, `Stripe--WebhookSecret`, `Stripe--PriceId` Key Vault secrets are **not set** in
production as of the last ops check — the checkout code path exists but has nothing to actually
charge against until those are configured. See `project-smilr-ops` memory / `infra/modules/
keyvault.bicep`.

---

## 5. Dashboard — "Upgrade to Pro" free-tier CTA

**File:** `dashboard.html`

HTML (was directly above the tab bar, inside `#dashContent`):
```html
<div id="freeActions" style="display:none;margin-bottom:24px;padding:16px 20px;border:1px solid var(--border);border-radius:var(--radius);background:var(--surface)">
  <strong style="display:block;margin-bottom:4px">Upgrade to Pro</strong>
  <p style="font-size:0.875rem;color:var(--text-muted);margin-bottom:12px">199 kr/month — up to 20 CVRs, add by Navnelbnr, webhooks, Developer API.</p>
  <button class="btn-primary" onclick="startCheckout()">Upgrade to Pro →</button>
  <span id="checkoutStatus" style="margin-left:12px;font-size:0.85rem;color:var(--text-muted)"></span>
</div>
```

JS — `startCheckout()` function (was directly above `openPortal()`):
```js
async function startCheckout() {
  const status = document.getElementById('checkoutStatus');
  if (status) status.textContent = 'Redirecting…';
  try {
    const res  = await fetch('/v1/business/checkout', { method: 'POST' });
    const data = await res.json();
    if (res.ok) {
      if (status) status.textContent = '';
      location.href = data.url;
    } else {
      if (status) status.textContent = data.error?.message || 'Failed to start checkout.';
    }
  } catch {
    if (status) status.textContent = 'Network error.';
  }
}
```

JS — the startup IIFE's tier-branch and `?upgraded=1` branch each need their `freeActions`/
`webhookLocked` show/hide lines restored alongside section 6 below (they were interleaved with the
Webhooks-tab lines in the original file — see that section for the full before/after).

JS — the CVR-limit-reached error handler (`cvrForm` submit listener) had an inline upgrade link:
```js
// current (simplified)
status.style.color = 'var(--red)';
status.textContent = data.error?.message || 'Failed.';

// original
status.style.color = 'var(--red)';
if (data.error?.code === 'cvr_limit_reached') {
  status.innerHTML = data.error.message + ' <a href="#" onclick="startCheckout();return false">Upgrade →</a>';
} else {
  status.textContent = data.error?.message || 'Failed.';
}
```

JS — `pageshow` listener also cleared `checkoutStatus`:
```js
window.addEventListener('pageshow', function () {
  document.getElementById('portalStatus').textContent   = '';
  document.getElementById('checkoutStatus').textContent = '';
});
```

**Backend prerequisite:** same Stripe secrets as section 4 — `/v1/business/checkout` exists and is
wired, but has nothing to charge against until those Key Vault secrets are set.

---

## 6. Dashboard — Webhooks tab

**File:** `dashboard.html`

Tab button (was the middle button of the 3-button `.dash-tabs` bar):
```html
<button class="dash-tab" data-tab="webhooks">Webhooks</button>
```

Full panel:
```html
<!-- ── Webhooks tab ── -->
<div id="tab-webhooks" class="tab-panel" style="display:none">

  <div id="webhookLocked" style="display:none;padding:24px 0">
    <div class="locked-card">
      <div style="font-size:1.4rem;margin-bottom:10px">🔒</div>
      <strong style="display:block;margin-bottom:6px">Webhooks are a Pro feature</strong>
      <p style="font-size:0.875rem;color:var(--text-muted);margin-bottom:16px">Upgrade to receive score-change notifications at your URL.</p>
      <button class="btn-primary" onclick="startCheckout()">Upgrade to Pro →</button>
    </div>
  </div>

  <div id="webhookSection" style="display:none">
    <p style="font-size:0.9rem;color:var(--text-muted);margin-bottom:16px">Get notified at your URL when a smiley score changes.</p>
    <form id="webhookForm" style="display:grid;grid-template-columns:1fr 2fr auto;gap:10px;max-width:640px;align-items:end;margin-bottom:10px">
      <div>
        <label style="font-size:0.8rem;font-weight:600;color:var(--text-muted);display:block;margin-bottom:4px">Establishment ID</label>
        <input type="number" id="webhookEstId" placeholder="123456" required
               style="width:100%;padding:10px 14px;border-radius:6px;border:1.5px solid var(--border);font-size:1rem" />
      </div>
      <div>
        <label style="font-size:0.8rem;font-weight:600;color:var(--text-muted);display:block;margin-bottom:4px">Callback URL (HTTPS)</label>
        <input type="url" id="webhookUrl" placeholder="https://yoursite.com/webhook" required
               style="width:100%;padding:10px 14px;border-radius:6px;border:1.5px solid var(--border);font-size:1rem" />
      </div>
      <button type="submit" class="btn-primary" style="white-space:nowrap">Add webhook</button>
    </form>
    <div id="webhookStatus" style="margin-bottom:16px;font-size:0.9rem"></div>
    <div id="webhookList"></div>
  </div>

</div>
```

JS — three functions + one form listener (were directly above `startCheckout()`):
```js
async function loadWebhooks() {
  const res  = await fetch('/v1/business/webhooks');
  const list = await res.json();
  const el   = document.getElementById('webhookList');
  if (!Array.isArray(list) || !list.length) {
    el.innerHTML = '<p style="color:var(--text-muted);font-size:0.9rem">No webhooks yet.</p>';
    return;
  }
  el.innerHTML = list.map(function (w) {
    return '<div style="display:flex;justify-content:space-between;align-items:center;padding:10px 14px;border:1px solid var(--border);border-radius:6px;margin-bottom:8px;background:var(--surface);gap:12px">' +
      '<div style="font-size:0.875rem">' +
        '<span style="font-family:var(--font-mono);color:var(--text-muted)">#' + w.establishmentId + '</span>' +
        ' <span>' + esc(w.callbackUrl) + '</span>' +
      '</div>' +
      '<button onclick="deleteWebhook(' + w.id + ')" style="background:none;border:1px solid var(--border);border-radius:4px;padding:4px 10px;cursor:pointer;font-size:0.8rem;color:var(--text-muted)">Remove</button>' +
    '</div>';
  }).join('');
}

document.getElementById('webhookForm').addEventListener('submit', async function (e) {
  e.preventDefault();
  const status = document.getElementById('webhookStatus');
  status.textContent = '';
  const res  = await fetch('/v1/business/webhooks', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({
      establishmentId: parseInt(document.getElementById('webhookEstId').value),
      callbackUrl:     document.getElementById('webhookUrl').value.trim()
    })
  });
  const data = await res.json();
  if (res.ok || res.status === 201) {
    status.style.color = 'var(--green)';
    status.textContent = 'Webhook added. Secret (save this — shown once): ' + data.secret;
    document.getElementById('webhookEstId').value = '';
    document.getElementById('webhookUrl').value   = '';
    loadWebhooks();
  } else {
    status.style.color = 'var(--red)';
    status.textContent = data.error?.message || 'Failed.';
  }
});

async function deleteWebhook(id) {
  if (!confirm('Remove this webhook?')) return;
  await fetch('/v1/business/webhooks/' + id, { method: 'DELETE' });
  loadWebhooks();
}
```

JS — the startup IIFE originally toggled `webhookLocked`/`webhookSection` alongside `freeActions`/
`proActions` and called `loadWebhooks()`. Combined original IIFE tier-branch (also restores section 5):
```js
if (me.tier === 'pro') {
  document.getElementById('proActions').style.display     = 'block';
  document.getElementById('addForm').style.display        = 'flex';
  document.getElementById('navFreeHint').style.display    = 'none';
  document.getElementById('webhookSection').style.display = 'block';
} else {
  document.getElementById('freeActions').style.display    = 'block';
  document.getElementById('addForm').style.display        = 'none';
  document.getElementById('navFreeHint').style.display    = 'flex';
  document.getElementById('cvrFreeNote').style.display    = 'block';
  document.getElementById('webhookLocked').style.display  = 'block';
}

if (new URLSearchParams(location.search).get('upgraded') === '1') {
  currentTier = 'pro';
  document.getElementById('upgradedBanner').style.display  = 'block';
  document.getElementById('freeActions').style.display     = 'none';
  document.getElementById('proActions').style.display      = 'block';
  document.getElementById('addForm').style.display          = 'flex';
  document.getElementById('navFreeHint').style.display      = 'none';
  document.getElementById('cvrFreeNote').style.display      = 'none';
  document.getElementById('webhookLocked').style.display    = 'none';
  document.getElementById('webhookSection').style.display  = 'block';
  document.getElementById('tierBadge').textContent         = 'Pro account';
  history.replaceState(null, '', '/dashboard.html');
  loadWebhooks();
} else if (me.tier === 'pro') {
  loadWebhooks();
}

loadLocations();
loadApiKey();  // see section 7
```

**Backend prerequisite:** none — the webhook backend (`/v1/business/webhooks` CRUD, Hangfire delivery
job, HMAC-SHA256 signing, score-change detection via MERGE OUTPUT) is complete and was untouched by
this pass. Restoring the UI above is sufficient to make Webhooks fully functional again.

---

## 7. Dashboard — Developer API tab

**File:** `dashboard.html`

Tab button (was the third button of the `.dash-tabs` bar):
```html
<button class="dash-tab" data-tab="apikey">Developer API</button>
```

Full panel:
```html
<!-- ── Developer API tab ── -->
<div id="tab-apikey" class="tab-panel" style="display:none">
  <p style="font-size:0.9rem;color:var(--text-muted);margin-bottom:16px">
    Use your API key with the <code>X-Api-Key</code> header to access the REST API.
    <a href="/developers.html" target="_blank" rel="noopener">API docs →</a>
  </p>
  <div id="apikeyStatus" style="margin-bottom:12px;font-size:0.9rem"></div>
  <div id="apikeyInfo" style="display:none;margin-bottom:16px;padding:14px 16px;border:1px solid var(--border);border-radius:6px;background:var(--surface)">
    <div style="font-size:0.8rem;color:var(--text-muted);margin-bottom:4px">Active key · tier: <strong><span id="apikeyTier"></span></strong></div>
    <div style="font-size:0.8rem;color:var(--text-muted)">Created: <span id="apikeyCreatedAt"></span></div>
  </div>
  <div id="apikeyNoKey" style="display:none;margin-bottom:16px;font-size:0.875rem;color:var(--text-muted)">No active API key.</div>
  <div id="apikeyNewKey" style="display:none;margin-bottom:16px;padding:14px 16px;border:1px solid #6ee7b7;border-radius:6px;background:#d1fae5;color:#065f46">
    <strong>New key — save this, it won't be shown again:</strong><br>
    <code id="apikeyPlaintext" style="font-family:var(--font-mono);word-break:break-all;font-size:0.9rem"></code>
  </div>
  <div style="display:flex;gap:10px">
    <button class="btn-primary" onclick="generateApiKey()">Generate new key</button>
    <button class="btn-secondary" id="revokeBtn" style="display:none" onclick="revokeApiKey()">Revoke key</button>
  </div>
</div>
```

JS — three functions (were directly above `var allLocations = []` / after `loadLocations()`'s
original position, right after the startup IIFE):
```js
async function loadApiKey() {
  const res  = await fetch('/v1/business/apikey');
  const data = await res.json();
  if (data.hasKey) {
    document.getElementById('apikeyInfo').style.display  = 'block';
    document.getElementById('apikeyNoKey').style.display = 'none';
    document.getElementById('revokeBtn').style.display   = 'inline-block';
    document.getElementById('apikeyTier').textContent    = data.tier;
    document.getElementById('apikeyCreatedAt').textContent = formatDate(data.createdAt.slice(0, 10).replace(/-/g, '-'));
  } else {
    document.getElementById('apikeyInfo').style.display  = 'none';
    document.getElementById('apikeyNoKey').style.display = 'block';
    document.getElementById('revokeBtn').style.display   = 'none';
  }
}

async function generateApiKey() {
  const hasKey = document.getElementById('revokeBtn').style.display !== 'none';
  if (hasKey && !confirm('This will invalidate your existing key. Continue?')) return;
  document.getElementById('apikeyNewKey').style.display = 'none';
  const status = document.getElementById('apikeyStatus');
  status.textContent = '';
  const res  = await fetch('/v1/business/apikey/generate', { method: 'POST' });
  const data = await res.json();
  if (res.ok) {
    document.getElementById('apikeyPlaintext').textContent = data.key;
    document.getElementById('apikeyNewKey').style.display  = 'block';
    loadApiKey();
  } else {
    status.style.color = 'var(--red)';
    status.textContent = data.error?.message || 'Failed.';
  }
}

async function revokeApiKey() {
  if (!confirm('Revoke your API key? All requests using it will stop working immediately.')) return;
  const res = await fetch('/v1/business/apikey', { method: 'DELETE' });
  if (res.ok) loadApiKey();
}
```

Plus the `loadApiKey();` call at the end of the startup IIFE (see section 6's combined IIFE listing
above — it was the last line before `})();`).

**Backend prerequisite:** none — `/v1/business/apikey` (generate/revoke/status) is complete and was
untouched. Restoring the UI above is sufficient.

---

## 8. Dashboard — the tab bar itself

**File:** `dashboard.html`

```html
<div class="dash-tabs">
  <button class="dash-tab active" data-tab="locations">Locations</button>
  <button class="dash-tab" data-tab="webhooks">Webhooks</button>
  <button class="dash-tab" data-tab="apikey">Developer API</button>
</div>
```
JS — the click handler that switched panels:
```js
document.querySelectorAll('.dash-tab').forEach(function (btn) {
  btn.addEventListener('click', function () {
    document.querySelectorAll('.dash-tab').forEach(function (b) { b.classList.remove('active'); });
    document.querySelectorAll('.tab-panel').forEach(function (p) { p.style.display = 'none'; });
    btn.classList.add('active');
    document.getElementById('tab-' + btn.dataset.tab).style.display = 'block';
  });
});
```

**Restore:** only needed if restoring section 6 and/or 7 — with just Locations, a one-button tab bar
serves no purpose, so it was dropped along with them. If either Webhooks or Developer API comes back,
restore the tab bar (with only the relevant buttons) and this click handler together.

---

## 9. "Upgrade to Pro" wording in tier-gate messages (dashboard + backend API)

Three places told a user to do something ("Upgrade to Pro") that isn't currently possible — softened
to plain factual statements, not removed as UI (the gate itself is a real, correct tier limit, only
the dead call-to-action wording changed).

**a) Dashboard — "Add by Navnelbnr" hint for Free-tier accounts**
**File:** `dashboard.html`, `#navFreeHint` (shown in place of the Navnelbnr form for Free tier)
```html
<!-- before -->
<span style="font-size:0.8rem;color:var(--text-muted)">Upgrade to add individual locations.</span>
<!-- after -->
<span style="font-size:0.8rem;color:var(--text-muted)">Coming soon on Pro.</span>
```

**b) Backend — CVR-limit-reached error (Free tier hits its 1-CVR cap)**
**File:** `src/SmilrApi.Api/Endpoints/BusinessEndpoints.cs` (`cvr_limit_reached` error, free-tier branch)
```csharp
// before
Error("cvr_limit_reached", "Free accounts are limited to one CVR. Upgrade to Pro to add more."),
// after
Error("cvr_limit_reached", "Free accounts are limited to one CVR."),
```
(The Pro-tier branch's own `cvr_limit_reached` message, which points to Enterprise via
`info@smilrhq.dk`, was left as-is — that's the existing "Contact us" pattern, not a dead CTA.)

**c) Backend — Developer API daily rate-limit response**
**File:** `src/SmilrApi.Api/Program.cs` (`RateLimiter` `OnRejected` handler)
```csharp
// before
"""{"error":{"code":"rate_limit_exceeded","message":"Daily request limit reached. Upgrade to Pro for higher limits."}}"""
// after
"""{"error":{"code":"rate_limit_exceeded","message":"Daily request limit reached."}}"""
```

**Restore:** re-append the removed clause to each message once Pro is purchasable again. No backend
logic changed in any of the three — only the string literals.

---

## Not in scope for this list

While verifying this pass, a genuinely **pre-existing, unrelated bug** was found and fixed in
`dashboard.html`: the `embedModal`/`historyModal` click-outside-to-close listeners were registered
before those `<div>`s existed in the DOM (they're declared near the end of `<body>`, after the
`<script>` block), throwing a `TypeError` on every page load. Fixed by wrapping both listener
registrations in a `DOMContentLoaded` callback. This isn't a removed feature — nothing to bring back,
noted here only so it isn't mistaken for part of this list later.

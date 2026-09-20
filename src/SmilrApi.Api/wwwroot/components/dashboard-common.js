// Helpers shared by the logged-in dashboard pages (overview, locations, developer-api).
// Requires /components/nav.js (window.SmilrSession) to be loaded first.

function esc(str) {
  return String(str).replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;');
}

function formatDate(iso) {
  var p = String(iso).slice(0, 10).split('-');
  return p[2] + '/' + p[1] + '/' + p[0].slice(2);
}

// Common page bootstrap: resolves the session, fills the hero, reveals #dashContent (or #authError when
// logged out). Returns the /me payload, or null when there is no session. `tier` is overridable so the
// post-checkout ?upgraded=1 landing can show Pro before the webhook has flipped the account.
async function initDashboardPage(options) {
  var me = await window.SmilrSession.me();
  if (!me) {
    document.getElementById('authError').style.display = 'block';
    return null;
  }
  var tier = (options && options.tier) || me.tier;
  applyTier(tier);
  document.getElementById('welcomeMsg').textContent = 'Welcome back, ' + me.companyName;
  document.getElementById('dashContent').style.display = 'block';
  return me;
}

function applyTier(tier) {
  document.getElementById('tierBadge').textContent = tier === 'pro' ? 'Pro account' : 'Free account';
  var actions = document.getElementById('proActions');
  if (actions) actions.style.display = tier === 'pro' ? 'block' : 'none';
}

async function openPortal() {
  var status = document.getElementById('portalStatus');
  status.textContent = 'Opening…';
  try {
    var res  = await fetch('/v1/business/portal', { method: 'POST' });
    var data = await res.json();
    if (res.ok) {
      status.textContent = '';
      location.href = data.url;
    } else {
      status.textContent = (data.error && data.error.message) || 'Failed to open portal.';
    }
  } catch (e) {
    status.textContent = 'Network error.';
  }
}

window.addEventListener('pageshow', function () {
  var status = document.getElementById('portalStatus');
  if (status) status.textContent = '';
});

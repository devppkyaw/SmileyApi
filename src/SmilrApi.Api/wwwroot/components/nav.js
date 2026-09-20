// Shared navigation + session helper.
//
//  - window.SmilrSession.me()   one memoized GET /v1/business/me per page load (null when logged out),
//                               shared by <dash-nav>, the public-page auth link and page scripts so
//                               nothing fetches it twice.
//  - <dash-nav active="overview|locations|developer-api">   header nav for the logged-in dashboard pages.
//  - On public pages (anything with #navAuthLink) the "Log in" link becomes "Dashboard →" when a
//    session exists — this used to be a copy-pasted inline script on every page.
(function () {
  var mePromise = null;

  function me() {
    if (!mePromise) {
      mePromise = fetch('/v1/business/me')
        .then(function (r) { return r.ok ? r.json() : null; })
        .catch(function () { return null; });
    }
    return mePromise;
  }

  async function logout() {
    await fetch('/v1/business/logout', { method: 'POST' });
    location.href = '/';
  }

  window.SmilrSession = { me: me, logout: logout };

  function esc(str) {
    return String(str).replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;');
  }

  class DashNav extends HTMLElement {
    connectedCallback() {
      var active = this.getAttribute('active');
      function link(key, href, label, hidden) {
        return '<a href="' + href + '" class="nav-link' + (active === key ? ' is-active' : '') + '"' +
          (active === key ? ' aria-current="page"' : '') +
          (hidden ? ' id="dashNavApiLink" style="display:none"' : '') + '>' + label + '</a>';
      }
      this.style.display = 'block';
      this.innerHTML =
        '<nav class="nav">' +
          '<a href="/" class="nav-logo" style="text-decoration:none">Smilr<span class="accent">HQ</span></a>' +
          '<div class="nav-links">' +
            link('overview', '/overview.html', 'Overview') +
            link('locations', '/locations.html', 'Locations') +
            link('developer-api', '/developer-api.html', 'Developer API', true) +
            '<span id="navEmail" class="nav-email"></span>' +
            '<button type="button" id="navSignOut" class="nav-link" style="background:none;border:none;cursor:pointer;color:inherit">Sign out</button>' +
          '</div>' +
        '</nav>';

      this.querySelector('#navSignOut').addEventListener('click', logout);

      var root = this;
      me().then(function (m) {
        if (!m) return;
        root.querySelector('#navEmail').textContent = m.email;
        // Developer API is Pro/Enterprise only (or a grandfathered Free account that has a key) —
        // /me resolves that so this doesn't need its own /apikey call.
        if (m.canUseDeveloperApi) root.querySelector('#dashNavApiLink').style.display = '';
      });
    }
  }
  customElements.define('dash-nav', DashNav);

  // Public pages: swap "Log in" for "Dashboard →" when a session exists.
  document.addEventListener('DOMContentLoaded', function () {
    var authLink = document.getElementById('navAuthLink');
    if (!authLink) return;
    me().then(function (m) {
      if (!m) return;
      authLink.href = '/overview.html';
      authLink.textContent = 'Dashboard →';
    });
  });
})();

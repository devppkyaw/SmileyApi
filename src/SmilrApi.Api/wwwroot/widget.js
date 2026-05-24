(function () {
  'use strict';

  var script = document.currentScript;
  if (!script) return;

  var navnelbnr = script.getAttribute('data-navnelbnr');
  if (!navnelbnr) return;

  var origin;
  try { origin = new URL(script.src).origin; } catch (e) { return; }

  var SCORES = {
    1: 'Sm1bg',
    2: 'Sm3bg',
    3: 'Sm3bg',
    4: 'Sm4bg'
  };

  fetch(origin + '/widget/score?navnelbnr=' + encodeURIComponent(navnelbnr))
    .then(function (r) { return r.ok ? r.json() : Promise.reject(); })
    .then(function (data) {
      var imgSrc, altText;
      if (!data.score) {
        imgSrc  = origin + '/Smiley_figurer/150/kontrolpaaVej.png';
        altText = 'Kontrol på vej';
      } else if (data.displayMode === 'document') {
        imgSrc  = origin + '/Smiley_figurer/150/engroIcon.png';
        altText = 'Engros kontrol';
      } else {
        imgSrc  = origin + '/Smiley_figurer/150/' + (SCORES[data.score] || 'Sm4bg') + '.jpg';
        altText = 'Smilr';
      }

      var dateStr = '';
      if (data.scoreDate && data.score) {
        var parts = String(data.scoreDate).split('-');
        if (parts.length === 3) dateStr = parts[2] + '/' + parts[1] + '/' + parts[0].slice(2);
      }

      var link = document.createElement('a');
      link.href   = data.reportUrl || '#';
      link.target = '_blank';
      link.rel    = 'noopener noreferrer';
      link.setAttribute('style', [
        'display:inline-block',
        'background:#fef3c7',
        'border:2px solid #e9c46a',
        'border-radius:8px',
        'padding:8px 10px',
        'text-align:center',
        'text-decoration:none',
        'line-height:1.2',
        'box-shadow:0 2px 6px rgba(0,0,0,0.10)'
      ].join(';'));

      link.innerHTML =
        '<img src="' + esc(imgSrc) + '" alt="' + esc(altText) + '"' +
        ' style="width:64px;height:64px;object-fit:contain;display:block;margin:0 auto;mix-blend-mode:multiply" />' +
        (dateStr
          ? '<div style="font-size:0.7rem;color:#3a3a2a;margin-top:5px;white-space:nowrap">' + dateStr + '</div>'
          : '');

      // if (data.history && data.history.length >= 2) {
      //   link.appendChild(buildSparkline(data.history));
      // }

      script.parentNode.insertBefore(link, script);
    })
    .catch(function () {});

  // Renders a small SVG polyline from newest-first history array.
  // Score 1 (best) → top of chart, score 4 (worst) → bottom.
  function buildSparkline(history) {
    var pts = history.slice().reverse(); // oldest → newest left → right
    var W = 60, H = 20, pad = 3;
    var xStep = pts.length > 1 ? (W - pad * 2) / (pts.length - 1) : 0;
    var points = pts.map(function (p, i) {
      var x = pad + i * xStep;
      var y = pad + (p.score - 1) / 3 * (H - pad * 2);
      return x.toFixed(1) + ',' + y.toFixed(1);
    }).join(' ');

    var ns = 'http://www.w3.org/2000/svg';
    var svg = document.createElementNS(ns, 'svg');
    svg.setAttribute('width', W);
    svg.setAttribute('height', H);
    svg.setAttribute('viewBox', '0 0 ' + W + ' ' + H);
    svg.setAttribute('style', 'display:block;margin:5px auto 0');

    var polyline = document.createElementNS(ns, 'polyline');
    polyline.setAttribute('points', points);
    polyline.setAttribute('fill', 'none');
    polyline.setAttribute('stroke', '#e9c46a');
    polyline.setAttribute('stroke-width', '2');
    polyline.setAttribute('stroke-linecap', 'round');
    polyline.setAttribute('stroke-linejoin', 'round');

    svg.appendChild(polyline);
    return svg;
  }

  function esc(str) {
    return String(str)
      .replace(/&/g, '&amp;')
      .replace(/</g, '&lt;')
      .replace(/>/g, '&gt;')
      .replace(/"/g, '&quot;');
  }
}());

(function () {
  'use strict';

  var script = document.currentScript;
  if (!script) return;

  var params = new URLSearchParams(script.src.split('?')[1] || '');
  var cvr = params.get('cvr');
  if (!cvr) return;

  var origin;
  try { origin = new URL(script.src).origin; } catch (e) { return; }

  var SCORES = {
    1: 'Sm1bg',
    2: 'Sm3bg',
    3: 'Sm3bg',
    4: 'Sm4bg'
  };

  fetch(origin + '/widget/score?cvr=' + encodeURIComponent(cvr))
    .then(function (r) { return r.ok ? r.json() : Promise.reject(); })
    .then(function (data) {
      var imgFile = SCORES[data.score] || 'Sm4bg';
      var imgSrc  = origin + '/Smiley_figurer/150/' + imgFile + '.jpg';

      var dateStr = '';
      if (data.lastInspectedOn) {
        var parts = String(data.lastInspectedOn).split('-');
        if (parts.length === 3) dateStr = parts[2] + '.' + parts[1] + '.' + parts[0];
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
        '<img src="' + esc(imgSrc) + '" alt="Smiley"' +
        ' style="width:64px;height:64px;object-fit:contain;display:block;margin:0 auto;mix-blend-mode:multiply" />' +
        (dateStr
          ? '<div style="font-size:0.7rem;color:#3a3a2a;margin-top:5px;white-space:nowrap">' + dateStr + '</div>'
          : '');

      script.parentNode.insertBefore(link, script);
    })
    .catch(function () {});

  function esc(str) {
    return String(str)
      .replace(/&/g, '&amp;')
      .replace(/</g, '&lt;')
      .replace(/>/g, '&gt;')
      .replace(/"/g, '&quot;');
  }
}());

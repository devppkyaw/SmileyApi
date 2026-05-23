// Centralized smiley icon resolver — matches widget.js priority.
// type: virksomhedsType ('Engros', 'Detail') or displayMode ('document', 'smiley')
// score: numeric 1-4 or null/undefined
// size: 32 (table thumbnails) or 150 (modal/badge preview)
function smilrIcon(type, score, size) {
  if (!score)
    return { src: '/Smiley_figurer/150/kontrolpaaVej.png', alt: 'Kontrol på vej' };
  if (type === 'Engros' || type === 'document')
    return { src: '/Smiley_figurer/150/engroIcon.png', alt: 'Engros' };
  var sfx  = size === 32 ? 'cg' : 'bg';
  var dir  = size === 32 ? '32' : '150';
  var base = ({ 1: 'Sm1', 2: 'Sm3', 3: 'Sm3', 4: 'Sm4' })[score] || 'Sm4';
  return { src: '/Smiley_figurer/' + dir + '/' + base + sfx + '.jpg', alt: String(score) };
}

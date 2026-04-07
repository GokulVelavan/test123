// ?? Scroll to login ??????????????????????????????????????????
function scrollToLogin() {
  document.getElementById('loginSection')?.scrollIntoView({ behavior: 'smooth', block: 'center' });
}

// ?? Toggle password visibility ???????????????????????????????
function togglePwd() {
  const inp = document.getElementById('passwordInput');
  const eye = document.getElementById('eyeIcon');
  if (!inp) return;
  if (inp.type === 'password') {
    inp.type = 'text';
    if (eye) eye.textContent = '\uD83D\uDE48'; // ??
  } else {
    inp.type = 'password';
    if (eye) eye.textContent = '\uD83D\uDC41'; // ??
  }
}

// ?? MFA screen ???????????????????????????????????????????????
function showMfa() {
  document.getElementById('loginBody')?.classList.add('hidden');
  document.getElementById('mfaScreen')?.classList.add('visible');
  setTimeout(() => document.getElementById('otp0')?.focus(), 100);
}

function backToLogin() {
  document.getElementById('mfaScreen')?.classList.remove('visible');
  document.getElementById('loginBody')?.classList.remove('hidden');
  document.querySelectorAll('.otp-input').forEach(i => i.value = '');
  const e = document.getElementById('mfaError');
  const s = document.getElementById('mfaSuccess');
  if (e) e.style.display = 'none';
  if (s) s.style.display = 'none';
}

function handleMFA() {
  const code = Array.from({ length: 6 }, (_, i) => document.getElementById('otp' + i)?.value ?? '').join('');
  const errEl = document.getElementById('mfaError');
  const okEl  = document.getElementById('mfaSuccess');
  if (errEl) errEl.style.display = 'none';
  if (okEl)  okEl.style.display  = 'none';
  if (code.length < 6) return;
  if (okEl) okEl.style.display = 'flex';
}

// OTP input auto-advance
document.querySelectorAll('.otp-input').forEach((inp, i) => {
  inp.addEventListener('input', () => {
    inp.value = inp.value.replace(/\D/g, '').slice(0, 1);
    if (inp.value && i < 5) document.getElementById('otp' + (i + 1))?.focus();
    const allFilled = Array.from({ length: 6 }, (_, j) => document.getElementById('otp' + j)?.value).every(v => v);
    if (allFilled) setTimeout(handleMFA, 300);
  });
  inp.addEventListener('keydown', e => {
    if (e.key === 'Backspace' && !inp.value && i > 0)
      document.getElementById('otp' + (i - 1))?.focus();
  });
});

// ?? Live ticker � populated from api/logs/live ????????????????
const _TICKER_FALLBACK = [
  'Platform ready � connecting to live event stream\u2026',
];

function _escHtml(str) {
  return str
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;');
}

function buildTicker(messages) {
  const track = document.getElementById('tickerTrack');
  if (!track) return;
  const list = (messages && messages.length) ? messages : _TICKER_FALLBACK;
  const doubled = [...list, ...list];
  track.innerHTML = doubled.map(m =>
    '<span style="padding:0 40px;color:var(--text-2);font-size:11.5px;font-family:var(--font-mono)">' + _escHtml(m) + '</span>'
  ).join('');
}

async function fetchTicker() {
  try {
    const r = await fetch('/api/logs/live?lastSeconds=120&limit=20');
    if (!r.ok) { buildTicker(null); return; }
    const data = await r.json();
    if (!Array.isArray(data) || !data.length) { buildTicker(null); return; }
    const messages = data.map(function (e) {
      var host = e.host || e.hostname || 'unknown';
      var prog = e.program ? ' [' + e.program + ']' : '';
      var msg  = e.message || '';
      return host + prog + ' \u2014 ' + msg;
    });
    buildTicker(messages);
  } catch (_) {
    buildTicker(null);
  }
}

// ?? Stats bar � live refresh from API ?????????????????????????
function fmtEvents(n) {
  if (n >= 1000000) return (n / 1000000).toFixed(1) + 'M';
  if (n >= 1000)    return (n / 1000).toFixed(1) + 'K';
  return String(n);
}
function fmtRatio(r) { return r > 0 ? r.toFixed(1) + ':1' : '\u2014'; }
function fmtEps(n)   { return n > 0 ? n.toLocaleString() : '\u2014'; }

async function refreshStats() {
  try {
    var results = await Promise.all([
      fetch('/api/logs/dashboard/summary'),
      fetch('/api/sources/summary'),
      fetch('/api/settings/storage'),
    ]);
    var sumRes = results[0];
    var srcRes = results[1];
    var stgRes = results[2];

    if (sumRes.ok) {
      var s = await sumRes.json();
      var epsEl = document.getElementById('statEps');
      var evEl  = document.getElementById('statEvents');
      if (epsEl && s.currentEps  != null) epsEl.textContent = fmtEps(s.currentEps);
      if (evEl  && s.totalEventsToday != null) evEl.textContent = fmtEvents(s.totalEventsToday);
    }

    if (srcRes.ok) {
      var s2 = await srcRes.json();
      var srcEl = document.getElementById('statSources');
      // LogSourceSummaryDto: onlineCount (was: online)
      if (srcEl && s2.onlineCount != null) srcEl.textContent = s2.onlineCount;
    }

    if (stgRes.ok) {
      var s3 = await stgRes.json();
      var ratioEl = document.getElementById('statCompression');
      if (ratioEl && s3.compressionRatio != null) ratioEl.textContent = fmtRatio(s3.compressionRatio);
    }
  } catch (_) {
    // silently ignore � server-rendered initial values remain visible
  }
}

// ?? Animate counters on scroll ????????????????????????????????
function animateCounters() {
  document.querySelectorAll('.stat-number').forEach(function (el, i) {
    el.style.opacity = '0';
    el.style.transform = 'translateY(10px)';
    setTimeout(function () {
      el.style.transition = 'all 0.6s ease';
      el.style.opacity = '1';
      el.style.transform = 'translateY(0)';
    }, 100 + i * 80);
  });
}

var statsEl = document.querySelector('.stats-inner');
if (statsEl) {
  var obs = new IntersectionObserver(function (entries) {
    if (entries[0].isIntersecting) { animateCounters(); obs.disconnect(); }
  }, { threshold: 0.3 });
  obs.observe(statsEl);
}

// ?? Feature card reveal ???????????????????????????????????????
var cardObs = new IntersectionObserver(function (entries) {
  entries.forEach(function (e, i) {
    if (e.isIntersecting) {
      var el = e.target;
      el.style.opacity = '0';
      el.style.transform = 'translateY(20px)';
      setTimeout(function () {
        el.style.transition = 'all 0.5s ease';
        el.style.opacity = '1';
        el.style.transform = 'translateY(0)';
      }, 80 * i);
      cardObs.unobserve(el);
    }
  });
}, { threshold: 0.1 });
document.querySelectorAll('.feature-card').forEach(function (c) { cardObs.observe(c); });

// ?? Init ??????????????????????????????????????????????????????
document.addEventListener('DOMContentLoaded', function () {
  // Populate ticker from live API
  fetchTicker();

  // Refresh stats (server-rendered values shown immediately; kept fresh by polling)
  refreshStats();
  setInterval(refreshStats, 30000);

  // Refresh ticker every 2 minutes
  setInterval(fetchTicker, 120000);
});

// Connect 4 Hoops Arcade — browser interop. Audio is fully implemented in Phase 6.
window.ArcadeAudio = (function () {
  const cache = {};
  let sfxVol = 0.8, voiceVol = 0.6, musicVol = 0.7, muted = false;
  let music = null;
  const lastPlayed = {};

  // Voice channel is SERIAL: only one voice plays at a time so lines never talk over each other.
  // `voiceEl` is the one currently playing; `nextVoice` is at most ONE pending key — a newer
  // request replaces the pending one, so rapid events (fast turns) collapse to the latest line
  // instead of piling up a backlog that would play stale.
  let voiceEl = null;
  let nextVoice = null;
  let afterVoiceSfx = null;   // an SFX to fire once the voice queue fully drains (e.g. win cheer)

  // `key` is the path under wwwroot/audio/, INCLUDING subdir, e.g. "game/chip-drop.mp3".
  function url(key) { return 'audio/' + key; }

  // POOL, not clone-per-play. Cloning a fresh <audio> on every sound let elements accumulate and
  // iOS frees them poorly → growing jank/freezes over a session. Instead each key keeps a small
  // ring of reusable elements; we round-robin through them. Total elements are bounded (≈ keys ×
  // POOL_MAX) and reused forever, so `audCreated` levels off instead of climbing without limit.
  const POOL_MAX = 3;
  const pool = {};       // key -> [HTMLAudioElement]
  const poolNext = {};   // key -> next index to reuse
  let audCreated = 0;    // probe: total elements ever created (now caps out)
  function getEl(key) {
    let arr = pool[key];
    if (!arr) { arr = pool[key] = []; poolNext[key] = 0; }
    if (arr.length < POOL_MAX) {
      const el = arr.length === 0 ? load(key) : new Audio(url(key));  // first reuses the cached/preloaded one
      audCreated++; arr.push(el);
      return el;
    }
    const el = arr[poolNext[key]];
    poolNext[key] = (poolNext[key] + 1) % arr.length;
    return el;
  }

  // Hard-stop ALL audio (voices, queued voices, deferred SFX, lingering cheers, music). Called when
  // a new round starts or the player leaves the game, so nothing bleeds into the next screen.
  function stopAllAudio() {
    for (const key in pool) for (const a of pool[key]) { try { a.pause(); a.currentTime = 0; } catch {} }
    voiceEl = null; nextVoice = null; afterVoiceSfx = null;
    if (music) { try { music.pause(); } catch {} music = null; }
  }

  function sfxNow(key) {
    try {
      const a = getEl(key);
      try { a.pause(); a.currentTime = 0; } catch {}
      a.volume = sfxVol;
      a.play().catch(() => {});
    } catch (e) { console.warn('[ArcadeAudio] sfx failed', key, e); }
  }

  function load(key) {
    if (!cache[key]) {
      const a = new Audio(url(key));
      a.addEventListener('error', () => console.warn('[ArcadeAudio] missing:', key));
      cache[key] = a;
    }
    return cache[key];
  }

  function startVoice(key) {
    try {
      voiceEl = getEl(key);
      try { voiceEl.pause(); voiceEl.currentTime = 0; } catch {}
      voiceEl.volume = voiceVol;
      const advance = () => {
        voiceEl = null;
        const n = nextVoice; nextVoice = null;
        if (n) { startVoice(n); }
        else if (afterVoiceSfx) { const k = afterVoiceSfx; afterVoiceSfx = null; sfxNow(k); }
      };
      // Property assignment (not addEventListener) so reusing a pooled element never stacks handlers.
      voiceEl.onended = advance;
      voiceEl.onerror = advance;
      voiceEl.play().catch(advance);
    } catch (e) { console.warn('[ArcadeAudio] voice failed', key, e); voiceEl = null; }
  }

  function stopVoice() {
    if (voiceEl) { try { voiceEl.pause(); } catch {} voiceEl = null; }
    nextVoice = null;
    afterVoiceSfx = null;
  }

  // Universal UI click feedback: any menu/setup/settings/sensor button plays the click sound.
  // In-game board controls (drop arrows, top bar) are excluded via .game-screen — they have their
  // own game sounds (chip-drop, etc.). The splash "toca para comenzar" plays its click in C#.
  document.addEventListener('click', (e) => {
    if (muted) return;
    const t = e.target;
    const btn = t && t.closest ? t.closest('button') : null;
    if (!btn || btn.closest('.game-screen')) return;
    sfxNow('ui/button-click.mp3');
  });

  return {
    init() { /* called after first user gesture; audio context unlocks via subsequent plays */ },
    preload(keys) { (keys || []).forEach(load); },
    playSfx(key, cooldownMs) {
      if (muted) return;
      const now = Date.now();
      if (cooldownMs && lastPlayed[key] && now - lastPlayed[key] < cooldownMs) return;
      lastPlayed[key] = now;
      sfxNow(key);
    },
    // Defer an SFX until the voice queue is empty (so a win cheer doesn't talk over the lines).
    playSfxAfterVoice(key) {
      if (muted) return;
      if (voiceEl || nextVoice) afterVoiceSfx = key;
      else sfxNow(key);
    },
    // interrupt=true stops the current voice and clears the pending one, then plays `key` now
    // (used for big moments like victory so a lingering turn line can't talk over the fanfare).
    playVoice(key, interrupt) {
      if (muted) return;
      if (interrupt) stopVoice();
      if (!voiceEl) startVoice(key);
      else nextVoice = key;   // replace any pending → only the most recent line plays next
    },
    playMusic(key, loop) {
      if (muted) return;
      try { this.stopMusic(); music = load(key).cloneNode(); music.loop = loop !== false; music.volume = musicVol; music.play().catch(() => {}); }
      catch (e) { console.warn('[ArcadeAudio] music failed', key, e); }
    },
    stopMusic() { if (music) { try { music.pause(); } catch {} music = null; } },
    setVolumes(s, v, m) { sfxVol = s; voiceVol = v; musicVol = m; if (music) music.volume = m; if (voiceEl) voiceEl.volume = v; },
    mute() { muted = true; this.stopMusic(); stopVoice(); },
    unmute() { muted = false; },
    stopAll() { stopAllAudio(); },
    stats() {   // TEMPORARY perf probe
      let live = 0;
      for (const key in pool) for (const a of pool[key]) if (!a.paused && !a.ended) live++;
      return { created: audCreated, live };
    },
  };
})();

// localStorage helpers
window.ArcadeStore = {
  get(key) { try { return localStorage.getItem(key); } catch { return null; } },
  set(key, val) { try { localStorage.setItem(key, val); } catch {} },
};

// Keyboard 1-7 → .NET. Wired to a DotNetObjectReference in Phase 5.
window.ArcadeKeyboard = {
  _ref: null,
  _handler: null,
  register(dotNetRef) {
    // Idempotent: drop any prior listener so re-registration can't stack handlers.
    if (this._handler) window.removeEventListener('keydown', this._handler);
    this._ref = dotNetRef;
    this._handler = (e) => {
      if (e.key >= '1' && e.key <= '7' && this._ref) {
        this._ref.invokeMethodAsync('OnColumnKey', parseInt(e.key, 10) - 1);
      }
    };
    window.addEventListener('keydown', this._handler);
  },
  unregister() {
    if (this._handler) window.removeEventListener('keydown', this._handler);
    this._handler = null;
    this._ref = null;
  },
};

window.ArcadeFullscreen = {
  toggle() {
    if (!document.fullscreenElement) document.documentElement.requestFullscreen?.().catch(() => {});
    else document.exitFullscreen?.().catch(() => {});
  },
};

// Viewport → .NET. Debounced; only notifies when the breakpoint or orientation changes.
window.ArcadeViewport = {
  _ref: null,
  _timer: null,
  _last: null,
  _onResize: null,
  snapshot() {
    var w = window.innerWidth, h = window.innerHeight;
    var isMobile = window.matchMedia('(max-width: 767px)').matches
                || window.matchMedia('(orientation: landscape) and (max-height: 480px)').matches;
    var bp = w < 768 ? 0 : (w < 1200 ? 1 : 2);   // 0 Mobile, 1 Tablet, 2 Desktop
    return { width: w, height: h, isMobile: isMobile, breakpoint: bp, isLandscape: w >= h };
  },
  _key(s) { return s.breakpoint + '|' + s.isMobile + '|' + s.isLandscape; },
  register(dotNetRef) {
    if (this._onResize) window.removeEventListener('resize', this._onResize);
    this._ref = dotNetRef;
    this._last = this.snapshot();
    this._onResize = () => {
      if (this._timer) clearTimeout(this._timer);
      this._timer = setTimeout(() => {
        var s = this.snapshot();
        if (this._ref && this._key(s) !== this._key(this._last)) {
          this._last = s;
          this._ref.invokeMethodAsync('NotifyChanged', s.width, s.height, s.isMobile, s.breakpoint, s.isLandscape);
        } else {
          this._last = s;
        }
      }, 150);
    };
    window.addEventListener('resize', this._onResize);
    return this._last;
  },
  dispose() {
    if (this._onResize) window.removeEventListener('resize', this._onResize);
    if (this._timer) clearTimeout(this._timer);
    this._onResize = null; this._timer = null; this._ref = null;
  },
};

// --- TEMPORARY perf probe (remove after diagnosing mobile lag) -----------------
// Logs EVERY main-thread block >=50ms (the "long task" threshold the browser uses for jank),
// no matter the source — render diff, JS interop, GC, or the CPU minimax. This lets us tell
// apart "CPU-turn freeze" (one big task during thinking) from "every-interaction lag"
// (frequent smaller tasks on clicks/menus = general WASM interpretation cost).
// Watch the browser console (remote-inspect the phone, or read window.ArcadePerf.dump()).
window.ArcadePerf = (function () {
  const tasks = [];
  let total = 0, last = 0;

  // On-screen readout so the numbers are legible ON THE PHONE (no desktop console needed).
  // Shows: n = how many main-thread blocks, max = worst freeze, last = most recent.
  // A single huge "max" during the CPU's turn => minimax freeze. Many smaller ones on every
  // tap/menu => general WASM/render lag. Tap the box to reset counters.
  let el = null;
  function ensureEl() {
    if (el || !document.body) return el;
    el = document.createElement('div');
    el.style.cssText = 'position:fixed;left:6px;bottom:6px;z-index:99999;font:11px/1.3 monospace;' +
      'background:rgba(0,0,0,.78);color:#ffd23f;padding:4px 7px;border-radius:6px;' +
      'pointer-events:auto;cursor:pointer;white-space:nowrap;';
    el.title = 'perf probe — tap to reset';
    el.addEventListener('click', () => window.ArcadePerf.reset());
    document.body.appendChild(el);
    return el;
  }
  // Live DOM-node count: if this CLIMBS with every rematch, something is accumulating
  // (a real leak). If it stays flat across many games, the lag is render/GC cost, not a leak.
  function domCount() {
    const root = document.querySelector('.arc-root');
    return root ? root.getElementsByTagName('*').length : 0;
  }
  function confettiCount() { return document.querySelectorAll('[style*="confettiFall"]').length; }
  function audStats() {
    try { return window.ArcadeAudio && window.ArcadeAudio.stats ? window.ArcadeAudio.stats() : null; }
    catch { return null; }
  }
  function paint() {
    const e = ensureEl();
    if (!e) return;
    const max = tasks.length ? Math.max(...tasks) : 0;
    const a = audStats();
    e.textContent = 'jank n=' + tasks.length + ' max=' + max + 'ms last=' + last +
      'ms | dom=' + domCount() + ' cf=' + confettiCount() +
      (a ? ' aud=' + a.live + '/' + a.created : '');
  }

  // rAF-gap detector: the browser can't paint a frame while the main thread is blocked, so the
  // gap between two animation frames ≈ how long the thread was stuck. Works in EVERY browser
  // (incl. iOS Safari, where the Long Tasks API does not exist). Threshold 50ms = visible jank.
  const THRESHOLD = 50;
  // Gaps longer than this are almost certainly the page being backgrounded/throttled (rAF pauses
  // when hidden), NOT a real freeze — no Connect 4 interaction blocks the thread for seconds.
  // We drop them so a locked screen / app-switch can't masquerade as a 50-second "freeze".
  const ARTIFACT_CAP = 3000;
  let prev = performance.now();
  let lastPaint = 0;
  // Returning from background produces one huge gap; reset the baseline so it isn't counted.
  document.addEventListener('visibilitychange', () => { prev = performance.now(); });
  function tick(now) {
    const gap = now - prev;
    prev = now;
    // Refresh the dom/cf readout ~2x/sec so you can watch the node count across rematches.
    if (now - lastPaint > 500) { lastPaint = now; paint(); }
    if (gap >= THRESHOLD && gap < ARTIFACT_CAP && !document.hidden) {
      const ms = Math.round(gap);
      tasks.push(ms); total += gap; last = ms;
      // Attribute the freeze: what was on screen when the thread blocked?
      console.log('%c[perf] long frame ' + ms + 'ms', 'color:#ff9800',
        '(blocked) dom=' + domCount() + ' confetti=' + confettiCount());
      paint();
    } else if (gap >= ARTIFACT_CAP) {
      console.log('[perf] ignored ' + Math.round(gap) + 'ms gap (background/throttle, not a freeze)');
    }
    requestAnimationFrame(tick);
  }
  requestAnimationFrame(tick);
  console.log('[perf] long-frame monitor ON (flags ' + THRESHOLD + '..' + ARTIFACT_CAP + 'ms blocks; ignores background)');
  if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', paint);
  else paint();
  return {
    dump() {
      const n = tasks.length;
      if (!n) { console.log('[perf] no long tasks recorded yet'); return; }
      const sorted = [...tasks].sort((a, b) => a - b);
      console.log('[perf] long tasks: count=' + n +
        ' total=' + Math.round(total) + 'ms' +
        ' max=' + sorted[n - 1] + 'ms' +
        ' median=' + sorted[Math.floor(n / 2)] + 'ms');
    },
    reset() { tasks.length = 0; total = 0; last = 0; paint(); console.log('[perf] counters reset'); },
  };
})();

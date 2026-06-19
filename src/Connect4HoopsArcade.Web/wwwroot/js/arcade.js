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

  function sfxNow(key) {
    try { const a = load(key).cloneNode(); a.volume = sfxVol; a.play().catch(() => {}); }
    catch (e) { console.warn('[ArcadeAudio] sfx failed', key, e); }
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
      voiceEl = load(key).cloneNode();
      voiceEl.volume = voiceVol;
      const advance = () => {
        voiceEl = null;
        const n = nextVoice; nextVoice = null;
        if (n) { startVoice(n); }
        else if (afterVoiceSfx) { const k = afterVoiceSfx; afterVoiceSfx = null; sfxNow(k); }
      };
      voiceEl.addEventListener('ended', advance);
      voiceEl.addEventListener('error', advance);
      voiceEl.play().catch(advance);
    } catch (e) { console.warn('[ArcadeAudio] voice failed', key, e); voiceEl = null; }
  }

  function stopVoice() {
    if (voiceEl) { try { voiceEl.pause(); } catch {} voiceEl = null; }
    nextVoice = null;
    afterVoiceSfx = null;
  }

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

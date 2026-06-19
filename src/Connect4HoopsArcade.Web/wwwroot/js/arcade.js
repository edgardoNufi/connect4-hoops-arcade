// Connect 4 Hoops Arcade — browser interop. Audio is fully implemented in Phase 6.
window.ArcadeAudio = (function () {
  const cache = {};
  let sfxVol = 0.8, voiceVol = 0.6, musicVol = 0.7, muted = false;
  let music = null;
  const lastPlayed = {};

  // `key` is the path under wwwroot/audio/, INCLUDING subdir, e.g. "game/chip-drop.mp3".
  function url(key) { return 'audio/' + key; }

  function load(key) {
    if (!cache[key]) {
      const a = new Audio(url(key));
      a.addEventListener('error', () => console.warn('[ArcadeAudio] missing:', key));
      cache[key] = a;
    }
    return cache[key];
  }

  return {
    init() { /* called after first user gesture; preload in Phase 6 */ },
    preload(keys) { (keys || []).forEach(load); },
    playSfx(key, cooldownMs) {
      if (muted) return;
      const now = Date.now();
      if (cooldownMs && lastPlayed[key] && now - lastPlayed[key] < cooldownMs) return;
      lastPlayed[key] = now;
      try { const a = load(key).cloneNode(); a.volume = sfxVol; a.play().catch(() => {}); }
      catch (e) { console.warn('[ArcadeAudio] sfx failed', key, e); }
    },
    playVoice(key) {
      if (muted) return;
      try { const a = load(key).cloneNode(); a.volume = voiceVol; a.play().catch(() => {}); }
      catch (e) { console.warn('[ArcadeAudio] voice failed', key, e); }
    },
    playMusic(key, loop) {
      if (muted) return;
      try { this.stopMusic(); music = load(key).cloneNode(); music.loop = loop !== false; music.volume = musicVol; music.play().catch(() => {}); }
      catch (e) { console.warn('[ArcadeAudio] music failed', key, e); }
    },
    stopMusic() { if (music) { try { music.pause(); } catch {} music = null; } },
    setVolumes(s, v, m) { sfxVol = s; voiceVol = v; musicVol = m; if (music) music.volume = m; },
    mute() { muted = true; this.stopMusic(); },
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
  register(dotNetRef) {
    this._ref = dotNetRef;
    window.addEventListener('keydown', (e) => {
      if (e.key >= '1' && e.key <= '7' && this._ref) {
        this._ref.invokeMethodAsync('OnColumnKey', parseInt(e.key, 10) - 1);
      }
    });
  },
};

window.ArcadeFullscreen = {
  toggle() {
    if (!document.fullscreenElement) document.documentElement.requestFullscreen?.().catch(() => {});
    else document.exitFullscreen?.().catch(() => {});
  },
};

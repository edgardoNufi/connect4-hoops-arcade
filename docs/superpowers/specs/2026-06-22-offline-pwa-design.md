# Offline support (installable PWA) — design

**Date:** 2026-06-22
**Status:** Approved (design); pending implementation plan
**Roadmap:** Enables playing with no connectivity (e.g. taken to a field/arcade with no wifi). Builds on the
PWA metadata added with the landscape/fullscreen work.

## Problem
The game logic already runs 100% client-side (no backend; CPU AI, audio, rendering all in the browser), so
gameplay needs no network *once loaded*. But there is **no service worker**, so every launch re-fetches the
app files from the network (or relies on the unreliable HTTP cache). "Add to Home Screen" today gives
fullscreen/icon but **not** offline. The user wants to install the game once (on wifi) and then play it fully
offline — including a cold start from the home-screen icon — in a place with no internet.

## Goal
After a one-time load with internet, the installed app **launches and plays fully offline** (cold start,
airplane mode, off-grid), with audio and fonts intact.

## Hard constraint (accepted by the user)
A web app cannot exist on a device that has *never* had internet — the files must arrive once. The model is:
**install/cache once with wifi, then use offline indefinitely.** (A truly never-online distribution would
require a native app/APK — explicitly out of scope.)

## Non-goals
- No native app / APK packaging.
- No changes to `Core` or game logic.
- No background sync / online features (there is no backend).

## Decisions (from brainstorming)
- **Model:** install-once-with-wifi → then fully offline (PWA service worker).
- **Visible status:** show a small **"✓ Listo para jugar sin conexión"** toast after the first full cache, and
  an **"Hay una actualización — recarga para aplicarla"** toast when a newer version is waiting.
- **Update strategy:** silent/standard — a new version downloads in the background on the next online launch
  and applies on the next reload (surfaced by the update toast). Offline, the cached version keeps working.

## Approach
Use Blazor WebAssembly's **built-in PWA service worker** support. At publish, Blazor generates
`service-worker-assets.js` — a manifest of every published asset with integrity hashes (this is what makes
the hashed `.wasm`/`.dll` filenames cacheable without hand-maintaining a list). The published service worker
precaches those assets on `install`, cleans old caches on `activate`, and serves cache-first on `fetch` with
an `index.html` fallback for navigation.

*Rejected alternatives:* a hand-rolled service worker (fragile — must track hashed filenames manually);
Workbox (unnecessary dependency + build complexity).

## Detailed design

### 1. Enable the service worker (build)
- `Connect4HoopsArcade.Web.csproj`: add
  `<ServiceWorkerAssetsManifest>service-worker-assets.js</ServiceWorkerAssetsManifest>` and
  `<ServiceWorker Include="wwwroot\service-worker.js" PublishedContent="wwwroot\service-worker.published.js" />`.
  The publish swaps in the `.published.js` version and generates `service-worker-assets.js`.
- `wwwroot/service-worker.js` (development): a no-op (no offline caching in local dev, so iteration isn't
  fought by a cache).
- `wwwroot/service-worker.published.js` (production): based on the standard Blazor template —
  - `install` → `caches.open(...)` and `addAll` the manifest assets (filtered, see §2); `skipWaiting()` is
    **not** called (so an update waits until all tabs close / reload — standard, surfaced by the toast).
  - `activate` → delete caches that aren't the current one.
  - `fetch` → for GET navigations/assets, serve from cache, fall back to network; navigation requests fall
    back to cached `index.html`.

### 2. Precache audio + fonts (critical)
The Blazor template's default `offlineAssetsInclude` regex does **not** include `.mp3`, `.m4a`, or `.woff2`.
Without fixing this the game would open offline but with **no sound and broken fonts**. Extend the include
list in `service-worker.published.js` to cover: `.mp3`, `.m4a`, `.woff2` (the self-hosted Fredoka/Nunito
fonts), and keep the defaults (`.dll`, `.wasm`, `.html`, `.js`, `.json`, `.css`, `.woff`, `.png`, `.ico`,
`.webmanifest`, etc.). Verify after a publish that the audio files and font files appear in the generated
`service-worker-assets.js` precache set.

### 3. Registration + status toasts
A small registration script (in `index.html`, or a `window.ArcadePWA` helper in `arcade.js`) registers
`service-worker.js` and detects state to show Spanish toasts (a self-contained DOM element, independent of
Blazor so it works even before hydration):
- **First install complete** — there was no controller and the SW reached `activated`/redundant-free with the
  assets cached → toast **"✓ Listo para jugar sin conexión"**.
- **Update available** — `registration.installing`/`waiting` transitions to installed while an existing
  controller is active → toast **"Hay una actualización — recarga para aplicarla"** (tap to dismiss / reload).
The toast is small, arcade-styled, bottom-centered, auto-dismiss after a few seconds (the "ready" one) or tap
to dismiss (the "update" one).

### 4. Update flow
Standard: opening online lets the new SW download in the background; it activates on the next reload (toast
prompts the reload). Offline, the previously cached version launches and plays normally.

## Files touched
- `src/Connect4HoopsArcade.Web/Connect4HoopsArcade.Web.csproj` — service-worker items.
- `src/Connect4HoopsArcade.Web/wwwroot/service-worker.js` — dev no-op (new).
- `src/Connect4HoopsArcade.Web/wwwroot/service-worker.published.js` — production SW with extended cache
  include (new).
- `src/Connect4HoopsArcade.Web/wwwroot/index.html` — SW registration + toast wiring.
- (maybe) `src/Connect4HoopsArcade.Web/wwwroot/js/arcade.js` — `window.ArcadePWA` register/toast helper.
- (maybe) a small CSS rule for the toast (`css/app.css` or `mobile.css`).
- No `Core` / game-logic changes.

## Edge cases / honest caveats (iOS)
- iOS supports service workers in installed PWAs → offline works from the home-screen icon (iOS 11.3+).
- iOS **may evict the cache** if storage runs low or the PWA is unused for several weeks; re-opening once
  with wifi re-caches. Fine for a weekend trip.
- First load (on wifi) downloads everything at once (a bit heavier); subsequent launches are instant.
- Dev (`dotnet run`) uses the no-op SW, so local iteration is unaffected.

## Testing / verification
- `dotnet build` + existing 51 Core tests stay green (no Core changes).
- After `dotnet publish`, confirm `service-worker-assets.js` lists the audio (`audio/**.mp3/.m4a`) and font
  (`*.woff2`/`*.woff`) files.
- On device (after deploy): load once on wifi → see "✓ Listo para jugar sin conexión" → enable airplane mode
  → relaunch from the home-screen icon → game loads and plays with audio and correct fonts. Deploy a new
  build → next online launch shows the update toast; reload applies it.

## Out of scope / next
- **Tutorial / learn mode** (separate feature, next up): an in-app guided tutorial to show/play/learn,
  including a **step-back (undo)** to experiment with placements and see how the AI responds. The step-back is
  scoped to the tutorial only — NOT the main game. Gets its own brainstorm → spec → plan.

# Offline (installable PWA) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make the game installable and fully playable offline after a one-time load on wifi — including audio and fonts — using Blazor WebAssembly's built-in PWA service worker.

**Architecture:** Enable Blazor's service-worker support in the `.csproj` so publish generates `service-worker-assets.js` (a hashed manifest of every published file). A dev no-op `service-worker.js` keeps local iteration uncached; the production `service-worker.published.js` precaches all matching assets on install (with the include list extended to cover `.mp3`/`.m4a`/`.woff2`), serves cache-first, and falls back to `index.html` for navigation. A small registration script in `index.html` shows a "ready offline" toast on first install and an "update available" toast when a new version is waiting.

**Tech Stack:** Blazor WebAssembly (.NET 10), service worker (Cache Storage API), `index.html`, `dotnet publish`.

**Testing note (project convention):** Per `CLAUDE.md`, `Core` is TDD'd, UI/infra uses **build-and-verify**. This feature touches only `Web` static/build config (no `Core`), so tasks verify with `dotnet build`/`dotnet publish` output inspection + the existing 51 Core tests staying green + on-device checks. No new unit tests.

**Spec:** `docs/superpowers/specs/2026-06-22-offline-pwa-design.md`

**Confirmed asset facts (from the repo):** fonts are `.woff2` only (8 files); audio is `.mp3` (99) + `.m4a` (1).

---

## File structure
- Modify: `src/Connect4HoopsArcade.Web/Connect4HoopsArcade.Web.csproj` — add service-worker build items.
- Create: `src/Connect4HoopsArcade.Web/wwwroot/service-worker.js` — dev no-op.
- Create: `src/Connect4HoopsArcade.Web/wwwroot/service-worker.published.js` — production offline SW (precache audio+fonts).
- Modify: `src/Connect4HoopsArcade.Web/wwwroot/index.html` — register the SW + status toasts.

---

## Task 1: Enable the service worker in the project file

**Files:**
- Modify: `src/Connect4HoopsArcade.Web/Connect4HoopsArcade.Web.csproj`

- [ ] **Step 1: Add the service-worker items**

In `Connect4HoopsArcade.Web.csproj`, add to the existing `<PropertyGroup>` (after `<ImplicitUsings>enable</ImplicitUsings>`):

```xml
    <ServiceWorkerAssetsManifest>service-worker-assets.js</ServiceWorkerAssetsManifest>
```

Then add a new `<ItemGroup>` (e.g. after the existing `PackageReference` ItemGroup):

```xml
  <ItemGroup>
    <ServiceWorker Include="wwwroot\service-worker.js" PublishedContent="wwwroot\service-worker.published.js" />
  </ItemGroup>
```

- [ ] **Step 2: Commit (no build yet — the SW files are created in Tasks 2-3; the first build/publish is Task 5, once every file exists)**

```bash
git add src/Connect4HoopsArcade.Web/Connect4HoopsArcade.Web.csproj
git commit -m "build(web): enable Blazor PWA service-worker items"
```

---

## Task 2: Development service worker (no-op)

**Files:**
- Create: `src/Connect4HoopsArcade.Web/wwwroot/service-worker.js`

- [ ] **Step 1: Create the dev SW**

`wwwroot/service-worker.js`:

```js
// Development service worker: intentionally a no-op so local `dotnet run` is never served from cache
// (offline caching only happens in the published build via service-worker.published.js).
self.addEventListener('fetch', () => { });
```

- [ ] **Step 2: Syntax-check**

Run: `node --check src/Connect4HoopsArcade.Web/wwwroot/service-worker.js`
Expected: exit 0, no output.

- [ ] **Step 3: Commit**

```bash
git add src/Connect4HoopsArcade.Web/wwwroot/service-worker.js
git commit -m "feat(web): dev no-op service worker"
```

---

## Task 3: Production service worker (precache, incl. audio + fonts)

**Files:**
- Create: `src/Connect4HoopsArcade.Web/wwwroot/service-worker.published.js`

- [ ] **Step 1: Create the published SW**

`wwwroot/service-worker.published.js` (standard Blazor offline SW; the only change from the template is the
extended `offlineAssetsInclude` adding `.mp3`, `.m4a`, `.woff2` so audio and fonts work offline):

```js
// Production offline service worker. Precaches every published asset listed in the generated
// service-worker-assets.js (hashed wasm/dll names included), then serves cache-first.
// See https://aka.ms/blazor-offline-considerations
self.importScripts('./service-worker-assets.js');
self.addEventListener('install', event => event.waitUntil(onInstall(event)));
self.addEventListener('activate', event => event.waitUntil(onActivate(event)));
self.addEventListener('fetch', event => event.respondWith(onFetch(event)));

const cacheNamePrefix = 'offline-cache-';
const cacheName = `${cacheNamePrefix}${self.assetsManifest.version}`;
// Extended vs the template: .mp3 + .m4a (game audio) and .woff2 (self-hosted fonts) MUST be cached,
// or the game opens offline with no sound and broken fonts.
const offlineAssetsInclude = [ /\.dll$/, /\.pdb$/, /\.wasm/, /\.html/, /\.js$/, /\.json$/, /\.css$/, /\.woff$/, /\.woff2$/, /\.mp3$/, /\.m4a$/, /\.png$/, /\.jpe?g$/, /\.gif$/, /\.ico$/, /\.webmanifest$/, /\.blat$/, /\.dat$/ ];
const offlineAssetsExclude = [ /^service-worker\.js$/ ];

const base = "/";
const baseUrl = new URL(base, self.origin);
const manifestUrlList = self.assetsManifest.assets.map(asset => new URL(asset.url, baseUrl).href);

async function onInstall(event) {
    console.info('Service worker: Install');
    const assetsRequests = self.assetsManifest.assets
        .filter(asset => offlineAssetsInclude.some(pattern => pattern.test(asset.url)))
        .filter(asset => !offlineAssetsExclude.some(pattern => pattern.test(asset.url)))
        .map(asset => new Request(asset.url, { integrity: asset.hash, cache: 'no-cache' }));
    await caches.open(cacheName).then(cache => cache.addAll(assetsRequests));
}

async function onActivate(event) {
    console.info('Service worker: Activate');
    const cacheKeys = await caches.keys();
    await Promise.all(cacheKeys
        .filter(key => key.startsWith(cacheNamePrefix) && key !== cacheName)
        .map(key => caches.delete(key)));
}

async function onFetch(event) {
    let cachedResponse = null;
    if (event.request.method === 'GET') {
        // For navigation requests, serve cached index.html (unless the URL is itself a cached asset).
        const shouldServeIndexHtml = event.request.mode === 'navigate'
            && !manifestUrlList.some(url => url === event.request.url);
        const request = shouldServeIndexHtml ? 'index.html' : event.request;
        const cache = await caches.open(cacheName);
        cachedResponse = await cache.match(request);
    }
    return cachedResponse || fetch(event.request);
}
```

- [ ] **Step 2: Syntax-check**

Run: `node --check src/Connect4HoopsArcade.Web/wwwroot/service-worker.published.js`
Expected: exit 0, no output. (The `self.importScripts`/`self.assetsManifest` references resolve at runtime in the browser, not at parse time, so `node --check` passes.)

- [ ] **Step 3: Commit**

```bash
git add src/Connect4HoopsArcade.Web/wwwroot/service-worker.published.js
git commit -m "feat(web): production offline service worker (precaches audio + fonts)"
```

---

## Task 4: Register the SW + status toasts

**Files:**
- Modify: `src/Connect4HoopsArcade.Web/wwwroot/index.html`

- [ ] **Step 1: Add the registration script**

In `index.html`, immediately AFTER the existing `<script src="js/arcade.js"></script>` line (and before `</body>`), add:

```html
    <script>
      (function () {
        if (!('serviceWorker' in navigator)) return;
        function toast(msg, isUpdate) {
          var t = document.createElement('div');
          t.textContent = msg;
          t.style.cssText = 'position:fixed;left:50%;bottom:calc(env(safe-area-inset-bottom) + 16px);'
            + 'transform:translateX(-50%);z-index:9999;max-width:90vw;background:#161031;color:#fff;'
            + 'border:1.5px solid ' + (isUpdate ? '#ffd23f' : '#2ee86e') + ';border-radius:999px;'
            + 'padding:11px 18px;font:700 14px/1.2 system-ui,sans-serif;box-shadow:0 8px 24px rgba(0,0,0,.5);'
            + 'text-align:center;' + (isUpdate ? 'cursor:pointer;' : '');
          if (isUpdate) t.addEventListener('click', function () { location.reload(); });
          document.body.appendChild(t);
          if (!isUpdate) setTimeout(function () { t.remove(); }, 4000);
        }
        navigator.serviceWorker.register('service-worker.js').then(function (reg) {
          reg.addEventListener('updatefound', function () {
            var sw = reg.installing;
            if (!sw) return;
            sw.addEventListener('statechange', function () {
              if (sw.state !== 'installed') return;
              if (navigator.serviceWorker.controller) toast('Hay una actualización — toca para recargar', true);
              else toast('✓ Listo para jugar sin conexión', false);
            });
          });
        }).catch(function () { /* registration failed: the app still works online */ });
      })();
    </script>
```

Notes:
- On the very first registration, `updatefound` fires and `navigator.serviceWorker.controller` is `null`
  (no controller yet) → shows the green "ready offline" toast once the install (precache) completes.
- On a later deploy, an existing controller is present → shows the yellow "update available" toast.
- On an unchanged reload, `updatefound` does not fire → no toast (correct).

- [ ] **Step 2: Confirm the existing registration isn't duplicated**

Verify `index.html` does not already register a service worker elsewhere (it does not as of this plan). If a
bare `navigator.serviceWorker.register('service-worker.js')` line exists, remove it so registration happens
only once via the script above.

- [ ] **Step 3: Commit**

```bash
git add src/Connect4HoopsArcade.Web/wwwroot/index.html
git commit -m "feat(web): register service worker + offline-ready/update toasts"
```

---

## Task 5: Publish and verify the precache manifest

**Files:** none (verification only)

- [ ] **Step 1: Build + Core tests stay green**

Run: `dotnet build -v q` then `dotnet test -v q`
Expected: build `Compilación correcta.`; tests `Superado: 51`.

- [ ] **Step 2: Publish and confirm the SW + manifest are generated**

Run: `dotnet publish src/Connect4HoopsArcade.Web -c Release -o output`
Expected: `output/wwwroot/service-worker.js` exists and is the PUBLISHED content (contains
`self.assetsManifest`), and `output/wwwroot/service-worker-assets.js` exists.

- [ ] **Step 3: Confirm audio + fonts are in the precache manifest**

Run: `grep -c '\.mp3' output/wwwroot/service-worker-assets.js; grep -c '\.woff2' output/wwwroot/service-worker-assets.js; grep -c '\.m4a' output/wwwroot/service-worker-assets.js`
Expected: mp3 count is large (~99), woff2 count ~8, m4a count ~1 — confirming audio and fonts will be cached
for offline. (The manifest lists all published assets; the SW's include-regex selects these.)

- [ ] **Step 4: Confirm the published SW has the extended include**

Run: `grep -o "service-worker.published" output/wwwroot/service-worker.js >/dev/null; grep -c "m4a" output/wwwroot/service-worker.js`
Expected: the published `service-worker.js` contains the `.m4a`/`.mp3`/`.woff2` include patterns (count ≥ 1),
confirming the published (not dev no-op) worker was deployed.

- [ ] **Step 5: Commit (no-op if nothing changed; otherwise any fix from this task)**

```bash
git add -A && git commit -m "chore(web): verify offline precache includes audio + fonts" --allow-empty
```

---

## Task 6: Deploy and verify offline on device

**Files:** none (verification only)

- [ ] **Step 1: Deploy**

Push to `main` (Cloudflare auto-deploys). Confirm the corner build tag updates to the new SHA.

- [ ] **Step 2: First load on wifi**

On the phone with internet, open the live site (or relaunch the home-screen app). After it finishes loading,
the green **"✓ Listo para jugar sin conexión"** toast appears once (first install). Re-add to Home Screen if
needed so the icon launches the cached app.

- [ ] **Step 3: Verify true offline**

Enable Airplane Mode (no wifi/cell). Cold-launch the app from the home-screen icon. Expected: it loads and is
fully playable — board renders with correct fonts, CPU plays, and **audio works** (chip drop, voices).

- [ ] **Step 4: Verify update flow**

Deploy a later build while online; on the next online launch the yellow **"Hay una actualización — toca para
recargar"** toast appears; tapping it reloads into the new version. Offline, the cached version keeps working.

- [ ] **Step 5: Commit (if any verify-driven tweak was needed)**

```bash
git add -A && git commit -m "fix(web): offline PWA polish from device verification"
```

---

## Notes / out of scope
- iOS may evict the cache under storage pressure or after weeks unused; re-open once on wifi to re-cache.
- First load on wifi is heavier (caches the full runtime + 100 audio files); later launches are instant.
- Next feature (separate brainstorm): in-app **tutorial / learn mode** with a step-back (undo) to experiment
  with the AI — scoped to the tutorial only, not the main game.

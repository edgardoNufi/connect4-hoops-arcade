// Production offline service worker. Precaches every published asset listed in the generated
// service-worker-assets.js (hashed wasm/dll names included), then serves cache-first.
// See https://aka.ms/blazor-offline-considerations
// rev: 3 — clean redirected responses (Cloudflare rewrites /index.html → / ; a cached redirected
//          response can't be served to a navigation → ERR_FAILED). Plus skipWaiting/clients.claim
//          so the fixed worker takes over immediately and recovers users stuck on the broken one.
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
    await self.skipWaiting();   // don't wait for old tabs to close — needed to replace the broken worker
}

async function onActivate(event) {
    console.info('Service worker: Activate');
    const cacheKeys = await caches.keys();
    await Promise.all(cacheKeys
        .filter(key => key.startsWith(cacheNamePrefix) && key !== cacheName)
        .map(key => caches.delete(key)));
    await self.clients.claim();   // take over already-open pages immediately
}

// A response cached after following a redirect (Cloudflare rewrites /index.html → /) has
// `redirected: true`, which the browser refuses to return for a navigation. Rebuild it clean.
async function cleanIfRedirected(response) {
    if (!response || !response.redirected) return response;
    const body = await response.blob();
    return new Response(body, { status: response.status, statusText: response.statusText, headers: response.headers });
}

async function onFetch(event) {
    if (event.request.method !== 'GET') return fetch(event.request);
    // For navigation requests, serve cached index.html (unless the URL is itself a cached asset).
    const shouldServeIndexHtml = event.request.mode === 'navigate'
        && !manifestUrlList.some(url => url === event.request.url);
    const request = shouldServeIndexHtml ? 'index.html' : event.request;
    const cache = await caches.open(cacheName);
    const cachedResponse = await cache.match(request);
    if (cachedResponse) return cleanIfRedirected(cachedResponse);
    return fetch(event.request);
}

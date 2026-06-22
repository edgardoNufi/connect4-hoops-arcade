// Development service worker: intentionally a no-op so local `dotnet run` is never served from cache
// (offline caching only happens in the published build via service-worker.published.js).
self.addEventListener('fetch', () => { });

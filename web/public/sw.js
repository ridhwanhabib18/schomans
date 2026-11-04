// The service worker of SchoMans caches the app shell only, so the installed app opens without a network.
// It never touches the API, Keycloak or any other origin: grades, attendance and sign-in data are always live.
const CACHE = 'schomans-shell-v1'
const SHELL = '/'

self.addEventListener('install', (event) => {
  event.waitUntil(caches.open(CACHE).then((cache) => cache.add(SHELL)).then(() => self.skipWaiting()))
})

self.addEventListener('activate', (event) => {
  event.waitUntil(
    caches
      .keys()
      .then((keys) => Promise.all(keys.filter((key) => key !== CACHE).map((key) => caches.delete(key))))
      .then(() => self.clients.claim()),
  )
})

self.addEventListener('fetch', (event) => {
  const { request } = event
  const url = new URL(request.url)
  if (request.method !== 'GET' || url.origin !== self.location.origin || url.pathname.startsWith('/api/')) {
    return
  }

  // A page: the network first (the app may have been updated); offline, the cached shell starts the app.
  if (request.mode === 'navigate') {
    event.respondWith(
      fetch(request)
        .then((response) => {
          if (response.ok) {
            const copy = response.clone()
            void caches.open(CACHE).then((cache) => cache.put(SHELL, copy))
          }
          return response
        })
        .catch(() => caches.match(SHELL).then((shell) => shell ?? Response.error())),
    )
    return
  }

  // Build output is named by its content hash and never changes: the cache first, filled on the first visit.
  if (url.pathname.startsWith('/assets/')) {
    event.respondWith(
      caches.match(request).then(
        (hit) =>
          hit ??
          fetch(request).then((response) => {
            if (response.ok) {
              const copy = response.clone()
              void caches.open(CACHE).then((cache) => cache.put(request, copy))
            }
            return response
          }),
      ),
    )
  }
})

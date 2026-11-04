/**
 * Registers the service worker that caches the app shell (see public/sw.js). Only in a production build: in development
 * a cached shell would hide every change. A browser without service workers, or a failed registration, just means the
 * app is not available offline.
 */
export function registerServiceWorker(isProduction: boolean = import.meta.env.PROD, browser: Navigator = navigator): void {
  if (!isProduction || !('serviceWorker' in browser)) {
    return
  }

  window.addEventListener('load', () => {
    browser.serviceWorker.register('/sw.js').catch(() => undefined)
  })
}

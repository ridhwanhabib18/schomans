/** The address a QR code points to: the check-in page of this app with the code as its `t` parameter. */
export function checkInUrl(token: string, origin: string = window.location.origin): string {
  return `${origin}/attend?t=${encodeURIComponent(token)}`
}

const TOKEN = /^[0-9a-f]{32}\.[A-Za-z0-9_-]+$/

/**
 * The attendance code inside whatever a scanner read: the check-in address of SchoMans, or the bare code. Anything
 * else (another QR code, a website) gives null.
 */
export function tokenFromScan(text: string): string | null {
  const value = text.trim()
  if (TOKEN.test(value)) {
    return value
  }

  try {
    const url = new URL(value)
    const token = url.searchParams.get('t')
    return url.pathname === '/attend' && token && TOKEN.test(token) ? token : null
  } catch {
    return null
  }
}

/** Seconds left until a moment, never below zero. */
export function secondsUntil(iso: string, now: number = Date.now()): number {
  return Math.max(0, Math.ceil((new Date(iso).getTime() - now) / 1000))
}

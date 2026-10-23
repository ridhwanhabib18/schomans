export interface AccessTokenClaims {
  sub?: string
  email?: string
  name?: string
  exp?: number
  realm_access?: { roles?: string[] }
}

/**
 * Reads the claims of an access token WITHOUT verifying it. Only for showing the user's name and role in the UI;
 * the API validates every token and enforces every permission.
 */
export function decodeAccessToken(token: string): AccessTokenClaims {
  const payload = token.split('.')[1]
  if (!payload) {
    throw new Error('Not a JWT.')
  }

  const base64 = payload.replace(/-/g, '+').replace(/_/g, '/')
  const padded = base64.padEnd(base64.length + ((4 - (base64.length % 4)) % 4), '=')
  const json = new TextDecoder().decode(Uint8Array.from(atob(padded), (c) => c.charCodeAt(0)))
  return JSON.parse(json) as AccessTokenClaims
}

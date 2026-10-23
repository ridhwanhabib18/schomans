import axios from 'axios'
import { InMemoryWebStorage, UserManager, WebStorageStateStore } from 'oidc-client-ts'
import { config, keycloakIssuer } from '@/shared/lib/config'
import type { AppRole } from '@/shared/lib/roles'
import { useAuthStore } from '@/stores/useAuthStore'

/** Thrown when the sign-in worked but the account holds no SchoMans role. */
export class NoRoleError extends Error {
  constructor() {
    super('The account has no SchoMans role.')
    this.name = 'NoRoleError'
  }
}

export const DEFAULT_RETURN_PATH = '/dashboard'
export const SILENT_CALLBACK_PATH = '/auth/silent'

let manager: UserManager | null = null

/**
 * The OIDC client (Authorization Code + PKCE against Keycloak). Tokens stay in memory: the user store is an
 * in-memory storage, and only the short-lived login state lives in sessionStorage so it survives the redirect.
 */
export function getUserManager(): UserManager {
  if (manager) {
    return manager
  }

  const origin = window.location.origin
  manager = new UserManager({
    authority: keycloakIssuer,
    client_id: config.keycloak.clientId,
    redirect_uri: `${origin}/auth/callback`,
    post_logout_redirect_uri: `${origin}/login`,
    silent_redirect_uri: `${origin}${SILENT_CALLBACK_PATH}`,
    response_type: 'code',
    scope: 'openid profile email',
    automaticSilentRenew: true,
    silentRequestTimeoutInSeconds: 5,
    userStore: new WebStorageStateStore({ store: new InMemoryWebStorage() }),
    stateStore: new WebStorageStateStore({ store: window.sessionStorage }),
  })

  manager.events.addUserLoaded((user) => {
    useAuthStore.getState().setSession(user.access_token)
  })
  manager.events.addSilentRenewError(() => useAuthStore.getState().clearSession())
  manager.events.addAccessTokenExpired(() => useAuthStore.getState().clearSession())
  return manager
}

/** Sends the browser to the Keycloak login page. `returnTo` is where the app continues afterwards. */
export async function startLogin(returnTo: string = DEFAULT_RETURN_PATH): Promise<void> {
  await getUserManager().signinRedirect({ state: returnTo })
}

/** Finishes the login on the callback page and returns the path to continue to. */
export async function completeLogin(): Promise<string> {
  const user = await getUserManager().signinCallback()
  if (!user) {
    throw new Error('The sign-in callback returned no user.')
  }

  if (!useAuthStore.getState().setSession(user.access_token)) {
    throw new NoRoleError()
  }

  return typeof user.state === 'string' && user.state.startsWith('/') ? user.state : DEFAULT_RETURN_PATH
}

/** On a page load there is no token in memory: try to resume the Keycloak session silently, else stay anonymous. */
export async function restoreSession(): Promise<void> {
  let accessToken: string | undefined
  try {
    accessToken = (await getUserManager().signinSilent())?.access_token
  } catch {
    // No Keycloak session (login_required) or the silent check timed out: the user is simply not signed in.
  }

  // The user may have signed in by hand (for example a demo button) while the check was running; never undo that.
  if (useAuthStore.getState().status !== 'loading') {
    return
  }

  if (!accessToken || !useAuthStore.getState().setSession(accessToken)) {
    useAuthStore.getState().clearSession()
  }
}

// --- Demo mode -------------------------------------------------------------------------------------------------
// One-click demo login: a password grant with the demo client, enabled only in the development and demo realms.

interface TokenResponse {
  access_token: string
  refresh_token?: string
  expires_in: number
}

let demoSession = false
let demoRefreshToken: string | undefined
let demoRefreshTimer: ReturnType<typeof setTimeout> | undefined

const tokenUrl = `${keycloakIssuer}/protocol/openid-connect/token`

async function requestDemoToken(form: Record<string, string>): Promise<TokenResponse> {
  const body = new URLSearchParams({ client_id: config.keycloak.demoClientId, ...form })
  const { data } = await axios.post<TokenResponse>(tokenUrl, body, {
    headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
  })
  return data
}

function applyDemoTokens(tokens: TokenResponse): void {
  if (!useAuthStore.getState().setSession(tokens.access_token)) {
    throw new NoRoleError()
  }

  demoSession = true
  demoRefreshToken = tokens.refresh_token
  clearTimeout(demoRefreshTimer)
  if (tokens.refresh_token) {
    // Renew shortly before the access token expires.
    const delayMs = Math.max(tokens.expires_in - 30, 10) * 1000
    demoRefreshTimer = setTimeout(() => void refreshDemoSession(), delayMs)
  }
}

async function refreshDemoSession(): Promise<void> {
  if (!demoRefreshToken) {
    return
  }

  try {
    applyDemoTokens(await requestDemoToken({ grant_type: 'refresh_token', refresh_token: demoRefreshToken }))
  } catch {
    endDemoSession()
    useAuthStore.getState().clearSession()
  }
}

function endDemoSession(): void {
  demoSession = false
  demoRefreshToken = undefined
  clearTimeout(demoRefreshTimer)
}

/** Signs in as the fictional demo user of a role, for example teacher@schomans.demo. */
export async function demoLogin(role: AppRole): Promise<void> {
  const tokens = await requestDemoToken({
    grant_type: 'password',
    username: `${role.toLowerCase()}@schomans.demo`,
    password: config.demoPassword,
    scope: 'openid',
  })
  applyDemoTokens(tokens)
}

/** Ends the session. A Keycloak login also ends the Keycloak session; a demo login only clears the memory. */
export async function logout(): Promise<void> {
  if (demoSession) {
    endDemoSession()
    useAuthStore.getState().clearSession()
    return
  }

  const user = await getUserManager().getUser()
  useAuthStore.getState().clearSession()
  if (user) {
    await getUserManager().signoutRedirect()
  }
}

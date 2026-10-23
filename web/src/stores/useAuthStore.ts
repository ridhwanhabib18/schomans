import { create } from 'zustand'
import { decodeAccessToken } from '@/shared/lib/jwt'
import { resolveRole, type AppRole } from '@/shared/lib/roles'

export interface AuthUser {
  sub: string
  email: string
  name: string
  role: AppRole
}

export type AuthStatus = 'loading' | 'anonymous' | 'authenticated'

interface AuthState {
  status: AuthStatus
  /** Kept in memory only: never written to localStorage or sessionStorage. */
  accessToken: string | null
  user: AuthUser | null
  /** Stores a session from an access token. Returns false (and ends up anonymous) when the token has no SchoMans role. */
  setSession: (accessToken: string) => boolean
  clearSession: () => void
}

/** Browser-side session state only. Server data (dashboards, profiles) belongs to TanStack Query. */
export const useAuthStore = create<AuthState>((set) => ({
  status: 'loading',
  accessToken: null,
  user: null,
  setSession: (accessToken) => {
    const claims = decodeAccessToken(accessToken)
    const role = resolveRole(claims.realm_access?.roles ?? [])
    if (!role || !claims.sub) {
      set({ status: 'anonymous', accessToken: null, user: null })
      return false
    }

    set({
      status: 'authenticated',
      accessToken,
      user: { sub: claims.sub, email: claims.email ?? '', name: claims.name ?? claims.email ?? '', role },
    })
    return true
  },
  clearSession: () => set({ status: 'anonymous', accessToken: null, user: null }),
}))

// Selectors: components subscribe to the slice they need, never to the whole store.
export const selectAuthStatus = (state: AuthState) => state.status
export const selectAuthUser = (state: AuthState) => state.user
export const selectAuthRole = (state: AuthState) => state.user?.role ?? null

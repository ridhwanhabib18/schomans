import { useMutation, useQueryClient } from '@tanstack/react-query'
import type { AppRole } from '@/shared/lib/roles'
import { completeLogin, demoLogin, logout, startLogin } from './authService'

/** Redirects to the Keycloak login page. */
export function useStartLogin() {
  return useMutation({ mutationFn: (returnTo?: string) => startLogin(returnTo) })
}

/** One-click demo login for a role (demo mode only). */
export function useDemoLogin() {
  return useMutation({ mutationFn: (role: AppRole) => demoLogin(role) })
}

/** Completes the Keycloak login on the callback page. Resolves with the path to continue to. */
export function useCompleteLogin() {
  return useMutation({ mutationFn: () => completeLogin() })
}

export function useLogout() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: () => logout(),
    // Cached server data belongs to the person who just left.
    onSettled: () => queryClient.clear(),
  })
}

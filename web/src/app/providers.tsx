import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { useEffect, useState, type ReactNode } from 'react'
import { restoreSession } from '@/features/auth/api/authService'
import { isDark, selectTheme, useUiStore } from '@/stores/useUiStore'
import { useAuthStore } from '@/stores/useAuthStore'

/** Applies the theme preference (light, dark, system) as the `dark` class on <html>. */
function useThemeClass() {
  const theme = useUiStore(selectTheme)

  useEffect(() => {
    const query = window.matchMedia('(prefers-color-scheme: dark)')
    const apply = () => document.documentElement.classList.toggle('dark', isDark(theme, query.matches))
    apply()
    query.addEventListener('change', apply)
    return () => query.removeEventListener('change', apply)
  }, [theme])
}

export function AppProviders({ children }: { children: ReactNode }) {
  const [queryClient] = useState(
    () => new QueryClient({ defaultOptions: { queries: { retry: 1, staleTime: 30_000, refetchOnWindowFocus: false } } }),
  )
  useThemeClass()

  useEffect(() => {
    // On a page load there is no token in memory yet: resume the Keycloak session if there is one. The callback
    // and silent-callback routes handle their own sign-in, so they must not start a second one.
    if (window.location.pathname !== '/auth/callback') {
      void restoreSession()
    } else {
      useAuthStore.getState().clearSession()
    }

    // Cached server data belongs to the person who just left; never show it to the next one.
    return useAuthStore.subscribe((state, previous) => {
      if (previous.status === 'authenticated' && state.status !== 'authenticated') {
        queryClient.clear()
      }
    })
  }, [queryClient])

  return <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>
}

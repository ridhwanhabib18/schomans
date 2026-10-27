import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render } from '@testing-library/react'
import { AxiosError, AxiosHeaders } from 'axios'
import type { ReactElement } from 'react'
import { createMemoryRouter, RouterProvider, type RouteObject } from 'react-router'
import '@/shared/lib/i18n'
import { useAuthStore } from '@/stores/useAuthStore'

/** Builds an unsigned JWT with the claims a Keycloak access token carries. Never validated, only decoded by the UI. */
export function makeToken(claims: { roles?: string[]; name?: string; email?: string; sub?: string } = {}): string {
  // UTF-8 first, because btoa alone only accepts Latin-1 and names can contain other characters.
  const encode = (value: unknown) =>
    btoa(String.fromCharCode(...new TextEncoder().encode(JSON.stringify(value))))
      .replace(/=+$/, '')
      .replace(/\+/g, '-')
      .replace(/\//g, '_')

  const payload = {
    sub: claims.sub ?? 'sub-test',
    email: claims.email ?? 'rina@schomans.demo',
    name: claims.name ?? 'Rina Putri',
    exp: Math.floor(Date.now() / 1000) + 900,
    realm_access: { roles: claims.roles ?? ['Student'] },
  }
  return `${encode({ alg: 'none' })}.${encode(payload)}.signature`
}

export function createTestQueryClient() {
  // No retries and no caching between tests: a failing request fails immediately.
  return new QueryClient({ defaultOptions: { queries: { retry: false, gcTime: 0 }, mutations: { retry: false } } })
}

/** Renders a component tree at a route, inside the same providers the app uses. */
export function renderRoutes(routes: RouteObject[], initialPath: string) {
  const router = createMemoryRouter(routes, { initialEntries: [initialPath] })
  const queryClient = createTestQueryClient()
  const result = render(
    <QueryClientProvider client={queryClient}>
      <RouterProvider router={router} />
    </QueryClientProvider>,
  )
  return { ...result, router, queryClient }
}

export function renderWithQuery(ui: ReactElement) {
  const queryClient = createTestQueryClient()
  return { ...render(<QueryClientProvider client={queryClient}>{ui}</QueryClientProvider>), queryClient }
}

/** An error like the one axios throws for a failed API call, with a ProblemDetails body. */
export function apiError(status: number, body: Record<string, unknown> = {}) {
  return new AxiosError('Request failed', 'ERR_BAD_REQUEST', undefined, undefined, {
    status,
    statusText: '',
    data: body,
    headers: {},
    config: { headers: new AxiosHeaders() },
  })
}

/** A page of a paged API answer. */
export function pageOf<T>(items: T[], overrides: { page?: number; pageSize?: number; totalCount?: number } = {}) {
  return { items, page: 1, pageSize: 20, totalCount: items.length, ...overrides }
}

/** Signs in as a role, the way the app does after a login. */
export function signInAs(role: string, name = 'Sari Dewi') {
  useAuthStore.getState().setSession(makeToken({ roles: [role], name }))
}

/** Renders one page inside the providers, at its own path. */
export function renderPage(element: ReactElement, path = '/') {
  return renderRoutes([{ path: '*', element }], path)
}

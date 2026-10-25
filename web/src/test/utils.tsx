import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, type RenderOptions } from '@testing-library/react'
import type { ReactElement } from 'react'
import { MemoryRouter } from 'react-router-dom'
import { ConfirmHost } from '@/components/shared/confirm'
import type { Me } from '@/api/types'
import i18n from '@/lib/i18n'
import { useAuth } from '@/stores/auth'

export function makeMe(overrides: Partial<Me> = {}): Me {
  return {
    id: '11111111-1111-1111-1111-111111111111',
    email: 'user@test.local',
    first_name: 'Test',
    last_name: 'User',
    preferred_language: 'en',
    roles: ['centre_admin'],
    permissions: ['students.read', 'students.write', 'staff.read', 'centre_structure.read'],
    tenant: { id: '22222222-2222-2222-2222-222222222222', slug: 'test', name: { en: 'Test Nursery', ar: 'حضانة الاختبار' } },
    ...overrides,
  }
}

export function signIn(me: Me = makeMe()) {
  useAuth.setState({ accessToken: 'test-token', me, ready: true })
}

export function signOut() {
  useAuth.setState({ accessToken: null, me: null, ready: true })
}

export async function setLanguage(language: 'en' | 'ar') {
  await i18n.changeLanguage(language)
}

export function renderWithProviders(ui: ReactElement, options: { route?: string } & Omit<RenderOptions, 'wrapper'> = {}) {
  const { route = '/', ...rest } = options
  const client = new QueryClient({ defaultOptions: { queries: { retry: false }, mutations: { retry: false } } })
  return render(ui, {
    wrapper: ({ children }) => (
      <QueryClientProvider client={client}>
        <MemoryRouter initialEntries={[route]}>{children}</MemoryRouter>
        <ConfirmHost />
      </QueryClientProvider>
    ),
    ...rest,
  })
}

/** Builds a JSON response in the API envelope for use with a mocked fetch. */
export function envelope<T>(data: T, init: { status?: number; pagination?: object } = {}) {
  return new Response(
    JSON.stringify({
      success: true,
      data,
      meta: { request_id: 'req', timestamp: new Date().toISOString(), pagination: init.pagination ?? null },
      errors: null,
    }),
    { status: init.status ?? 200, headers: { 'Content-Type': 'application/json' } },
  )
}

export function failure(status: number, code: string, message = 'x', field: string | null = null) {
  return new Response(
    JSON.stringify({ success: false, data: null, meta: { request_id: 'req' }, errors: [{ code, message, field }] }),
    { status, headers: { 'Content-Type': 'application/json' } },
  )
}

type Handler = Response | (() => Response) | ((init: RequestInit | undefined, url: string) => Response)

/**
 * Routes mocked fetch calls by "METHOD substring" (for example "POST /students"). The first matching route wins,
 * so list the most specific routes first. Unmatched calls answer 404 so a missing mock is obvious.
 */
export function mockApi(fetchMock: { mockImplementation: (fn: (url: string, init?: RequestInit) => Promise<Response>) => unknown }, routes: Record<string, Handler>) {
  const calls: { method: string; url: string; body: unknown }[] = []
  fetchMock.mockImplementation((url: string, init?: RequestInit) => {
    const method = init?.method ?? 'GET'
    calls.push({ method, url: String(url), body: init?.body ? JSON.parse(String(init.body)) : undefined })
    const key = Object.keys(routes).find((k) => {
      const [m, ...rest] = k.split(' ')
      return m === method && String(url).includes(rest.join(' '))
    })
    if (!key) return Promise.resolve(failure(404, 'RESOURCE_NOT_FOUND'))
    const handler = routes[key]
    return Promise.resolve(typeof handler === 'function' ? (handler as (i?: RequestInit, u?: string) => Response)(init, String(url)) : handler.clone())
  })
  return calls
}

export const onePage = { limit: 25, has_more: false, next_cursor: null }

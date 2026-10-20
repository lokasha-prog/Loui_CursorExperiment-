import { useAuth } from '@/stores/auth'
import type { CentreChoice, Pagination, Session } from './types'

const BASE = '/api/v1'
const CSRF_HEADER = { 'X-Requested-With': 'XMLHttpRequest' }

export interface ApiFieldError {
  code: string
  message: string
  field: string | null
}

/** A failed call. `code` is one of the API error codes (spec 08 §5) and drives the translated message shown to people. */
export class ApiError extends Error {
  constructor(
    public readonly status: number,
    public readonly code: string,
    message: string,
    public readonly errors: ApiFieldError[] = [],
  ) {
    super(message)
    this.name = 'ApiError'
  }
}

interface Envelope<T> {
  success: boolean
  data: T | null
  meta: { request_id: string; pagination: Pagination | null }
  errors: ApiFieldError[] | null
}

export interface Page<T> {
  items: T[]
  pagination: Pagination
}

type Query = Record<string, string | number | boolean | null | undefined>

function withQuery(path: string, query?: Query) {
  if (!query) return path
  const params = new URLSearchParams()
  for (const [key, value] of Object.entries(query)) {
    if (value !== undefined && value !== null && value !== '') params.set(key, String(value))
  }
  const text = params.toString()
  return text ? `${path}?${text}` : path
}

async function parse<T>(response: Response): Promise<Envelope<T> | null> {
  if (response.status === 204) return null
  try {
    return (await response.json()) as Envelope<T>
  } catch {
    return null
  }
}

let refreshing: Promise<boolean> | null = null

/** Uses the HttpOnly refresh cookie to get a new access token. Concurrent callers share one refresh. */
export function refreshSession(): Promise<boolean> {
  refreshing ??= (async () => {
    try {
      const response = await fetch(`${BASE}/auth/refresh`, { method: 'POST', headers: CSRF_HEADER, credentials: 'same-origin' })
      const body = await parse<Session>(response)
      if (response.ok && body?.data) {
        useAuth.getState().setSession(body.data.access_token, body.data.me)
        return true
      }
      // Only a definite "no valid session" answer signs the person out; a busy server (429/5xx) must not.
      if (response.status !== 401 && response.status !== 403) return false
    } catch {
      return false
    }
    useAuth.getState().clear()
    return false
  })().finally(() => {
    refreshing = null
  })
  return refreshing
}

async function send<T>(method: string, path: string, options: { body?: unknown; query?: Query; retry?: boolean } = {}) {
  const headers: Record<string, string> = { Accept: 'application/json' }
  const token = useAuth.getState().accessToken
  if (token) headers.Authorization = `Bearer ${token}`
  if (options.body !== undefined) headers['Content-Type'] = 'application/json'

  let response: Response
  try {
    response = await fetch(withQuery(`${BASE}${path}`, options.query), {
      method,
      headers,
      body: options.body === undefined ? undefined : JSON.stringify(options.body),
      credentials: 'same-origin',
    })
  } catch {
    throw new ApiError(0, 'NETWORK', 'Network error')
  }

  if (response.status === 401 && token && options.retry !== false && (await refreshSession())) {
    return send<T>(method, path, { ...options, retry: false })
  }

  const envelope = await parse<T>(response)
  if (!response.ok || (envelope && !envelope.success)) {
    const first = envelope?.errors?.[0]
    throw new ApiError(
      response.status,
      first?.code ?? (response.status === 429 ? 'RATE_LIMIT_EXCEEDED' : response.status >= 500 ? 'INTERNAL_ERROR' : 'UNKNOWN'),
      first?.message ?? response.statusText,
      envelope?.errors ?? [],
    )
  }
  return envelope
}

export const api = {
  async get<T>(path: string, query?: Query): Promise<T> {
    return (await send<T>('GET', path, { query }))!.data as T
  },
  async page<T>(path: string, query?: Query): Promise<Page<T>> {
    const envelope = (await send<T[]>('GET', path, { query }))!
    return {
      items: envelope.data ?? [],
      pagination: envelope.meta.pagination ?? { limit: 0, has_more: false, next_cursor: null },
    }
  },
  async post<T>(path: string, body?: unknown): Promise<T> {
    return (await send<T>('POST', path, { body: body ?? {} }))?.data as T
  },
  async patch<T>(path: string, body: unknown): Promise<T> {
    return (await send<T>('PATCH', path, { body }))!.data as T
  },
  async delete(path: string): Promise<void> {
    await send<void>('DELETE', path)
  },
}

export const authApi = {
  /** The centre is found from the e-mail. If the same e-mail belongs to several centres, the choices come back instead of a session. */
  async login(input: { tenant?: string; email: string; password: string }): Promise<Session | { chooseCentre: CentreChoice[] }> {
    const response = await fetch(`${BASE}/auth/login`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ ...input, tenant: input.tenant || null }),
      credentials: 'same-origin',
    }).catch(() => {
      throw new ApiError(0, 'NETWORK', 'Network error')
    })
    const body = await parse<Session | { choose_centre: CentreChoice[] }>(response)
    if (!response.ok || !body?.data) {
      const first = body?.errors?.[0]
      throw new ApiError(
        response.status,
        first?.code ?? 'UNKNOWN',
        first?.message ?? response.statusText,
        body?.errors ?? [],
      )
    }
    if ('choose_centre' in body.data) return { chooseCentre: body.data.choose_centre }
    useAuth.getState().setSession(body.data.access_token, body.data.me)
    return body.data
  },

  /** Development helper: the list of test users, or null when the server does not offer it (always so outside local development). */
  async devTestUsers(): Promise<{ role: string; email: string; exists: boolean }[] | null> {
    const response = await fetch(`${BASE}/dev/test-users`, { credentials: 'same-origin' }).catch(() => null)
    if (!response?.ok) return null
    return (await parse<{ role: string; email: string; exists: boolean }[]>(response))?.data ?? null
  },

  /** Development helper: signs in as the test user for a role without a password. Only works on the same computer as the server. */
  async devQuickLogin(role: string): Promise<Session> {
    const response = await fetch(`${BASE}/dev/quick-login`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ role }),
      credentials: 'same-origin',
    }).catch(() => {
      throw new ApiError(0, 'NETWORK', 'Network error')
    })
    const body = await parse<Session>(response)
    if (!response.ok || !body?.data) {
      const first = body?.errors?.[0]
      throw new ApiError(response.status, first?.code ?? 'UNKNOWN', first?.message ?? response.statusText, body?.errors ?? [])
    }
    useAuth.getState().setSession(body.data.access_token, body.data.me)
    return body.data
  },

  async logout(): Promise<void> {
    await fetch(`${BASE}/auth/logout`, { method: 'POST', headers: CSRF_HEADER, credentials: 'same-origin' }).catch(() => undefined)
    useAuth.getState().clear()
  },

  async setupStatus(): Promise<{ setupRequired: boolean; passwordMinLength: number }> {
    const response = await fetch(`${BASE}/auth/setup-status`).catch(() => null)
    const body = response ? await parse<{ setup_required: boolean; password_min_length: number }>(response) : null
    return { setupRequired: body?.data?.setup_required ?? false, passwordMinLength: body?.data?.password_min_length ?? 0 }
  },

  async bootstrap(input: {
    bootstrap_token: string
    tenant_slug: string
    tenant_name: Record<string, string>
    admin_email: string
    admin_first_name: string
    admin_last_name: string
    admin_password: string
  }): Promise<void> {
    const response = await fetch(`${BASE}/auth/bootstrap-super-admin`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(input),
    }).catch(() => {
      throw new ApiError(0, 'NETWORK', 'Network error')
    })
    const body = await parse<unknown>(response)
    if (!response.ok) {
      const first = body?.errors?.[0]
      throw new ApiError(
        response.status,
        first?.code ?? 'UNKNOWN',
        first?.message ?? response.statusText,
        body?.errors ?? [],
      )
    }
  },
}

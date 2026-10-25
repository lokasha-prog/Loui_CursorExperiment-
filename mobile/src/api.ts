import { apiBase, secret } from './core'

export interface Me {
  id: string
  email: string
  first_name: string | null
  last_name: string | null
  preferred_language: string
  roles: string[]
  permissions: string[]
  tenant: { id: string; slug: string; name: Record<string, string> }
}

interface Session {
  access_token: string
  me: Me
  refresh_token?: string
}

export class ApiError extends Error {
  constructor(public status: number, public code: string, message: string) {
    super(message)
  }
}

const REFRESH_KEY = 'nms.refresh'
let accessToken: string | null = null
let onSignedOut: () => void = () => undefined
let refreshing: Promise<Session | null> | null = null

export const setSignedOutHandler = (fn: () => void) => { onSignedOut = fn }

const headers = (extra: Record<string, string> = {}) => ({ 'Content-Type': 'application/json', 'X-NMS-Client': 'mobile', 'X-Requested-With': 'XMLHttpRequest', ...extra })

async function parse(response: Response) {
  try {
    return await response.json()
  } catch {
    return null
  }
}

/** Gets a new access token with the refresh token kept in the secure store. Several callers at once share one attempt. */
export function refreshSession(): Promise<Session | null> {
  refreshing ??= (async () => {
    const token = await secret.get(REFRESH_KEY)
    if (!token) return null
    try {
      const response = await fetch(`${apiBase()}/api/v1/auth/refresh`, { method: 'POST', headers: headers(), body: JSON.stringify({ refresh_token: token }) })
      const body = await parse(response)
      if (!response.ok || !body?.data) {
        await secret.remove(REFRESH_KEY)
        return null
      }
      const session = body.data as Session
      accessToken = session.access_token
      if (session.refresh_token) await secret.set(REFRESH_KEY, session.refresh_token)
      return session
    } catch {
      return null
    } finally {
      refreshing = null
    }
  })()
  return refreshing
}

export async function login(email: string, password: string, tenant?: string): Promise<Session | { choose: { slug: string; name: Record<string, string> }[] }> {
  const response = await fetch(`${apiBase()}/api/v1/auth/login`, { method: 'POST', headers: headers(), body: JSON.stringify({ email, password, tenant: tenant ?? null }) })
  const body = await parse(response)
  if (!response.ok) throw new ApiError(response.status, body?.errors?.[0]?.code ?? 'UNKNOWN', body?.errors?.[0]?.message ?? 'Sign-in failed')
  if (body.data?.choose_centre) return { choose: body.data.choose_centre }
  const session = body.data as Session
  accessToken = session.access_token
  if (session.refresh_token) await secret.set(REFRESH_KEY, session.refresh_token)
  return session
}

/** Local test-user helper (ADR-0006 A). Only shown when EXPO_PUBLIC_DEV_LOGIN=1; the server refuses it outside a developer's own computer. */
export const DEV_LOGIN = process.env.EXPO_PUBLIC_DEV_LOGIN === '1'

export async function devLogin(role: string): Promise<Session> {
  const response = await fetch(`${apiBase()}/api/v1/dev/quick-login`, { method: 'POST', headers: headers(), body: JSON.stringify({ role }) })
  const body = await parse(response)
  if (!response.ok) throw new ApiError(response.status, 'DEV_LOGIN', 'Test sign-in is not available')
  const session = body.data as Session
  accessToken = session.access_token
  if (session.refresh_token) await secret.set(REFRESH_KEY, session.refresh_token)
  return session
}

export async function signOut() {
  const token = await secret.get(REFRESH_KEY)
  await secret.remove(REFRESH_KEY)
  accessToken = null
  if (token) fetch(`${apiBase()}/api/v1/auth/logout`, { method: 'POST', headers: headers(), body: JSON.stringify({ refresh_token: token }) }).catch(() => undefined)
}

export const hasSavedSession = async () => !!(await secret.get(REFRESH_KEY))

/** True when the phone could not reach the server at all (as opposed to the server saying no). */
export const isOffline = (error: unknown) => error instanceof ApiError && error.status === 0

export async function request<T>(method: string, path: string, body?: unknown, retry = true): Promise<{ data: T; next: string | null }> {
  let response: Response
  try {
    response = await fetch(`${apiBase()}/api/v1${path}`, { method, headers: headers(accessToken ? { Authorization: `Bearer ${accessToken}` } : {}), body: body === undefined ? undefined : JSON.stringify(body) })
  } catch {
    throw new ApiError(0, 'NETWORK', 'No connection')
  }
  if (response.status === 401 && retry) {
    const session = await refreshSession()
    if (session) return request<T>(method, path, body, false)
    onSignedOut()
  }
  if (response.status === 204) return { data: undefined as T, next: null }
  const json = await parse(response)
  if (!response.ok) throw new ApiError(response.status, json?.errors?.[0]?.code ?? 'UNKNOWN', json?.errors?.[0]?.message ?? response.statusText)
  return { data: json.data as T, next: json.meta?.pagination?.has_more ? json.meta.pagination.next_cursor : null }
}

export const api = {
  get: async <T>(path: string) => (await request<T>('GET', path)).data,
  page: async <T>(path: string) => (await request<T[]>('GET', path)).data,
  post: async <T>(path: string, body?: unknown) => (await request<T>('POST', path, body ?? {})).data,
  put: async <T>(path: string, body?: unknown) => (await request<T>('PUT', path, body ?? {})).data,
}

/** What an image component needs to show a stored photo (the server only serves it to signed-in people). */
export const mediaSource = (id: string) => ({ uri: `${apiBase()}/api/v1/media/${id}/content`, headers: { 'X-NMS-Client': 'mobile', ...(accessToken ? { Authorization: `Bearer ${accessToken}` } : {}) } as Record<string, string> })

/** Sends a photo or file taken on the phone. Returns what the server stored. */
export async function upload<T>(path: string, uri: string, name: string, type: string): Promise<T> {
  const form = new FormData()
  if (uri.startsWith('data:') || uri.startsWith('blob:') || uri.startsWith('http')) {
    form.append('file', await (await fetch(uri)).blob(), name)
  } else {
    form.append('file', { uri, name, type } as unknown as Blob)
  }
  let response: Response
  try {
    response = await fetch(`${apiBase()}/api/v1${path}`, { method: 'POST', headers: { 'X-NMS-Client': 'mobile', ...(accessToken ? { Authorization: `Bearer ${accessToken}` } : {}) }, body: form })
  } catch {
    throw new ApiError(0, 'NETWORK', 'No connection')
  }
  const json = await parse(response)
  if (!response.ok) throw new ApiError(response.status, json?.errors?.[0]?.code ?? 'UNKNOWN', json?.errors?.[0]?.message ?? response.statusText)
  return json.data as T
}

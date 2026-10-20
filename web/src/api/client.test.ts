import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { useAuth } from '@/stores/auth'
import { envelope, failure, makeMe, signIn, signOut } from '@/test/utils'
import { ApiError, api, authApi, refreshSession } from './client'

const fetchMock = vi.fn()

beforeEach(() => {
  fetchMock.mockReset()
  vi.stubGlobal('fetch', fetchMock)
  signOut()
})

afterEach(() => vi.unstubAllGlobals())

describe('api client', () => {
  it('sends the access token and unwraps the envelope', async () => {
    signIn()
    fetchMock.mockResolvedValueOnce(envelope({ id: 'a' }))

    const result = await api.get<{ id: string }>('/students/a')

    expect(result).toEqual({ id: 'a' })
    const [url, init] = fetchMock.mock.calls[0]
    expect(url).toBe('/api/v1/students/a')
    expect(init.headers.Authorization).toBe('Bearer test-token')
  })

  it('builds query strings and skips empty values', async () => {
    signIn()
    fetchMock.mockResolvedValueOnce(envelope([], { pagination: { limit: 25, has_more: false, next_cursor: null } }))

    await api.page('/students', { search: 'lay', status: '', roomId: undefined, limit: 25 })

    expect(fetchMock.mock.calls[0][0]).toBe('/api/v1/students?search=lay&limit=25')
  })

  it('returns pagination details for lists', async () => {
    signIn()
    fetchMock.mockResolvedValueOnce(envelope([{ id: 1 }], { pagination: { limit: 25, has_more: true, next_cursor: 'abc' } }))

    const page = await api.page<{ id: number }>('/students')

    expect(page.items).toHaveLength(1)
    expect(page.pagination.next_cursor).toBe('abc')
  })

  it('turns API errors into ApiError with the code', async () => {
    signIn()
    fetchMock.mockResolvedValueOnce(failure(409, 'DUPLICATE_RESOURCE', 'exists', 'email'))

    const error = await api.post('/staff', {}).catch((e: unknown) => e)

    expect(error).toBeInstanceOf(ApiError)
    expect((error as ApiError).status).toBe(409)
    expect((error as ApiError).code).toBe('DUPLICATE_RESOURCE')
    expect((error as ApiError).errors[0].field).toBe('email')
  })

  it('reports a network failure with its own code', async () => {
    signIn()
    fetchMock.mockRejectedValueOnce(new TypeError('offline'))

    const error = await api.get('/students').catch((e: unknown) => e)

    expect((error as ApiError).code).toBe('NETWORK')
  })

  it('refreshes once on 401 and retries the original call', async () => {
    signIn()
    fetchMock
      .mockResolvedValueOnce(failure(401, 'UNAUTHORIZED'))
      .mockResolvedValueOnce(envelope({ access_token: 'new-token', expires_at: '', me: makeMe() }))
      .mockResolvedValueOnce(envelope({ ok: true }))

    const result = await api.get<{ ok: boolean }>('/students')

    expect(result.ok).toBe(true)
    expect(fetchMock.mock.calls[1][0]).toBe('/api/v1/auth/refresh')
    expect(fetchMock.mock.calls[1][1].headers['X-Requested-With']).toBeDefined()
    expect(useAuth.getState().accessToken).toBe('new-token')
  })

  it('signs the person out when the session cannot be refreshed', async () => {
    signIn()
    fetchMock.mockResolvedValueOnce(failure(401, 'UNAUTHORIZED')).mockResolvedValueOnce(failure(401, 'UNAUTHORIZED'))

    const error = await api.get('/students').catch((e: unknown) => e)

    expect((error as ApiError).status).toBe(401)
    expect(useAuth.getState().me).toBeNull()
  })

  it('does not sign anyone out when the server is merely busy', async () => {
    signIn()
    fetchMock.mockResolvedValueOnce(failure(429, 'RATE_LIMIT_EXCEEDED'))

    const restored = await refreshSession()

    expect(restored).toBe(false)
    expect(useAuth.getState().me).not.toBeNull()
  })

  it('shares one refresh between simultaneous callers', async () => {
    fetchMock.mockResolvedValue(envelope({ access_token: 't', expires_at: '', me: makeMe() }))

    await Promise.all([refreshSession(), refreshSession(), refreshSession()])

    expect(fetchMock).toHaveBeenCalledTimes(1)
  })

  it('handles 204 responses', async () => {
    signIn()
    fetchMock.mockResolvedValueOnce(new Response(null, { status: 204 }))

    await expect(api.delete('/students/a')).resolves.toBeUndefined()
  })
})

describe('auth api', () => {
  it('stores the session after login and never puts the token in storage', async () => {
    fetchMock.mockResolvedValueOnce(envelope({ access_token: 'abc', expires_at: '', me: makeMe() }))

    await authApi.login({ email: 'a@b.ae', password: 'x', tenant: '' })

    expect(useAuth.getState().accessToken).toBe('abc')
    expect(JSON.stringify(localStorage)).not.toContain('abc')
    expect(JSON.parse(fetchMock.mock.calls[0][1].body).tenant).toBeNull()
  })

  it('throws ApiError on bad credentials', async () => {
    fetchMock.mockResolvedValueOnce(failure(401, 'UNAUTHORIZED', 'Invalid email or password.'))

    await expect(authApi.login({ email: 'a@b.ae', password: 'x' })).rejects.toMatchObject({ status: 401 })
    expect(useAuth.getState().me).toBeNull()
  })

  it('logout clears the session even if the server is unreachable', async () => {
    signIn()
    fetchMock.mockRejectedValueOnce(new TypeError('offline'))

    await authApi.logout()

    expect(useAuth.getState().me).toBeNull()
  })

  it('reads first-run status and password rule', async () => {
    fetchMock.mockResolvedValueOnce(envelope({ setup_required: true, password_min_length: 12 }))

    await expect(authApi.setupStatus()).resolves.toEqual({ setupRequired: true, passwordMinLength: 12 })
  })
})

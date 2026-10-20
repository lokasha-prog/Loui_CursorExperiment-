import { screen } from '@testing-library/react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { useAuth } from '@/stores/auth'
import { AppRoutes, createQueryClient } from './App'
import { ApiError } from '@/api/client'
import { envelope, makeMe, mockApi, onePage, renderWithProviders, setLanguage, signIn, signOut } from '@/test/utils'

const fetchMock = vi.fn()

beforeEach(async () => {
  fetchMock.mockReset()
  vi.stubGlobal('fetch', fetchMock)
  mockApi(fetchMock, {
    'GET /auth/setup-status': envelope({ setup_required: false, password_min_length: 12 }),
    'GET /students': envelope([], { pagination: onePage }),
    'GET /staff': envelope([], { pagination: onePage }),
    'GET /rooms': envelope([], { pagination: onePage }),
  })
  await setLanguage('en')
  signOut()
})

afterEach(() => vi.unstubAllGlobals())

describe('routing and access', () => {
  it('sends a signed-out visitor to the login page', async () => {
    renderWithProviders(<AppRoutes />, { route: '/students' })

    expect(await screen.findByRole('heading', { name: 'Welcome back' })).toBeInTheDocument()
  })

  it('shows a loading state until the saved session has been checked', () => {
    useAuth.setState({ accessToken: null, me: null, ready: false })
    renderWithProviders(<AppRoutes />, { route: '/students' })

    expect(screen.getByRole('status', { name: 'Loading…' })).toBeInTheDocument()
  })

  it('lands a signed-in person on the dashboard', async () => {
    signIn(makeMe({ first_name: 'Layla' }))
    renderWithProviders(<AppRoutes />, { route: '/' })

    expect(await screen.findByRole('heading', { name: 'Welcome, Layla' })).toBeInTheDocument()
  })

  it('keeps people out of areas their role cannot use', async () => {
    signIn(makeMe({ roles: ['parent'], permissions: ['students.read'] }))
    renderWithProviders(<AppRoutes />, { route: '/users' })

    expect(await screen.findByRole('heading', { name: /Welcome/ })).toBeInTheDocument()
  })

  it('shows a friendly page for unknown addresses', async () => {
    signIn()
    renderWithProviders(<AppRoutes />, { route: '/nothing-here' })

    expect(await screen.findByText('Page not found')).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Go to the dashboard' })).toBeInTheDocument()
  })

  it('shows the not-found page in Arabic too', async () => {
    signIn()
    await setLanguage('ar')
    renderWithProviders(<AppRoutes />, { route: '/nothing-here' })

    expect(await screen.findByText('الصفحة غير موجودة')).toBeInTheDocument()
  })
})

describe('query defaults', () => {
  it('does not retry answers that will not change', () => {
    const retry = createQueryClient().getDefaultOptions().queries!.retry as (count: number, error: unknown) => boolean

    expect(retry(0, new ApiError(404, 'RESOURCE_NOT_FOUND', 'x'))).toBe(false)
    expect(retry(0, new ApiError(403, 'INSUFFICIENT_PERMISSION', 'x'))).toBe(false)
    expect(retry(0, new ApiError(500, 'INTERNAL_ERROR', 'x'))).toBe(true)
    expect(retry(2, new ApiError(500, 'INTERNAL_ERROR', 'x'))).toBe(false)
  })
})

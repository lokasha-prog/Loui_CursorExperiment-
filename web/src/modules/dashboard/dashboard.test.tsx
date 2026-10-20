import { screen } from '@testing-library/react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { envelope, makeMe, mockApi, onePage, renderWithProviders, setLanguage, signIn, signOut } from '@/test/utils'
import DashboardPage from './DashboardPage'

const fetchMock = vi.fn()

beforeEach(async () => {
  fetchMock.mockReset()
  vi.stubGlobal('fetch', fetchMock)
  await setLanguage('en')
})

afterEach(() => {
  vi.unstubAllGlobals()
  signOut()
})

describe('DashboardPage', () => {
  it('shows live counts and quick actions for an administrator', async () => {
    signIn(makeMe({ permissions: ['students.read', 'students.write', 'staff.read', 'staff.write', 'centre_structure.read', 'centre_structure.write'] }))
    mockApi(fetchMock, {
      'GET /students': envelope([{ id: 1 }, { id: 2 }, { id: 3 }], { pagination: onePage }),
      'GET /staff': envelope([{ id: 1 }], { pagination: onePage }),
      'GET /rooms': envelope([{ id: 1 }, { id: 2 }], { pagination: onePage }),
    })
    renderWithProviders(<DashboardPage />)

    expect(await screen.findByText('3')).toBeInTheDocument()
    expect(screen.getByText('1')).toBeInTheDocument()
    expect(screen.getByText('2')).toBeInTheDocument()
    expect(screen.getByRole('link', { name: /Add a student/ })).toHaveAttribute('href', '/students?new=1')
    expect(screen.getByRole('link', { name: /Add a room/ })).toBeInTheDocument()
  })

  it('shows "25+" when there are more than one page', async () => {
    signIn(makeMe({ permissions: ['students.read'] }))
    mockApi(fetchMock, {
      'GET /students': envelope(Array.from({ length: 25 }, (_, i) => ({ id: i })), { pagination: { limit: 25, has_more: true, next_cursor: 'x' } }),
    })
    renderWithProviders(<DashboardPage />)

    expect(await screen.findByText('25+')).toBeInTheDocument()
  })

  it('shows a parent only their children and no management shortcuts', async () => {
    signIn(makeMe({ roles: ['parent'], permissions: ['students.read'], first_name: 'Aisha' }))
    mockApi(fetchMock, { 'GET /students': envelope([{ id: 1 }], { pagination: onePage }) })
    renderWithProviders(<DashboardPage />)

    expect(await screen.findByText('Everything about your children in one place.')).toBeInTheDocument()
    expect(screen.getByText('My children')).toBeInTheDocument()
    expect(screen.queryByText('Staff')).not.toBeInTheDocument()
    expect(screen.queryByText('Quick actions')).not.toBeInTheDocument()
  })

  it('is fully translated in Arabic', async () => {
    signIn(makeMe({ permissions: ['students.read', 'students.write'], first_name: 'ليلى' }))
    await setLanguage('ar')
    mockApi(fetchMock, { 'GET /students': envelope([], { pagination: onePage }) })
    renderWithProviders(<DashboardPage />)

    expect(await screen.findByRole('heading', { name: 'مرحبًا، ليلى' })).toBeInTheDocument()
    expect(screen.getByText('إضافة طالب')).toBeInTheDocument()
    expect(screen.getByText('المزيد قادم')).toBeInTheDocument()
  })
})

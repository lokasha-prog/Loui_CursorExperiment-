import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { envelope, failure, makeMe, mockApi, onePage, renderWithProviders, setLanguage, signIn, signOut } from '@/test/utils'
import UsersPage from './UsersPage'
import { useApiErrorMessage, serverFieldErrors } from '@/hooks/use-api-error'
import { ApiError } from '@/api/client'
import { renderHook } from '@testing-library/react'

const fetchMock = vi.fn()
const status = envelope({ setup_required: false, password_min_length: 12 })

const account = (overrides = {}) => ({
  id: 'u2',
  email: 'teacher@test.local',
  first_name: 'Noor',
  last_name: 'Ahmed',
  preferred_language: 'en',
  status: 'active',
  roles: ['teacher'],
  last_login_at: null,
  created_at: '2026-01-01T00:00:00Z',
  ...overrides,
})

beforeEach(async () => {
  fetchMock.mockReset()
  vi.stubGlobal('fetch', fetchMock)
  await setLanguage('en')
  signIn(makeMe({ id: 'me', roles: ['super_admin'], permissions: ['user_management.manage', 'staff.read'] }))
})

afterEach(() => {
  vi.unstubAllGlobals()
  signOut()
})

describe('UsersPage', () => {
  it('lists accounts with translated roles and status', async () => {
    mockApi(fetchMock, { 'GET /users': envelope([account()], { pagination: onePage }), 'GET /staff': envelope([], { pagination: onePage }) })
    renderWithProviders(<UsersPage />)

    const row = (await screen.findByText('Noor Ahmed')).closest('tr')!
    expect(within(row).getByText('Teacher')).toBeInTheDocument()
    expect(within(row).getByText('Active')).toBeInTheDocument()
    expect(within(row).getByText('Never')).toBeInTheDocument()
  })

  it('shows Arabic role and status names', async () => {
    await setLanguage('ar')
    mockApi(fetchMock, { 'GET /users': envelope([account()], { pagination: onePage }), 'GET /staff': envelope([], { pagination: onePage }) })
    renderWithProviders(<UsersPage />)

    const row = (await screen.findByText('Noor Ahmed')).closest('tr')!
    expect(within(row).getByText('معلّم/معلّمة')).toBeInTheDocument()
    expect(within(row).getByText('نشط')).toBeInTheDocument()
  })

  it('suspends an account but never offers to suspend yourself', async () => {
    const calls = mockApi(fetchMock, {
      'POST /users/u2/actions/set-status': envelope(account({ status: 'suspended' })),
      'GET /users': envelope([account(), account({ id: 'me', email: 'me@test.local', first_name: 'Me', last_name: 'Self', roles: ['super_admin'] })], { pagination: onePage }),
      'GET /staff': envelope([], { pagination: onePage }),
    })
    renderWithProviders(<UsersPage />)
    await screen.findByText('Noor Ahmed')

    expect(screen.getAllByRole('button', { name: 'Suspend' })).toHaveLength(1)
    await userEvent.click(screen.getByRole('button', { name: 'Suspend' }))

    await waitFor(() => expect(calls.find((c) => c.method === 'POST')?.body).toEqual({ status: 'suspended' }))
  })

  it('offers reactivation for suspended accounts', async () => {
    mockApi(fetchMock, { 'GET /users': envelope([account({ status: 'suspended' })], { pagination: onePage }), 'GET /staff': envelope([], { pagination: onePage }) })
    renderWithProviders(<UsersPage />)

    expect(await screen.findByRole('button', { name: 'Reactivate' })).toBeInTheDocument()
  })

  it('enforces the password rule supplied by the server and sends the chosen role', async () => {
    const calls = mockApi(fetchMock, {
      'POST /users': envelope(account(), { status: 201 }),
      'GET /auth/setup-status': status,
      'GET /users': envelope([], { pagination: onePage }),
      'GET /staff': envelope([], { pagination: onePage }),
    })
    renderWithProviders(<UsersPage />)
    await userEvent.click(await screen.findByRole('button', { name: /Create account/ }))
    const dialog = await screen.findByRole('dialog')
    await waitFor(() => expect(within(dialog).getByText('At least 12 characters with letters and numbers')).toBeInTheDocument())

    await userEvent.type(within(dialog).getByLabelText(/First name/), 'Noor')
    await userEvent.type(within(dialog).getByLabelText(/Last name/), 'Ahmed')
    await userEvent.type(within(dialog).getByLabelText(/^Email/), 'noor@test.local')
    await userEvent.type(within(dialog).getByLabelText(/Temporary password/), 'short1')
    await userEvent.click(within(dialog).getByRole('button', { name: 'Create' }))
    expect(await within(dialog).findByText('Use at least 12 characters')).toBeInTheDocument()

    await userEvent.clear(within(dialog).getByLabelText(/Temporary password/))
    await userEvent.type(within(dialog).getByLabelText(/Temporary password/), 'Str0ngPassword12')
    await userEvent.selectOptions(within(dialog).getByLabelText(/Role/), 'receptionist')
    await userEvent.click(within(dialog).getByRole('button', { name: 'Create' }))

    await waitFor(() => expect(calls.some((c) => c.method === 'POST')).toBe(true))
    expect(calls.find((c) => c.method === 'POST')!.body).toMatchObject({ roles: ['receptionist'], email: 'noor@test.local' })
  })

  it('only offers the super administrator role to a super administrator', async () => {
    signIn(makeMe({ roles: ['it'], permissions: ['user_management.manage'] }))
    mockApi(fetchMock, { 'GET /auth/setup-status': status, 'GET /users': envelope([], { pagination: onePage }), 'GET /staff': envelope([], { pagination: onePage }) })
    renderWithProviders(<UsersPage />)
    await userEvent.click(await screen.findByRole('button', { name: /Create account/ }))
    const dialog = await screen.findByRole('dialog')

    expect(within(dialog).queryByRole('option', { name: 'Super administrator' })).not.toBeInTheDocument()
    expect(within(dialog).getByRole('option', { name: 'Teacher' })).toBeInTheDocument()
  })

  it('shows a duplicate email next to the field', async () => {
    mockApi(fetchMock, {
      'POST /users': failure(409, 'DUPLICATE_RESOURCE'),
      'GET /auth/setup-status': status,
      'GET /users': envelope([], { pagination: onePage }),
      'GET /staff': envelope([], { pagination: onePage }),
    })
    renderWithProviders(<UsersPage />)
    await userEvent.click(await screen.findByRole('button', { name: /Create account/ }))
    const dialog = await screen.findByRole('dialog')
    await waitFor(() => expect(within(dialog).getByText('At least 12 characters with letters and numbers')).toBeInTheDocument())

    await userEvent.type(within(dialog).getByLabelText(/First name/), 'A')
    await userEvent.type(within(dialog).getByLabelText(/Last name/), 'B')
    await userEvent.type(within(dialog).getByLabelText(/^Email/), 'dup@test.local')
    await userEvent.type(within(dialog).getByLabelText(/Temporary password/), 'Str0ngPassword12')
    await userEvent.click(within(dialog).getByRole('button', { name: 'Create' }))

    expect(await within(dialog).findByText('This already exists.')).toBeInTheDocument()
  })
})

describe('error messages', () => {
  it('translates API codes and falls back safely', async () => {
    const { result } = renderHook(() => useApiErrorMessage())

    expect(result.current(new ApiError(403, 'INSUFFICIENT_PERMISSION', 'raw server text'))).toBe('You do not have permission to do this.')
    expect(result.current(new ApiError(500, 'SOMETHING_NEW', 'raw'))).toBe('Something went wrong. Please try again.')
    expect(result.current(new Error('boom'))).toBe('Something went wrong. Please try again.')

    await setLanguage('ar')
    const arabic = renderHook(() => useApiErrorMessage())
    expect(arabic.result.current(new ApiError(0, 'NETWORK', 'x'))).toContain('تعذّر')
  })

  it('collects field errors from the server', () => {
    const error = new ApiError(400, 'VALIDATION_ERROR', 'x', [
      { code: 'VALIDATION_ERROR', message: 'bad', field: 'email' },
      { code: 'VALIDATION_ERROR', message: 'bad', field: null },
    ])

    expect(serverFieldErrors(error)).toEqual({ email: 'VALIDATION_ERROR' })
    expect(serverFieldErrors(new Error('x'))).toEqual({})
  })
})

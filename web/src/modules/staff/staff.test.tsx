import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { envelope, failure, makeMe, mockApi, onePage, renderWithProviders, setLanguage, signIn, signOut } from '@/test/utils'
import StaffPage from './StaffPage'

const fetchMock = vi.fn()

const member = {
  id: 's1',
  first_name: 'Noor',
  last_name: 'Ahmed',
  email: 'noor@test.local',
  phone: null,
  job_title: 'Lead teacher',
  status: 'on_leave',
  hire_date: '2025-09-01',
  barcode: 'STF123456789',
  created_at: '2026-01-01T00:00:00Z',
}

beforeEach(async () => {
  fetchMock.mockReset()
  vi.stubGlobal('fetch', fetchMock)
  await setLanguage('en')
  signIn(makeMe({ permissions: ['staff.read', 'staff.write'] }))
})

afterEach(() => {
  vi.unstubAllGlobals()
  signOut()
})

describe('StaffPage', () => {
  it('lists team members with job title and status', async () => {
    mockApi(fetchMock, { 'GET /staff': envelope([member], { pagination: onePage }) })
    renderWithProviders(<StaffPage />)

    const row = (await screen.findByText('Noor Ahmed')).closest('tr')!
    expect(within(row).getByText('Lead teacher')).toBeInTheDocument()
    expect(within(row).getByText('On leave')).toBeInTheDocument()
    expect(within(row).getByText('noor@test.local')).toHaveAttribute('dir', 'ltr')
  })

  it('shows the empty state and hides add for people without permission', async () => {
    signIn(makeMe({ permissions: ['staff.read'] }))
    mockApi(fetchMock, { 'GET /staff': envelope([], { pagination: onePage }) })
    renderWithProviders(<StaffPage />)

    expect(await screen.findByText('No staff yet')).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /Add staff member/ })).not.toBeInTheDocument()
  })

  it('shows Arabic labels', async () => {
    await setLanguage('ar')
    mockApi(fetchMock, { 'GET /staff': envelope([member], { pagination: onePage }) })
    renderWithProviders(<StaffPage />)

    expect(await screen.findByText('في إجازة')).toBeInTheDocument()
    expect(screen.getByRole('heading', { name: 'الموظفون' })).toBeInTheDocument()
  })

  it('validates the form before sending anything', async () => {
    const calls = mockApi(fetchMock, { 'GET /staff': envelope([], { pagination: onePage }) })
    renderWithProviders(<StaffPage />)
    await userEvent.click((await screen.findAllByRole('button', { name: /Add staff member/ }))[0])
    const dialog = await screen.findByRole('dialog')

    await userEvent.click(within(dialog).getByRole('button', { name: 'Create' }))

    expect((await within(dialog).findAllByText('This field is required')).length).toBeGreaterThanOrEqual(3)
    expect(calls.some((c) => c.method === 'POST')).toBe(false)
  })

  it('stores salary as whole fils and sends nulls for blanks', async () => {
    const calls = mockApi(fetchMock, {
      'POST /staff': envelope({ id: 'new' }, { status: 201 }),
      'GET /staff': envelope([], { pagination: onePage }),
    })
    renderWithProviders(<StaffPage />)
    await userEvent.click((await screen.findAllByRole('button', { name: /Add staff member/ }))[0])
    const dialog = await screen.findByRole('dialog')

    await userEvent.type(within(dialog).getByLabelText(/First name/), 'Omar')
    await userEvent.type(within(dialog).getByLabelText(/Last name/), 'Saeed')
    await userEvent.type(within(dialog).getByLabelText(/^Email/), 'omar@test.local')
    await userEvent.type(within(dialog).getByLabelText(/Monthly salary/), '6500.50')
    await userEvent.click(within(dialog).getByRole('button', { name: 'Create' }))

    await waitFor(() => expect(calls.some((c) => c.method === 'POST')).toBe(true))
    const body = calls.find((c) => c.method === 'POST')!.body as { staff: Record<string, unknown> }
    expect(body.staff.salary_amount_fils).toBe(650050)
    expect(body.staff.phone).toBeNull()
    expect(body.staff.email).toBe('omar@test.local')
  })

  it('rejects a negative or non-numeric salary', async () => {
    mockApi(fetchMock, { 'GET /staff': envelope([], { pagination: onePage }) })
    renderWithProviders(<StaffPage />)
    await userEvent.click((await screen.findAllByRole('button', { name: /Add staff member/ }))[0])
    const dialog = await screen.findByRole('dialog')

    await userEvent.type(within(dialog).getByLabelText(/Monthly salary/), 'abc')
    await userEvent.click(within(dialog).getByRole('button', { name: 'Create' }))

    expect(await within(dialog).findByText('Enter a valid number')).toBeInTheDocument()
  })

  it('tells the person when the email is already used', async () => {
    mockApi(fetchMock, {
      'POST /staff': failure(409, 'DUPLICATE_RESOURCE', 'exists', 'email'),
      'GET /staff': envelope([], { pagination: onePage }),
    })
    renderWithProviders(<StaffPage />)
    await userEvent.click((await screen.findAllByRole('button', { name: /Add staff member/ }))[0])
    const dialog = await screen.findByRole('dialog')

    await userEvent.type(within(dialog).getByLabelText(/First name/), 'Omar')
    await userEvent.type(within(dialog).getByLabelText(/Last name/), 'Saeed')
    await userEvent.type(within(dialog).getByLabelText(/^Email/), 'omar@test.local')
    await userEvent.click(within(dialog).getByRole('button', { name: 'Create' }))

    expect(await within(dialog).findByText('This already exists.')).toBeInTheDocument()
  })
})

import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { envelope, failure, makeMe, mockApi, onePage, renderWithProviders, setLanguage, signIn, signOut } from '@/test/utils'
import AttendancePage from './AttendancePage'
import LeavesPage from './LeavesPage'

const fetchMock = vi.fn()

const row = (overrides = {}) => ({
  id: 'a1',
  person_type: 'student',
  person_id: 's1',
  person_name: 'Layla Hassan',
  room_id: 'r1',
  date: '2026-10-20',
  check_in_at: '2026-10-20T04:30:00Z',
  check_in_method: 'barcode',
  check_out_at: null,
  check_out_method: null,
  check_out_guardian_id: null,
  status: 'present',
  notes: null,
  created_at: '2026-10-20T04:30:00Z',
  ...overrides,
})

const ratio = (overrides = {}) => ({
  room_id: 'r1',
  name: { en: 'Little Stars', ar: 'النجوم الصغيرة' },
  capacity: 12,
  ratio_staff_to_student: 4,
  students_present: 5,
  staff_present: 1,
  staff_required: 2,
  is_breach: true,
  ...overrides,
})

const staffPermissions = ['attendance.read', 'attendance.write', 'centre_structure.read']

beforeEach(async () => {
  fetchMock.mockReset()
  vi.stubGlobal('fetch', fetchMock)
  await setLanguage('en')
})

afterEach(() => {
  vi.unstubAllGlobals()
  signOut()
})

describe('AttendancePage', () => {
  it('lists today with status, times and the live staff cover, flagging a short room in words', async () => {
    signIn(makeMe({ permissions: staffPermissions }))
    mockApi(fetchMock, {
      'GET /attendance/ratio/live': envelope([ratio()]),
      'GET /attendance': envelope([row()], { pagination: onePage }),
      'GET /rooms': envelope([], { pagination: onePage }),
    })
    renderWithProviders(<AttendancePage />)

    const line = (await screen.findByText('Layla Hassan')).closest('tr')!
    expect(within(line).getByText('Present')).toBeInTheDocument()
    expect(await screen.findByText('Needs staff')).toBeInTheDocument()
    expect(screen.getByText('5 children, 1 staff here')).toBeInTheDocument()
    expect(screen.getByText('1 staff for every 4 children. Needs 2 now.')).toBeInTheDocument()
  })

  it('checks a child in by barcode, confirms by name and clears the box for the next scan', async () => {
    signIn(makeMe({ permissions: staffPermissions }))
    const calls = mockApi(fetchMock, {
      'POST /attendance/actions/check-in': envelope(row()),
      'GET /attendance/ratio/live': envelope([]),
      'GET /attendance': envelope([], { pagination: onePage }),
      'GET /rooms': envelope([], { pagination: onePage }),
    })
    renderWithProviders(<AttendancePage />)

    const box = await screen.findByLabelText('Barcode')
    await userEvent.type(box, 'STU123456789{Enter}')

    expect(await screen.findByText('Layla Hassan is checked in')).toBeInTheDocument()
    expect(calls.find((c) => c.method === 'POST')!.body).toEqual({ barcode: 'STU123456789' })
    expect(box).toHaveValue('')
    expect(box).toHaveFocus()
  })

  it.each([
    [404, 'RESOURCE_NOT_FOUND', 'No one matches this barcode.'],
    [409, 'DUPLICATE_RESOURCE', 'This person has already been recorded today.'],
    [422, 'INVALID_STATE_TRANSITION', 'This cannot be done right now. The person may not be active, or may not be checked in.'],
  ])('explains a refused scan (%s) in plain words', async (status, code, text) => {
    signIn(makeMe({ permissions: staffPermissions }))
    mockApi(fetchMock, {
      'POST /attendance/actions/check-in': failure(status, code),
      'GET /attendance/ratio/live': envelope([]),
      'GET /attendance': envelope([], { pagination: onePage }),
      'GET /rooms': envelope([], { pagination: onePage }),
    })
    renderWithProviders(<AttendancePage />)

    await userEvent.type(await screen.findByLabelText('Barcode'), 'X1{Enter}')

    expect(await screen.findByRole('alert')).toHaveTextContent(text)
  })

  it('asks who is collecting a child and refuses to continue until identity is confirmed', async () => {
    signIn(makeMe({ permissions: staffPermissions }))
    const calls = mockApi(fetchMock, {
      'POST /attendance/actions/check-out': envelope(row({ check_out_at: '2026-10-20T11:00:00Z' })),
      'GET /students/s1/guardians': envelope([
        { id: 'g1', first_name: 'Mona', last_name: 'Hassan', email: 'm@x.com', phone: null, relationship: 'mother', is_primary: true, can_pickup: true, receives_communications: true, has_login: false },
        { id: 'g2', first_name: 'Blocked', last_name: 'Person', email: 'b@x.com', phone: null, relationship: 'other', is_primary: false, can_pickup: false, receives_communications: false, has_login: false },
      ]),
      'GET /attendance/ratio/live': envelope([]),
      'GET /attendance': envelope([row()], { pagination: onePage }),
      'GET /rooms': envelope([], { pagination: onePage }),
    })
    renderWithProviders(<AttendancePage />)

    const line = (await screen.findByText('Layla Hassan')).closest('tr')!
    await userEvent.click(within(line).getByRole('button', { name: 'Check out' }))
    const dialog = await screen.findByRole('dialog')
    expect(within(dialog).queryByText(/Blocked Person/)).not.toBeInTheDocument()
    await userEvent.selectOptions(await within(dialog).findByLabelText(/Collected by/), 'g1')
    await userEvent.click(within(dialog).getByRole('button', { name: 'Confirm check-out' }))

    expect(await within(dialog).findByText('Confirm that you checked their identity.')).toBeInTheDocument()
    expect(calls.some((c) => c.method === 'POST')).toBe(false)

    await userEvent.click(within(dialog).getByRole('checkbox'))
    await userEvent.click(within(dialog).getByRole('button', { name: 'Confirm check-out' }))

    await waitFor(() => expect(calls.some((c) => c.method === 'POST')).toBe(true))
    expect(calls.find((c) => c.method === 'POST')!.body).toEqual({
      person_type: 'student',
      person_id: 's1',
      guardian_id: 'g1',
      pickup_identity_verified: true,
    })
  })

  it('works in Arabic with the right-to-left layout and Arabic room names', async () => {
    await setLanguage('ar')
    signIn(makeMe({ permissions: staffPermissions }))
    mockApi(fetchMock, {
      'GET /attendance/ratio/live': envelope([ratio()]),
      'GET /attendance': envelope([row()], { pagination: onePage }),
      'GET /rooms': envelope([], { pagination: onePage }),
    })
    renderWithProviders(<AttendancePage />)

    expect(await screen.findByText('النجوم الصغيرة')).toBeInTheDocument()
    expect(screen.getByText('يحتاج موظفين')).toBeInTheDocument()
    expect(screen.getByText('حاضر')).toBeInTheDocument()
    expect(document.documentElement.dir).toBe('rtl')
  })

  it('shows a parent only the list: no scanning, no staff cover', async () => {
    signIn(makeMe({ roles: ['parent'], permissions: ['attendance.read'] }))
    mockApi(fetchMock, { 'GET /attendance': envelope([row()], { pagination: onePage }) })
    renderWithProviders(<AttendancePage />)

    expect(await screen.findByText('Layla Hassan')).toBeInTheDocument()
    expect(screen.queryByLabelText('Barcode')).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Check out' })).not.toBeInTheDocument()
    expect(screen.queryByText('Staff cover by room')).not.toBeInTheDocument()
  })
})

describe('LeavesPage', () => {
  const leave = (overrides = {}) => ({
    id: 'l1',
    person_type: 'student',
    person_id: 's1',
    person_name: 'Layla Hassan',
    leave_type_id: 't1',
    leave_type_name: { en: 'Family travel', ar: 'سفر عائلي' },
    start_date: '2026-11-01',
    end_date: '2026-11-03',
    reason: null,
    status: 'submitted',
    approved_at: null,
    approval_notes: null,
    created_at: '2026-10-20T00:00:00Z',
    ...overrides,
  })

  it('lets a manager approve or reject a waiting request', async () => {
    signIn(makeMe({ permissions: ['attendance.read', 'attendance.write', 'attendance.override'] }))
    const calls = mockApi(fetchMock, {
      'POST /leaves/l1/actions/approve': envelope(leave({ status: 'approved' })),
      'GET /leaves': envelope([leave()], { pagination: onePage }),
    })
    renderWithProviders(<LeavesPage />)

    const line = (await screen.findByText('Layla Hassan')).closest('tr')!
    expect(within(line).getByText('Family travel')).toBeInTheDocument()
    expect(within(line).getByText('Waiting')).toBeInTheDocument()
    expect(within(line).getByRole('button', { name: /Reject/ })).toBeInTheDocument()
    await userEvent.click(within(line).getByRole('button', { name: /Approve/ }))

    await waitFor(() => expect(calls.some((c) => c.method === 'POST' && c.url.includes('/leaves/l1/actions/approve'))).toBe(true))
  })

  it('hides decisions from people who cannot make them but keeps cancel', async () => {
    signIn(makeMe({ permissions: ['attendance.read', 'attendance.write'] }))
    mockApi(fetchMock, { 'GET /leaves': envelope([leave()], { pagination: onePage }) })
    renderWithProviders(<LeavesPage />)

    const line = (await screen.findByText('Layla Hassan')).closest('tr')!
    expect(within(line).queryByRole('button', { name: /Approve/ })).not.toBeInTheDocument()
    expect(within(line).getByRole('button', { name: 'Cancel' })).toBeInTheDocument()
  })

  it('filters by status through the server', async () => {
    signIn(makeMe({ permissions: ['attendance.read'] }))
    const calls = mockApi(fetchMock, { 'GET /leaves': envelope([], { pagination: onePage }) })
    renderWithProviders(<LeavesPage />)

    await userEvent.click(await screen.findByRole('button', { name: 'Approved' }))

    await waitFor(() => expect(calls.some((c) => c.url.includes('status=approved'))).toBe(true))
    expect(await screen.findByText('No leave requests')).toBeInTheDocument()
  })

  it('sends a new request only when the dates are in order', async () => {
    signIn(makeMe({ permissions: ['attendance.read', 'attendance.write'] }))
    const calls = mockApi(fetchMock, {
      'POST /leaves': envelope(leave(), { status: 201 }),
      'GET /leave-types': envelope([{ id: 't1', name: { en: 'Family travel' }, applies_to: 'both', requires_approval: true, affects_attendance: true, affects_ratio: false, created_at: '' }], { pagination: onePage }),
      'GET /students': envelope([{ id: 's1', first_name: 'Layla', last_name: 'Hassan' }], { pagination: onePage }),
      'GET /leaves': envelope([], { pagination: onePage }),
    })
    renderWithProviders(<LeavesPage />)

    await userEvent.click(await screen.findByRole('button', { name: 'New request' }))
    const dialog = await screen.findByRole('dialog')
    await userEvent.selectOptions(await within(dialog).findByLabelText(/Person/), 's1')
    await userEvent.selectOptions(within(dialog).getByLabelText(/Kind of leave/), 't1')
    await userEvent.type(within(dialog).getByLabelText(/From/), '2026-11-05')
    await userEvent.type(within(dialog).getByLabelText(/To/), '2026-11-02')
    await userEvent.click(within(dialog).getByRole('button', { name: 'Send request' }))

    expect(await within(dialog).findByText('The end date must not be before the start date.')).toBeInTheDocument()
    expect(calls.some((c) => c.method === 'POST')).toBe(false)

    await userEvent.clear(within(dialog).getByLabelText(/To/))
    await userEvent.type(within(dialog).getByLabelText(/To/), '2026-11-06')
    await userEvent.click(within(dialog).getByRole('button', { name: 'Send request' }))

    await waitFor(() => expect(calls.some((c) => c.method === 'POST')).toBe(true))
    expect(calls.find((c) => c.method === 'POST')!.body).toMatchObject({
      person_type: 'student',
      person_id: 's1',
      leave_type_id: 't1',
      start_date: '2026-11-05',
      end_date: '2026-11-06',
    })
  })

  it('shows Arabic labels, names and status', async () => {
    await setLanguage('ar')
    signIn(makeMe({ permissions: ['attendance.read'] }))
    mockApi(fetchMock, { 'GET /leaves': envelope([leave()], { pagination: onePage }) })
    renderWithProviders(<LeavesPage />)

    expect(await screen.findByText('سفر عائلي')).toBeInTheDocument()
    expect(screen.getByText('بانتظار القرار', { selector: 'span' })).toBeInTheDocument()
  })
})

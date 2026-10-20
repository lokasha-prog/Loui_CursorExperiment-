import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { Route, Routes } from 'react-router-dom'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { envelope, failure, makeMe, mockApi, onePage, renderWithProviders, setLanguage, signIn, signOut } from '@/test/utils'
import StudentDetailPage from './StudentDetailPage'

const fetchMock = vi.fn()

const detail = (overrides = {}) => ({
  id: 's1',
  first_name: 'Mariam',
  last_name: 'Hassan',
  first_name_arabic: 'مريم',
  last_name_arabic: 'حسن',
  date_of_birth: '2025-03-10',
  gender: 'female',
  room_id: 'room-1',
  group_id: null,
  program_id: null,
  display_name: null,
  key_person_staff_id: null,
  status: 'active',
  status_changed_at: '2026-09-01T00:00:00Z',
  status_reason: null,
  start_date: '2026-01-05',
  end_date: null,
  barcode: 'STU555',
  nationality: 'AE',
  religion: null,
  primary_language: 'Arabic',
  medical_notes: 'Asthma inhaler',
  allergy_info: 'Peanuts',
  dietary_requirements: null,
  custody_notes: 'Court order 1',
  passport_number: 'P1234',
  emirates_id: null,
  label_ids: [],
  created_at: '2026-01-01T00:00:00Z',
  ...overrides,
})

const guardian = (overrides = {}) => ({
  id: 'g1',
  first_name: 'Aisha',
  last_name: 'Hassan',
  email: 'aisha@test.local',
  phone: '+971500000000',
  relationship: 'mother',
  is_primary: true,
  can_pickup: true,
  receives_communications: true,
  has_login: false,
  ...overrides,
})

const rooms = envelope([{ id: 'room-1', name: { en: 'Little Stars', ar: 'النجوم الصغيرة' } }], { pagination: { limit: 100, has_more: false, next_cursor: null } })

function renderDetail() {
  return renderWithProviders(
    <Routes>
      <Route path="/students/:id" element={<StudentDetailPage />} />
    </Routes>,
    { route: '/students/s1' },
  )
}

beforeEach(async () => {
  fetchMock.mockReset()
  vi.stubGlobal('fetch', fetchMock)
  vi.stubGlobal('confirm', vi.fn(() => true))
  await setLanguage('en')
  signIn(makeMe({ permissions: ['students.read', 'students.write', 'centre_structure.read', 'user_management.manage', 'staff.read'] }))
})

afterEach(() => {
  vi.unstubAllGlobals()
  signOut()
})

describe('StudentDetailPage', () => {
  it('shows the profile, health information and guardians', async () => {
    mockApi(fetchMock, {
      'GET /students/s1/guardians': envelope([guardian()]),
      'GET /students/s1': envelope(detail()),
      'GET /rooms': rooms,
    })
    renderDetail()

    expect(await screen.findByRole('heading', { name: 'Mariam Hassan' })).toBeInTheDocument()
    expect(screen.getByText('مريم حسن')).toHaveAttribute('dir', 'rtl')
    expect(screen.getByText('Peanuts')).toBeInTheDocument()
    expect(screen.getByText('Asthma inhaler')).toBeInTheDocument()
    expect(screen.getByText('Little Stars')).toBeInTheDocument()
    expect(await screen.findByText('Aisha Hassan')).toBeInTheDocument()
    expect(screen.getByText('aisha@test.local')).toBeInTheDocument()
  })

  it('shows administrators the custody and identity details, but not parents', async () => {
    mockApi(fetchMock, { 'GET /students/s1/guardians': envelope([]), 'GET /students/s1': envelope(detail()), 'GET /rooms': rooms })
    const admin = renderDetail()
    expect(await screen.findByText('Court order 1')).toBeInTheDocument()
    admin.unmount()

    signIn(makeMe({ roles: ['parent'], permissions: ['students.read'] }))
    mockApi(fetchMock, { 'GET /students/s1/guardians': envelope([]), 'GET /students/s1': envelope(detail({ custody_notes: null, passport_number: null })) })
    renderDetail()

    expect(await screen.findByText('Peanuts')).toBeInTheDocument()
    expect(screen.queryByText('Custody notes')).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Apply change' })).not.toBeInTheDocument()
  })

  it('says "not found" without leaking anything when the student is not visible', async () => {
    mockApi(fetchMock, { 'GET /students/s1': failure(404, 'RESOURCE_NOT_FOUND'), 'GET /students/s1/guardians': failure(404, 'RESOURCE_NOT_FOUND') })
    renderDetail()

    expect(await screen.findByText('Student not found')).toBeInTheDocument()
    expect(screen.queryByText('Mariam')).not.toBeInTheDocument()
  })

  it('offers only the allowed next statuses and applies the change with a reason', async () => {
    const calls = mockApi(fetchMock, {
      'POST /students/s1/actions/transition-status': envelope(detail({ status: 'on_hold', status_reason: 'Travel' })),
      'GET /students/s1/guardians': envelope([]),
      'GET /students/s1': envelope(detail()),
      'GET /rooms': rooms,
    })
    renderDetail()
    const select = await screen.findByLabelText('New status')

    const options = within(select).getAllByRole('option').map((o) => o.textContent)
    expect(options).toEqual(['—', 'On hold', 'Inactive', 'Withdrawn', 'Transferred'])
    expect(screen.getByRole('button', { name: 'Apply change' })).toBeDisabled()

    await userEvent.selectOptions(select, 'on_hold')
    await userEvent.type(screen.getByLabelText(/Reason/), 'Travel')
    await userEvent.click(screen.getByRole('button', { name: 'Apply change' }))

    await waitFor(() => expect(calls.find((c) => c.method === 'POST')?.body).toEqual({ status: 'on_hold', reason: 'Travel' }))
  })

  it('shows no status controls once a student has withdrawn', async () => {
    mockApi(fetchMock, { 'GET /students/s1/guardians': envelope([]), 'GET /students/s1': envelope(detail({ status: 'withdrawn' })), 'GET /rooms': rooms })
    renderDetail()

    await screen.findByRole('heading', { name: 'Mariam Hassan' })
    expect(screen.queryByLabelText('New status')).not.toBeInTheDocument()
  })

  it('adds a guardian after validating the details', async () => {
    const calls = mockApi(fetchMock, {
      'POST /students/s1/guardians': envelope(guardian({ id: 'g2' }), { status: 201 }),
      'GET /students/s1/guardians': envelope([]),
      'GET /students/s1': envelope(detail()),
      'GET /rooms': rooms,
    })
    renderDetail()
    await userEvent.click(await screen.findByRole('button', { name: /^Add$/ }))
    const dialog = await screen.findByRole('dialog')

    await userEvent.click(within(dialog).getByRole('button', { name: 'Add' }))
    expect((await within(dialog).findAllByText('This field is required')).length).toBe(3)

    await userEvent.type(within(dialog).getByLabelText(/Guardian first name/), 'Khalid')
    await userEvent.type(within(dialog).getByLabelText(/Guardian last name/), 'Hassan')
    await userEvent.type(within(dialog).getByLabelText(/Guardian email/), 'khalid@test.local')
    await userEvent.selectOptions(within(dialog).getByLabelText(/Relationship/), 'father')
    await userEvent.click(within(dialog).getByRole('button', { name: 'Add' }))

    await waitFor(() => expect(calls.some((c) => c.method === 'POST')).toBe(true))
    expect(calls.find((c) => c.method === 'POST')!.body).toMatchObject({ email: 'khalid@test.local', relationship: 'father', can_pickup: true })
  })

  it('removes a guardian after confirmation', async () => {
    const calls = mockApi(fetchMock, {
      'DELETE /students/s1/guardians/g1': new Response(null, { status: 204 }),
      'GET /students/s1/guardians': envelope([guardian()]),
      'GET /students/s1': envelope(detail()),
      'GET /rooms': rooms,
    })
    renderDetail()

    await userEvent.click(await screen.findByRole('button', { name: /Remove: Aisha Hassan/ }))

    await waitFor(() => expect(calls.some((c) => c.method === 'DELETE')).toBe(true))
  })

  it('lets a user manager create a sign-in for a guardian who has none', async () => {
    mockApi(fetchMock, {
      'GET /auth/setup-status': envelope({ setup_required: false, password_min_length: 12 }),
      'GET /students/s1/guardians': envelope([guardian(), guardian({ id: 'g2', first_name: 'Khalid', has_login: true, email: 'k@test.local' })]),
      'GET /students/s1': envelope(detail()),
      'GET /staff': envelope([], { pagination: onePage }),
      'GET /rooms': rooms,
    })
    renderDetail()

    const buttons = await screen.findAllByRole('button', { name: 'Create sign-in' })
    expect(buttons).toHaveLength(1)
    await userEvent.click(buttons[0])
    const dialog = await screen.findByRole('dialog')

    expect(within(dialog).getByLabelText(/^Email/)).toHaveValue('aisha@test.local')
    expect(within(dialog).getByLabelText(/Role/)).toBeDisabled()
    expect(within(dialog).getByLabelText(/Role/)).toHaveValue('parent')
  })

  it('is fully mirrored and translated in Arabic', async () => {
    mockApi(fetchMock, { 'GET /students/s1/guardians': envelope([guardian()]), 'GET /students/s1': envelope(detail()), 'GET /rooms': rooms })
    await setLanguage('ar')
    renderDetail()

    expect(await screen.findByText('نظرة عامة')).toBeInTheDocument()
    expect(screen.getByText('أولياء الأمور')).toBeInTheDocument()
    expect(screen.getByText('النجوم الصغيرة')).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'العودة إلى الطلاب' })).toBeInTheDocument()
    expect(document.documentElement.dir).toBe('rtl')
  })
})

import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { Route, Routes } from 'react-router-dom'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import type { StudentSummary } from '@/api/types'
import { envelope, failure, makeMe, renderWithProviders, setLanguage, signIn, signOut } from '@/test/utils'
import StudentsPage from './StudentsPage'
import { studentSchema, toCreateRequest, type StudentFormValues } from './StudentForm'

const fetchMock = vi.fn()

const student = (overrides: Partial<StudentSummary> = {}): StudentSummary => ({
  id: 'a',
  first_name: 'Mariam',
  last_name: 'Hassan',
  first_name_arabic: 'مريم',
  last_name_arabic: 'حسن',
  date_of_birth: '2025-03-10',
  gender: 'female',
  room_id: 'room-1',
  group_id: null,
  program_id: null,
  status: 'active',
  barcode: 'STU123456789',
  created_at: '2026-10-01T00:00:00Z',
  ...overrides,
})

function serve(students: StudentSummary[], extra: Record<string, () => Response> = {}) {
  fetchMock.mockImplementation((url: string) => {
    const u = String(url)
    const key = Object.keys(extra).find((k) => u.includes(k))
    if (key) return Promise.resolve(extra[key]())
    if (u.includes('/rooms')) {
      return Promise.resolve(envelope([{ id: 'room-1', name: { en: 'Little Stars', ar: 'النجوم الصغيرة' } }], { pagination: { limit: 100, has_more: false, next_cursor: null } }))
    }
    if (u.includes('/programs')) return Promise.resolve(envelope([], { pagination: { limit: 100, has_more: false, next_cursor: null } }))
    if (u.includes('/students')) return Promise.resolve(envelope(students, { pagination: { limit: 25, has_more: false, next_cursor: null } }))
    return Promise.resolve(failure(404, 'RESOURCE_NOT_FOUND'))
  })
}

beforeEach(async () => {
  fetchMock.mockReset()
  vi.stubGlobal('fetch', fetchMock)
  await setLanguage('en')
  signIn(makeMe({ permissions: ['students.read', 'students.write', 'centre_structure.read'] }))
})

afterEach(() => {
  vi.unstubAllGlobals()
  signOut()
})

function renderPage() {
  return renderWithProviders(
    <Routes>
      <Route path="/students" element={<StudentsPage />} />
    </Routes>,
    { route: '/students' },
  )
}

describe('StudentsPage', () => {
  it('lists students with room, status and the Arabic name', async () => {
    serve([student()])
    renderPage()

    const row = (await screen.findByText('Mariam Hassan')).closest('tr')!
    expect(within(row).getByText('Little Stars')).toBeInTheDocument()
    expect(within(row).getByText('Active')).toBeInTheDocument()
    expect(within(row).getByText('مريم حسن')).toHaveAttribute('dir', 'rtl')
    expect(within(row).getByText('STU123456789')).toHaveAttribute('dir', 'ltr')
  })

  it('shows Arabic names, room and status in Arabic mode', async () => {
    serve([student()])
    await setLanguage('ar')
    renderPage()

    const row = (await screen.findByText('مريم حسن')).closest('tr')!
    expect(within(row).getByText('النجوم الصغيرة')).toBeInTheDocument()
    expect(within(row).getByText('نشط')).toBeInTheDocument()
  })

  it('shows a friendly empty state with an add button for admins', async () => {
    serve([])
    renderPage()

    expect(await screen.findByText('No students yet')).toBeInTheDocument()
    expect(screen.getAllByRole('button', { name: /Add student/ }).length).toBeGreaterThan(0)
  })

  it('hides create and search tools from a parent and uses their wording', async () => {
    signIn(makeMe({ roles: ['parent'], permissions: ['students.read'] }))
    serve([])
    renderPage()

    expect(await screen.findByText('No children are linked to your account yet.')).toBeInTheDocument()
    expect(screen.getByRole('heading', { name: 'My children' })).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /Add student/ })).not.toBeInTheDocument()
    expect(screen.queryByPlaceholderText('Search by name or barcode')).not.toBeInTheDocument()
  })

  it('explains a server failure in plain language and lets people retry', async () => {
    fetchMock.mockResolvedValue(failure(500, 'INTERNAL_ERROR'))
    renderPage()

    expect(await screen.findByText('Something went wrong on our side. Please try again.')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Try again' })).toBeInTheDocument()
  })

  it('searches after typing stops', async () => {
    serve([student()])
    renderPage()
    await screen.findByText('Mariam Hassan')

    await userEvent.type(screen.getByPlaceholderText('Search by name or barcode'), 'Mar')

    await waitFor(() => expect(fetchMock.mock.calls.some(([u]) => String(u).includes('search=Mar'))).toBe(true))
  })

  it('opens the create dialog and refuses to submit an empty form', async () => {
    serve([])
    renderPage()
    await userEvent.click((await screen.findAllByRole('button', { name: /Add student/ }))[0])

    const dialog = await screen.findByRole('dialog')
    await userEvent.click(within(dialog).getByRole('button', { name: 'Create' }))

    expect((await within(dialog).findAllByText('This field is required')).length).toBeGreaterThanOrEqual(3)
    expect(fetchMock.mock.calls.some(([, init]) => init?.method === 'POST')).toBe(false)
  })

  it('creates a student with a guardian and shows the confirmation', async () => {
    serve([], { '/students': undefined as never })
    fetchMock.mockImplementation((url: string, init?: RequestInit) => {
      const u = String(url)
      if (init?.method === 'POST' && u.endsWith('/students')) return Promise.resolve(envelope({ id: 'new' }, { status: 201 }))
      if (u.includes('/rooms') || u.includes('/programs')) return Promise.resolve(envelope([], { pagination: { limit: 100, has_more: false, next_cursor: null } }))
      return Promise.resolve(envelope([], { pagination: { limit: 25, has_more: false, next_cursor: null } }))
    })
    renderPage()
    await userEvent.click((await screen.findAllByRole('button', { name: /Add student/ }))[0])
    const dialog = await screen.findByRole('dialog')

    await userEvent.type(within(dialog).getByLabelText(/^First name\*?$/), 'Omar')
    await userEvent.type(within(dialog).getByLabelText(/^Last name\*?$/), 'Saeed')
    await userEvent.type(within(dialog).getByLabelText(/Date of birth/), '2023-05-01')
    await userEvent.type(within(dialog).getByLabelText(/Guardian first name/), 'Fatima')
    await userEvent.type(within(dialog).getByLabelText(/Guardian last name/), 'Saeed')
    await userEvent.type(within(dialog).getByLabelText(/Guardian email/), 'fatima@test.local')
    await userEvent.click(within(dialog).getByRole('button', { name: 'Create' }))

    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument())
    const post = fetchMock.mock.calls.find(([, init]) => init?.method === 'POST')!
    const body = JSON.parse(post[1].body)
    expect(body.student.first_name).toBe('Omar')
    expect(body.student.room_id).toBeNull()
    expect(body.guardians[0]).toMatchObject({ email: 'fatima@test.local', is_primary: true })
  })
})

describe('student form rules', () => {
  const valid: StudentFormValues = {
    firstName: 'Mariam',
    lastName: 'Hassan',
    dateOfBirth: '2024-01-01',
    guardianRelationship: 'mother',
  }

  it('accepts a minimal valid student', () => {
    expect(studentSchema.safeParse(valid).success).toBe(true)
  })

  it('rejects a birth date in the future', () => {
    const result = studentSchema.safeParse({ ...valid, dateOfBirth: '2999-01-01' })
    expect(result.success).toBe(false)
  })

  it('requires a complete guardian once any guardian field is filled', () => {
    const result = studentSchema.safeParse({ ...valid, guardianFirst: 'Fatima' })
    expect(result.success).toBe(false)
    const paths = result.success ? [] : result.error.issues.map((i) => i.path[0])
    expect(paths).toEqual(expect.arrayContaining(['guardianLast', 'guardianEmail']))
  })

  it('checks the guardian email format', () => {
    const result = studentSchema.safeParse({ ...valid, guardianFirst: 'F', guardianLast: 'S', guardianEmail: 'bad' })
    expect(result.success).toBe(false)
  })

  it('turns blank optional fields into nulls and omits the guardian when none was entered', () => {
    const request = toCreateRequest({ ...valid, gender: '', roomId: '', medicalNotes: '  ' })

    expect(request.student.gender).toBeNull()
    expect(request.student.room_id).toBeNull()
    expect(request.student.medical_notes).toBeNull()
    expect(request.guardians).toBeNull()
  })

  it('keeps Arabic names exactly as typed', () => {
    const request = toCreateRequest({ ...valid, firstNameAr: 'مريم', lastNameAr: 'حسن' })

    expect(request.student.first_name_arabic).toBe('مريم')
    expect(request.student.last_name_arabic).toBe('حسن')
  })
})

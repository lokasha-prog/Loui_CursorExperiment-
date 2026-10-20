import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import type { StudentDetail } from '@/api/types'
import { envelope, makeMe, mockApi, onePage, renderWithProviders, setLanguage, signIn, signOut } from '@/test/utils'
import { StudentDialog } from './StudentForm'

const fetchMock = vi.fn()

const room = (id: string, en: string, min: number, max: number) => ({
  id, name: { en, ar: en }, capacity: 10, min_age_months: min, max_age_months: max, ratio_staff_to_student: 4, is_active: true, student_count: 0, created_at: '',
})
const program = (id: string, en: string, min: number, max: number) => ({
  id, name: { en }, description: null, days_per_week: 5, start_time: '07:30:00', end_time: '14:00:00', age_group: 'x', min_age_months: min, max_age_months: max, is_active: true, created_at: '',
})

const routes = {
  'GET /rooms': envelope([room('r1', 'Little Stars', 12, 24), room('r2', 'Busy Bees', 24, 36), room('r3', 'Sunshine', 36, 60)], { pagination: onePage }),
  'GET /programs': envelope([program('p1', 'Toddler day', 12, 36), program('p2', 'Pre-school', 36, 60)], { pagination: onePage }),
}

function monthsAgo(months: number) {
  const d = new Date()
  d.setMonth(d.getMonth() - months)
  d.setDate(d.getDate() - 3)
  return d.toISOString().slice(0, 10)
}

const existing: StudentDetail = {
  id: 's1', first_name: 'Mariam', last_name: 'Hassan', first_name_arabic: 'مريم', last_name_arabic: 'حسن', date_of_birth: monthsAgo(20), gender: 'female',
  room_id: 'r1', group_id: 'g1', program_id: 'p1', status: 'active', barcode: 'STU1', display_name: null, key_person_staff_id: 'k1', status_changed_at: '', status_reason: null,
  start_date: '2026-01-05', end_date: null, nationality: 'AE', religion: 'Islam', primary_language: 'ar', medical_notes: null, allergy_info: 'Peanuts', dietary_requirements: null,
  custody_notes: 'Court order', passport_number: 'P123', emirates_id: '784-2022-1', photo_media_id: null, id_photo_media_id: null, label_ids: ['l1'], created_at: '',
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

describe('StudentDialog', () => {
  it('offers rooms and programmes only after the date of birth, and only those that fit the age', async () => {
    mockApi(fetchMock, routes)
    renderWithProviders(<StudentDialog open onOpenChange={() => undefined} />)
    const dialog = await screen.findByRole('dialog')
    const roomSelect = within(dialog).getByLabelText('Room')

    expect(roomSelect).toBeDisabled()
    expect(within(dialog).getByText(/Enter the date of birth first/)).toBeInTheDocument()

    await userEvent.type(within(dialog).getByLabelText(/Date of birth/), monthsAgo(30))

    await waitFor(() => expect(roomSelect).toBeEnabled())
    const rooms = within(roomSelect).getAllByRole('option').map((o) => o.textContent)
    expect(rooms.join('|')).toContain('Busy Bees')
    expect(rooms.join('|')).not.toContain('Little Stars')
    expect(rooms.join('|')).not.toContain('Sunshine')
    const programs = within(within(dialog).getByLabelText('Program')).getAllByRole('option').map((o) => o.textContent)
    expect(programs).toContain('Toddler day')
    expect(programs).not.toContain('Pre-school')
    expect(within(dialog).getByLabelText('Program')).toHaveValue('p1')
  })

  it('drops a room that no longer fits when the date of birth changes', async () => {
    mockApi(fetchMock, routes)
    renderWithProviders(<StudentDialog open onOpenChange={() => undefined} />)
    const dialog = await screen.findByRole('dialog')
    const dob = within(dialog).getByLabelText(/Date of birth/)

    await userEvent.type(dob, monthsAgo(30))
    await userEvent.selectOptions(await within(dialog).findByLabelText('Room'), 'r2')
    await userEvent.clear(dob)
    await userEvent.type(dob, monthsAgo(50))

    await waitFor(() => expect(within(dialog).getByLabelText('Room')).toHaveValue(''))
  })

  it('has only male and female for gender and a nationality list that stores the country code', async () => {
    const calls = mockApi(fetchMock, { ...routes, 'POST /students': envelope({ id: 's9' }, { status: 201 }) })
    renderWithProviders(<StudentDialog open onOpenChange={() => undefined} />)
    const dialog = await screen.findByRole('dialog')

    const genders = within(within(dialog).getByLabelText('Gender')).getAllByRole('option').map((o) => o.textContent)
    expect(genders).toEqual(['—', 'Male', 'Female'])
    const nationality = within(dialog).getByLabelText('Nationality')
    expect(within(nationality).getByRole('option', { name: 'United Arab Emirates' })).toBeInTheDocument()
    expect(within(nationality).getAllByRole('option').length).toBeGreaterThan(200)

    await userEvent.type(within(dialog).getByLabelText(/First name/, { selector: '#firstName' }), 'Sara')
    await userEvent.type(within(dialog).getByLabelText(/Last name/, { selector: '#lastName' }), 'Ali')
    await userEvent.type(within(dialog).getByLabelText(/Date of birth/), monthsAgo(30))
    await userEvent.selectOptions(nationality, 'AE')
    await userEvent.type(within(dialog).getByLabelText(/Student ID number/), '784-2022-7777777-1')
    await userEvent.type(within(dialog).getByLabelText(/Guardian ID number/), '784-1990-1111111-1')
    await userEvent.type(within(dialog).getByLabelText(/Guardian first name/), 'Huda')
    await userEvent.type(within(dialog).getByLabelText(/Guardian last name/), 'Ali')
    await userEvent.type(within(dialog).getByLabelText(/Guardian email/), 'huda@test.local')
    await userEvent.click(within(dialog).getByRole('button', { name: 'Create' }))

    await waitFor(() => expect(calls.some((c) => c.method === 'POST')).toBe(true))
    const body = calls.find((c) => c.method === 'POST')!.body as { student: Record<string, unknown>; guardians: Record<string, unknown>[] }
    expect(body.student).toMatchObject({ nationality: 'AE', emirates_id: '784-2022-7777777-1', photo_media_id: null, id_photo_media_id: null })
    expect(body.guardians[0]).toMatchObject({ emirates_id: '784-1990-1111111-1' })
  })

  it('lets an administrator edit names in English and Arabic and keeps what the form does not show', async () => {
    const calls = mockApi(fetchMock, { ...routes, 'PATCH /students/s1': envelope(existing) })
    renderWithProviders(<StudentDialog open onOpenChange={() => undefined} student={existing} />)
    const dialog = await screen.findByRole('dialog')

    expect(within(dialog).getByRole('heading', { name: 'Edit student' })).toBeInTheDocument()
    await waitFor(() => expect(within(dialog).getByLabelText('Room')).toHaveValue('r1'))
    expect(within(dialog).getByLabelText('Program')).toHaveValue('p1')
    expect(within(dialog).queryByLabelText(/Guardian ID number/)).not.toBeInTheDocument()
    const arabic = within(dialog).getByLabelText(/First name \(Arabic\)/)
    expect(arabic).toHaveValue('مريم')
    await userEvent.clear(arabic)
    await userEvent.type(arabic, 'مريم علي')
    const first = within(dialog).getByLabelText(/First name/, { selector: '#firstName' })
    await userEvent.clear(first)
    await userEvent.type(first, 'Maryam')
    await userEvent.click(within(dialog).getByRole('button', { name: 'Save' }))

    await waitFor(() => expect(calls.some((c) => c.method === 'PATCH')).toBe(true))
    const body = calls.find((c) => c.method === 'PATCH')!.body as { student: Record<string, unknown> }
    expect(body.student).toMatchObject({
      first_name: 'Maryam', first_name_arabic: 'مريم علي', last_name_arabic: 'حسن', room_id: 'r1', program_id: 'p1',
      group_id: 'g1', key_person_staff_id: 'k1', religion: 'Islam', custody_notes: 'Court order', passport_number: 'P123', label_ids: ['l1'],
    })
  })

  it('shows the nationality list in Arabic names', async () => {
    await setLanguage('ar')
    mockApi(fetchMock, routes)
    renderWithProviders(<StudentDialog open onOpenChange={() => undefined} />)
    const dialog = await screen.findByRole('dialog')

    const nationality = within(dialog).getByLabelText('الجنسية')
    expect(within(nationality).getByRole('option', { name: 'الإمارات العربية المتحدة' })).toBeInTheDocument()
  })
})

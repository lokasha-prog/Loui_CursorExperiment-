import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { envelope, failure, makeMe, mockApi, onePage, renderWithProviders, setLanguage, signIn, signOut } from '@/test/utils'
import RoomsPage from './RoomsPage'

const fetchMock = vi.fn()

const room = {
  id: 'r1',
  name: { en: 'Little Stars', ar: 'النجوم الصغيرة' },
  capacity: 12,
  min_age_months: 12,
  max_age_months: 24,
  ratio_staff_to_student: 4,
  is_active: true,
  student_count: 3,
  created_at: '2026-01-01T00:00:00Z',
}

const empty = envelope([], { pagination: onePage })

beforeEach(async () => {
  fetchMock.mockReset()
  vi.stubGlobal('fetch', fetchMock)
  await setLanguage('en')
  signIn(makeMe({ permissions: ['centre_structure.read', 'centre_structure.write'] }))
})

afterEach(() => {
  vi.unstubAllGlobals()
  signOut()
})

describe('RoomsPage', () => {
  it('lists rooms with ratio, ages and occupancy', async () => {
    mockApi(fetchMock, { 'GET /rooms': envelope([room], { pagination: onePage }) })
    renderWithProviders(<RoomsPage />)

    const row = (await screen.findByText('Little Stars')).closest('tr')!
    expect(within(row).getByText('1 : 4')).toBeInTheDocument()
    expect(within(row).getByText('12–24 months')).toBeInTheDocument()
    expect(within(row).getByText('3 / 12')).toBeInTheDocument()
    expect(within(row).getByText('Active')).toBeInTheDocument()
  })

  it('shows room names and labels in Arabic', async () => {
    await setLanguage('ar')
    mockApi(fetchMock, { 'GET /rooms': envelope([room], { pagination: onePage }) })
    renderWithProviders(<RoomsPage />)

    expect(await screen.findByText('النجوم الصغيرة')).toBeInTheDocument()
    expect(screen.getByRole('tab', { name: 'الغرف' })).toBeInTheDocument()
    expect(screen.getByText('نشطة')).toBeInTheDocument()
  })

  it('shows an empty state that invites creating the first room', async () => {
    mockApi(fetchMock, { 'GET /rooms': empty })
    renderWithProviders(<RoomsPage />)

    expect(await screen.findByText('No rooms yet')).toBeInTheDocument()
  })

  it('hides every add button from read-only users', async () => {
    signIn(makeMe({ permissions: ['centre_structure.read'] }))
    mockApi(fetchMock, { 'GET /rooms': envelope([room], { pagination: onePage }) })
    renderWithProviders(<RoomsPage />)

    await screen.findByText('Little Stars')
    expect(screen.queryByRole('button', { name: /Add room/ })).not.toBeInTheDocument()
  })

  it('validates room details, including the age range', async () => {
    const calls = mockApi(fetchMock, { 'GET /rooms': empty })
    renderWithProviders(<RoomsPage />)
    await userEvent.click((await screen.findAllByRole('button', { name: /Add room/ }))[0])
    const dialog = await screen.findByRole('dialog')

    await userEvent.type(within(dialog).getByLabelText(/Capacity/), '12')
    await userEvent.type(within(dialog).getByLabelText(/Children per staff/), '4')
    await userEvent.clear(within(dialog).getByLabelText(/Minimum age/))
    await userEvent.type(within(dialog).getByLabelText(/Minimum age/), '30')
    await userEvent.clear(within(dialog).getByLabelText(/Maximum age/))
    await userEvent.type(within(dialog).getByLabelText(/Maximum age/), '12')
    await userEvent.click(within(dialog).getByRole('button', { name: 'Create' }))

    expect(await within(dialog).findByText('This field is required')).toBeInTheDocument()
    expect(await within(dialog).findByText('Maximum age must not be below minimum age')).toBeInTheDocument()
    expect(calls.some((c) => c.method === 'POST')).toBe(false)
  })

  it('creates a room with English and Arabic names', async () => {
    const calls = mockApi(fetchMock, {
      'POST /rooms': envelope(room, { status: 201 }),
      'GET /rooms': empty,
    })
    renderWithProviders(<RoomsPage />)
    await userEvent.click((await screen.findAllByRole('button', { name: /Add room/ }))[0])
    const dialog = await screen.findByRole('dialog')

    await userEvent.type(within(dialog).getByLabelText(/Room name \(English\)/), 'Busy Bees')
    await userEvent.type(within(dialog).getByLabelText(/Room name \(Arabic\)/), 'النحلات')
    await userEvent.type(within(dialog).getByLabelText(/Capacity/), '16')
    await userEvent.type(within(dialog).getByLabelText(/Children per staff/), '6')
    await userEvent.click(within(dialog).getByRole('button', { name: 'Create' }))

    await waitFor(() => expect(calls.some((c) => c.method === 'POST')).toBe(true))
    expect(calls.find((c) => c.method === 'POST')!.body).toMatchObject({
      name: { en: 'Busy Bees', ar: 'النحلات' },
      capacity: 16,
      ratio_staff_to_student: 6,
      min_age_months: 0,
      max_age_months: 36,
    })
  })

  it('reports a duplicate name in plain language', async () => {
    mockApi(fetchMock, { 'POST /rooms': failure(409, 'DUPLICATE_RESOURCE'), 'GET /rooms': empty })
    renderWithProviders(<RoomsPage />)
    await userEvent.click((await screen.findAllByRole('button', { name: /Add room/ }))[0])
    const dialog = await screen.findByRole('dialog')

    await userEvent.type(within(dialog).getByLabelText(/Room name \(English\)/), 'Little Stars')
    await userEvent.type(within(dialog).getByLabelText(/Capacity/), '10')
    await userEvent.type(within(dialog).getByLabelText(/Children per staff/), '4')
    await userEvent.click(within(dialog).getByRole('button', { name: 'Create' }))

    expect(await within(dialog).findByText('This already exists.')).toBeInTheDocument()
  })

  it('switches tabs and shows programs with their hours', async () => {
    mockApi(fetchMock, {
      'GET /programs': envelope(
        [{ id: 'p1', name: { en: 'Full day', ar: 'يوم كامل' }, description: null, days_per_week: 5, start_time: '07:30:00', end_time: '15:30:00', age_group: '1-4 years', is_active: true, created_at: '2026-01-01T00:00:00Z' }],
        { pagination: onePage },
      ),
      'GET /rooms': empty,
    })
    renderWithProviders(<RoomsPage />)

    await userEvent.click(await screen.findByRole('tab', { name: 'Programs' }))

    const row = (await screen.findByText('Full day')).closest('tr')!
    expect(within(row).getByText('07:30 – 15:30')).toHaveAttribute('dir', 'ltr')
    expect(screen.getByRole('button', { name: /Add program/ })).toBeInTheDocument()
  })

  it('creates a program and checks that end time is after start time', async () => {
    const calls = mockApi(fetchMock, {
      'POST /programs': envelope({ id: 'p' }, { status: 201 }),
      'GET /programs': empty,
      'GET /rooms': empty,
    })
    renderWithProviders(<RoomsPage />)
    await userEvent.click(await screen.findByRole('tab', { name: 'Programs' }))
    await userEvent.click((await screen.findAllByRole('button', { name: /Add program/ }))[0])
    const dialog = await screen.findByRole('dialog')

    await userEvent.type(within(dialog).getByLabelText(/Program name \(English\)/), 'Half day')
    await userEvent.type(within(dialog).getByLabelText(/Age group/), '2-4 years')
    await userEvent.type(within(dialog).getByLabelText(/Starts/), '12:00')
    await userEvent.type(within(dialog).getByLabelText(/Ends/), '08:00')
    await userEvent.click(within(dialog).getByRole('button', { name: 'Create' }))
    expect(await within(dialog).findByText('End time must be after start time')).toBeInTheDocument()

    await userEvent.clear(within(dialog).getByLabelText(/Ends/))
    await userEvent.type(within(dialog).getByLabelText(/Ends/), '16:00')
    await userEvent.click(within(dialog).getByRole('button', { name: 'Create' }))

    await waitFor(() => expect(calls.some((c) => c.method === 'POST')).toBe(true))
    expect(calls.find((c) => c.method === 'POST')!.body).toMatchObject({ start_time: '12:00:00', end_time: '16:00:00', days_per_week: 5 })
  })

  it('creates a label with a valid colour only', async () => {
    const calls = mockApi(fetchMock, {
      'POST /labels': envelope({ id: 'l' }, { status: 201 }),
      'GET /labels': empty,
      'GET /rooms': empty,
    })
    renderWithProviders(<RoomsPage />)
    await userEvent.click(await screen.findByRole('tab', { name: 'Labels' }))
    await userEvent.click((await screen.findAllByRole('button', { name: /Add label/ }))[0])
    const dialog = await screen.findByRole('dialog')

    await userEvent.type(within(dialog).getByLabelText(/Label name \(English\)/), 'Allergy')
    await userEvent.clear(within(dialog).getByLabelText(/Colour/))
    await userEvent.type(within(dialog).getByLabelText(/Colour/), 'red')
    await userEvent.click(within(dialog).getByRole('button', { name: 'Create' }))
    expect(await within(dialog).findByText('Use a colour such as #E07A5F')).toBeInTheDocument()

    await userEvent.clear(within(dialog).getByLabelText(/Colour/))
    await userEvent.type(within(dialog).getByLabelText(/Colour/), '#81B29A')
    await userEvent.click(within(dialog).getByRole('button', { name: 'Create' }))

    await waitFor(() => expect(calls.some((c) => c.method === 'POST')).toBe(true))
    expect(calls.find((c) => c.method === 'POST')!.body).toMatchObject({ name: { en: 'Allergy' }, color: '#81B29A' })
  })

  it('creates a group in an existing room', async () => {
    const calls = mockApi(fetchMock, {
      'POST /groups': envelope({ id: 'g' }, { status: 201 }),
      'GET /groups': empty,
      'GET /rooms': envelope([room], { pagination: onePage }),
    })
    renderWithProviders(<RoomsPage />)
    await userEvent.click(await screen.findByRole('tab', { name: 'Groups' }))
    await userEvent.click((await screen.findAllByRole('button', { name: /Add group/ }))[0])
    const dialog = await screen.findByRole('dialog')

    await userEvent.type(within(dialog).getByLabelText(/Group name \(English\)/), 'Sunflowers')
    await waitFor(() => expect(within(dialog).getByRole('option', { name: 'Little Stars' })).toBeInTheDocument())
    await userEvent.selectOptions(within(dialog).getByLabelText(/^Room/), 'r1')
    await userEvent.click(within(dialog).getByRole('button', { name: 'Create' }))

    await waitFor(() => expect(calls.some((c) => c.method === 'POST')).toBe(true))
    expect(calls.find((c) => c.method === 'POST')!.body).toMatchObject({ name: { en: 'Sunflowers' }, room_id: 'r1' })
  })
})

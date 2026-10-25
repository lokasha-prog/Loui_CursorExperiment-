import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { envelope, makeMe, mockApi, renderWithProviders, setLanguage, signIn, signOut } from '@/test/utils'
import CalendarPage from './CalendarPage'
import { buildRule, parseRule } from './EventDialog'

const fetchMock = vi.fn()
const now = new Date()
const thisMonthDay = (d: number, h = 9) => new Date(now.getFullYear(), now.getMonth(), d, h).toISOString()

const occurrence = (overrides = {}) => ({
  event_id: 'ev1', title: { en: 'Sports day', ar: 'يوم رياضي' }, description: null, starts_at: thisMonthDay(10), ends_at: thisMonthDay(10, 11), is_all_day: false,
  type: 'regular', room_id: null, visibility: 'parents', repeats: false, ...overrides,
})

beforeEach(async () => {
  fetchMock.mockReset()
  vi.stubGlobal('fetch', fetchMock)
  await setLanguage('en')
})

afterEach(() => {
  vi.unstubAllGlobals()
  signOut()
})

describe('repeat rules', () => {
  it('turns the choices into a rule and back again', () => {
    expect(buildRule('', [], '')).toBeNull()
    expect(buildRule('daily', [], '')).toBe('FREQ=DAILY')
    expect(buildRule('weekly', ['TU', 'TH'], '2026-12-31')).toBe('FREQ=WEEKLY;BYDAY=TU,TH;UNTIL=20261231')
    expect(parseRule('FREQ=WEEKLY;BYDAY=TU,TH;UNTIL=20261231')).toEqual({ freq: 'weekly', days: ['TU', 'TH'], until: '2026-12-31' })
    expect(parseRule(null)).toEqual({ freq: '', days: [], until: '' })
  })
})

describe('CalendarPage', () => {
  it('shows the month with its events for staff, with all calendar views', async () => {
    signIn(makeMe({ permissions: ['calendar.read', 'calendar.write', 'staff.read'] }))
    mockApi(fetchMock, { 'GET /calendar/events': envelope([occurrence()]), 'GET /calendar/feed-link': envelope({ path: '/api/v1/public/calendar/x/abc.ics' }), 'GET /rooms': envelope([]) })
    renderWithProviders(<CalendarPage />)

    expect(await screen.findByRole('button', { name: 'Sports day' })).toBeInTheDocument()
    for (const name of ['School calendar', 'Admin calendar', 'My calendar', 'Schedules']) expect(screen.getByRole('tab', { name })).toBeInTheDocument()
    expect(await screen.findByDisplayValue(/\/api\/v1\/public\/calendar\/x\/abc\.ics/)).toBeInTheDocument()
  })

  it('shows a parent the events only, with no way to add or rearrange', async () => {
    signIn(makeMe({ roles: ['parent'], permissions: ['calendar.read', 'students.read'] }))
    mockApi(fetchMock, { 'GET /calendar/events': envelope([occurrence()]), 'GET /calendar/feed-link': envelope({ path: '/api/v1/public/calendar/x/abc.ics' }) })
    renderWithProviders(<CalendarPage />)

    expect(await screen.findByText('Sports day')).toBeInTheDocument()
    expect(screen.queryByRole('tab')).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /Add event/ })).not.toBeInTheDocument()
  })

  it('adds a weekly event with the right repeat rule', async () => {
    signIn(makeMe({ permissions: ['calendar.read', 'calendar.write', 'staff.read'] }))
    const calls = mockApi(fetchMock, {
      'POST /calendar/events': envelope({ id: 'n1' }, { status: 201 }),
      'GET /calendar/events': envelope([]),
      'GET /calendar/feed-link': envelope({ path: '/x.ics' }),
      'GET /rooms': envelope([]),
    })
    renderWithProviders(<CalendarPage />)

    await userEvent.click(await screen.findByRole('button', { name: /Add event/ }))
    const dialog = await screen.findByRole('dialog')
    await userEvent.type(within(dialog).getByLabelText(/Title \(English\)/), 'Story time')
    await userEvent.selectOptions(within(dialog).getByLabelText('Repeat'), 'weekly')
    await userEvent.click(within(dialog).getByLabelText('Tue'))
    await userEvent.click(within(dialog).getByRole('button', { name: 'Save event' }))

    await waitFor(() => expect(calls.some((c) => c.method === 'POST')).toBe(true))
    expect(calls.find((c) => c.method === 'POST')!.body).toMatchObject({ title: { en: 'Story time' }, is_all_day: true, type: 'regular', recurrence: 'FREQ=WEEKLY;BYDAY=TU', visibility: 'parents' })
  })

  it('opens an event with its details, and a parent can only add it to their own calendar', async () => {
    signIn(makeMe({ roles: ['parent'], permissions: ['calendar.read', 'students.read'] }))
    mockApi(fetchMock, {
      'GET /calendar/events': envelope([occurrence({ description: { en: 'Bring trainers' } })]),
      'GET /calendar/feed-link': envelope({ path: '/x.ics' }),
    })
    renderWithProviders(<CalendarPage />)

    await userEvent.click(await screen.findByRole('button', { name: 'Sports day' }))
    const dialog = await screen.findByRole('dialog')
    expect(within(dialog).getByText('Bring trainers')).toBeInTheDocument()
    expect(within(dialog).getByText('Parents can see it')).toBeInTheDocument()
    expect(within(dialog).getByRole('button', { name: /Add to my calendar/ })).toBeInTheDocument()
    expect(within(dialog).queryByRole('button', { name: /Edit/ })).not.toBeInTheDocument()
    expect(within(dialog).queryByRole('button', { name: /Delete/ })).not.toBeInTheDocument()
  })

  it('lets a manager edit or remove an event from its details', async () => {
    signIn(makeMe({ permissions: ['calendar.read', 'calendar.write', 'staff.read'] }))
    const calls = mockApi(fetchMock, {
      'GET /calendar/events': envelope([occurrence()]),
      'DELETE /calendar/events/ev1': envelope(null),
      'GET /calendar/feed-link': envelope({ path: '/x.ics' }),
      'GET /rooms': envelope([]),
    })
    renderWithProviders(<CalendarPage />)

    await userEvent.click(await screen.findByRole('button', { name: 'Sports day' }))
    const dialog = await screen.findByRole('dialog')
    expect(within(dialog).getByRole('button', { name: /Edit/ })).toBeInTheDocument()
    await userEvent.click(within(dialog).getByRole('button', { name: /Delete/ }))
    await userEvent.click(within(await screen.findByRole('alertdialog')).getByRole('button', { name: 'Remove' }))

    await waitFor(() => expect(calls.some((c) => c.method === 'DELETE' && c.url.includes('/calendar/events/ev1'))).toBe(true))
  })

  it('lists every event of a busy day', async () => {
    signIn(makeMe({ roles: ['parent'], permissions: ['calendar.read'] }))
    const busy = ['A', 'B', 'C', 'D'].map((x) => occurrence({ event_id: x, title: { en: `Event ${x}` } }))
    mockApi(fetchMock, { 'GET /calendar/events': envelope(busy), 'GET /calendar/feed-link': envelope({ path: '/x.ics' }) })
    renderWithProviders(<CalendarPage />)

    await userEvent.click(await screen.findByRole('button', { name: '+1 more' }))
    const dialog = await screen.findByRole('dialog')
    await userEvent.click(within(dialog).getByRole('button', { name: /Event D/ }))
    expect(await screen.findByRole('heading', { name: 'Event D' })).toBeInTheDocument()
  })

  it('works in Arabic', async () => {
    await setLanguage('ar')
    signIn(makeMe({ permissions: ['calendar.read', 'calendar.write', 'staff.read'] }))
    mockApi(fetchMock, { 'GET /calendar/events': envelope([]), 'GET /calendar/feed-link': envelope({ path: '/x.ics' }) })
    renderWithProviders(<CalendarPage />)

    expect(await screen.findByRole('tab', { name: 'تقويم الحضانة' })).toBeInTheDocument()
    expect(document.documentElement.dir).toBe('rtl')
  })
})

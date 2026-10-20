import { screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { envelope, makeMe, mockApi, onePage, renderWithProviders, setLanguage, signIn, signOut } from '@/test/utils'
import AuditPage from '../audit/AuditPage'
import NotificationsPage from './NotificationsPage'

const fetchMock = vi.fn()

const note = (overrides = {}) => ({
  id: 'n1', tier: 'critical', type: 'ratio_breach', title: { en: 'Little Stars needs more staff', ar: 'النجوم الصغيرة تحتاج موظفين' },
  body: { en: '5 children and 1 staff are in the room.', ar: 'يوجد 5 أطفال وموظف واحد.' }, link: '/attendance', read: false, created_at: '2026-10-20T08:00:00Z', ...overrides,
})

const prefs = envelope([
  { tier: 'critical', in_app: true, email: true },
  { tier: 'operational', in_app: true, email: false },
  { tier: 'informational', in_app: false, email: true },
])

beforeEach(async () => {
  fetchMock.mockReset()
  vi.stubGlobal('fetch', fetchMock)
  await setLanguage('en')
  signIn(makeMe({ permissions: ['dashboard.view', 'notifications.preferences', 'audit_log.view'] }))
})

afterEach(() => {
  vi.unstubAllGlobals()
  signOut()
})

describe('NotificationsPage', () => {
  it('lists alerts with their urgency, marks one read and opens its page', async () => {
    const calls = mockApi(fetchMock, {
      'POST /notifications/n1/actions/read': envelope({ marked: 1 }),
      'GET /notifications/preferences': prefs,
      'GET /notifications': envelope([note()], { pagination: onePage }),
    })
    renderWithProviders(<NotificationsPage />, { route: '/notifications' })

    expect(await screen.findByText('Little Stars needs more staff')).toBeInTheDocument()
    expect(screen.getByText('Urgent', { selector: 'span' })).toBeInTheDocument()
    await userEvent.click(screen.getByRole('button', { name: 'Mark as read' }))

    await waitFor(() => expect(calls.some((c) => c.method === 'POST' && c.url.includes('/n1/actions/read'))).toBe(true))
  })

  it('saves channel choices but never lets urgent alerts leave the app', async () => {
    const calls = mockApi(fetchMock, {
      'PUT /notifications/preferences': prefs,
      'GET /notifications/preferences': prefs,
      'GET /notifications': envelope([], { pagination: onePage }),
    })
    renderWithProviders(<NotificationsPage />, { route: '/notifications' })

    const boxes = await screen.findAllByRole('checkbox')
    expect(boxes[0]).toBeDisabled()
    await userEvent.click(boxes[3])

    await waitFor(() => expect(calls.some((c) => c.method === 'PUT')).toBe(true))
    const body = calls.find((c) => c.method === 'PUT')!.body as { preferences: { tier: string; email: boolean }[] }
    expect(body.preferences.find((p) => p.tier === 'operational')!.email).toBe(true)
  })

  it('works in Arabic', async () => {
    await setLanguage('ar')
    mockApi(fetchMock, { 'GET /notifications/preferences': prefs, 'GET /notifications': envelope([note()], { pagination: onePage }) })
    renderWithProviders(<NotificationsPage />, { route: '/notifications' })

    expect(await screen.findByText('النجوم الصغيرة تحتاج موظفين')).toBeInTheDocument()
    expect(document.documentElement.dir).toBe('rtl')
  })
})

const retention = envelope({
  retention_days: 2555, auto_purge_enabled: false, min_retention_days: 365, total_entries: 120, oldest_entry_at: '2024-01-01T00:00:00Z',
  eligible_for_purge: 7, cutoff: '2019-10-20T00:00:00Z', recent_runs: [{ run_at: '2026-10-01T02:00:00Z', mode: 'auto', cutoff: '2019-10-01T00:00:00Z', rows_deleted: 4 }],
})

describe('AuditPage', () => {
  it('shows who did what and filters through the server', async () => {
    const calls = mockApi(fetchMock, {
      'GET /audit-logs/retention': retention,
      'GET /audit-logs': envelope([{
        id: 'a1', user_id: 'u1', user_email: 'admin@test.local', entity_type: 'Student', entity_id: 's1', action: 'update',
        changed_fields: { first_name: { before: 'A', after: 'B' }, medical_notes: { before: '[encrypted]', after: '[encrypted]' } },
        ip_address: null, occurred_at: '2026-10-20T08:00:00Z', created_at: '',
      }], { pagination: onePage }),
    })
    renderWithProviders(<AuditPage />)

    expect(await screen.findByText('admin@test.local')).toBeInTheDocument()
    expect(screen.getByText('Changed', { selector: 'span' })).toBeInTheDocument()
    expect(screen.getByText(/2 field\(s\)/)).toBeInTheDocument()
    await userEvent.selectOptions(screen.getByLabelText('Action'), 'delete')

    await waitFor(() => expect(calls.some((c) => c.url.includes('action=delete'))).toBe(true))
  })

  it('shows an empty state and speaks Arabic', async () => {
    await setLanguage('ar')
    mockApi(fetchMock, { 'GET /audit-logs/retention': retention, 'GET /audit-logs': envelope([], { pagination: onePage }) })
    renderWithProviders(<AuditPage />)

    expect(await screen.findByText('لا توجد نتائج مطابقة')).toBeInTheDocument()
  })

  it('shows the retention policy, saves a new period and refuses one below the legal minimum', async () => {
    const calls = mockApi(fetchMock, {
      'PUT /audit-logs/retention': retention,
      'GET /audit-logs/retention': retention,
      'GET /audit-logs': envelope([], { pagination: onePage }),
    })
    renderWithProviders(<AuditPage />)

    expect(await screen.findByText('How long history is kept')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: /Remove expired history now/ })).toBeEnabled()
    const days = screen.getByLabelText('Keep history for (days)')
    await userEvent.clear(days)
    await userEvent.type(days, '100')
    expect(await screen.findByText('Must be at least 365 days')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Save' })).toBeDisabled()
    await userEvent.clear(days)
    await userEvent.type(days, '800')
    await userEvent.click(screen.getByRole('button', { name: 'Save' }))

    await waitFor(() => expect(calls.some((c) => c.method === 'PUT')).toBe(true))
    expect(calls.find((c) => c.method === 'PUT')!.body).toEqual({ retention_days: 800, auto_purge_enabled: false })
  })

  it('asks for confirmation before removing history, and can switch the automatic clean-up on', async () => {
    const calls = mockApi(fetchMock, {
      'POST /audit-logs/retention/actions/purge': envelope({ run_at: '', mode: 'manual', cutoff: '', rows_deleted: 7 }),
      'PUT /audit-logs/retention': retention,
      'GET /audit-logs/retention': retention,
      'GET /audit-logs': envelope([], { pagination: onePage }),
    })
    renderWithProviders(<AuditPage />)

    await userEvent.click(await screen.findByRole('checkbox'))
    await waitFor(() => expect(calls.some((c) => c.method === 'PUT')).toBe(true))
    expect(calls.find((c) => c.method === 'PUT')!.body).toEqual({ retention_days: 2555, auto_purge_enabled: true })

    await userEvent.click(screen.getByRole('button', { name: /Remove expired history now/ }))
    expect(await screen.findByText(/7 entries older than the limit/)).toBeInTheDocument()
    expect(calls.some((c) => c.url.includes('/actions/purge'))).toBe(false)
    await userEvent.click(screen.getByRole('button', { name: 'Remove permanently' }))

    await waitFor(() => expect(calls.some((c) => c.method === 'POST' && c.url.includes('/actions/purge'))).toBe(true))
    expect(calls.find((c) => c.method === 'POST')!.body).toEqual({ confirm: true })
  })
})

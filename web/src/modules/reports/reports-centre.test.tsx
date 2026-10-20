import { screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { envelope, makeMe, mockApi, onePage, renderWithProviders, setLanguage, signIn, signOut } from '@/test/utils'
import ReportsPage from '../attendance/ReportsPage'

const fetchMock = vi.fn()

const definitions = [
  { kind: 'attendance_students', name: { en: 'Monthly attendance (children)', ar: 'الحضور الشهري (الأطفال)' }, formats: ['excel', 'pdf'], filters: ['month', 'room_id'], permission: 'reports.generate' },
  { kind: 'audit_log', name: { en: 'Audit log export', ar: 'تصدير سجل التدقيق' }, formats: ['excel', 'json'], filters: ['from', 'to'], permission: 'audit_log.view' },
]

const run = (overrides = {}) => ({
  id: 'r1', kind: 'attendance_students', format: 'excel', status: 'done', error: null, file_name: 'attendance.xlsx', parameters: {}, created_at: '2026-10-20T08:00:00Z', completed_at: '2026-10-20T08:00:05Z', expires_at: '2026-11-19T08:00:00Z', ...overrides,
})

const analytics = envelope({
  from: '2026-08-01', to: '2026-10-20', attendance_rate: [{ label: '2026-08', value: 90 }, { label: '2026-09', value: 94 }], attendance_by_status: [{ label: 'present', count: 40 }, { label: 'absent', count: 4 }],
  revenue: [{ month: '2026-09', billed_fils: 500_000, collected_fils: 300_000, expenses_fils: 100_000 }], invoice_status: [{ label: 'paid', count: 3 }],
  enquiries_by_month: [{ label: '2026-09', count: 5 }], enrolled_by_month: [{ label: '2026-09', count: 2 }], enquiries_by_source: [{ label: 'website', count: 5 }],
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

describe('Report centre', () => {
  it('asks for the chosen report with its filters and lists finished reports with a download', async () => {
    signIn(makeMe({ permissions: ['reports.view', 'reports.generate', 'centre_structure.read', 'staff.read'] }))
    const calls = mockApi(fetchMock, {
      'POST /reports/center/runs': envelope(run({ status: 'pending' }), { status: 202 }),
      'GET /reports/center/definitions': envelope(definitions),
      'GET /reports/center/runs': envelope([run()], { pagination: onePage }),
      'GET /rooms': envelope([], { pagination: onePage }),
      'GET /programs': envelope([], { pagination: onePage }),
      'GET /staff': envelope([], { pagination: onePage }),
    })
    renderWithProviders(<ReportsPage />)

    expect(await screen.findByRole('button', { name: 'Download' })).toBeInTheDocument()
    await userEvent.selectOptions(await screen.findByLabelText('File type'), 'pdf')
    await userEvent.click(screen.getByRole('button', { name: 'Make report' }))

    await waitFor(() => expect(calls.some((c) => c.method === 'POST')).toBe(true))
    expect(calls.find((c) => c.method === 'POST')!.body).toMatchObject({ kind: 'attendance_students', format: 'pdf', parameters: { month: expect.stringMatching(/^\d{4}-\d{2}$/) }, language: 'en' })
  })

  it('shows analytics charts with money only when the server sent it', async () => {
    signIn(makeMe({ permissions: ['reports.view'] }))
    mockApi(fetchMock, {
      'GET /reports/analytics': analytics,
      'GET /reports/center/definitions': envelope([]),
      'GET /reports/center/runs': envelope([], { pagination: onePage }),
    })
    renderWithProviders(<ReportsPage />)

    await userEvent.click(await screen.findByRole('tab', { name: 'Analytics' }))
    expect(await screen.findByRole('img', { name: 'Attendance rate' })).toBeInTheDocument()
    expect(screen.getByRole('img', { name: 'Billed, collected and spent' })).toBeInTheDocument()
    expect(screen.getByRole('img', { name: 'Where enquiries come from' })).toBeInTheDocument()
  })

  it('leaves out the money charts for someone who may not see them', async () => {
    signIn(makeMe({ permissions: ['reports.view'] }))
    mockApi(fetchMock, {
      'GET /reports/analytics': envelope({ from: '2026-08-01', to: '2026-10-20', attendance_rate: [{ label: '2026-09', value: 90 }], attendance_by_status: [], revenue: null, invoice_status: null, enquiries_by_month: null, enrolled_by_month: null, enquiries_by_source: null }),
      'GET /reports/center/definitions': envelope([]),
      'GET /reports/center/runs': envelope([], { pagination: onePage }),
    })
    renderWithProviders(<ReportsPage />)

    await userEvent.click(await screen.findByRole('tab', { name: 'Analytics' }))
    expect(await screen.findByRole('img', { name: 'Attendance rate' })).toBeInTheDocument()
    expect(screen.queryByRole('img', { name: 'Billed, collected and spent' })).not.toBeInTheDocument()
  })

  it('works in Arabic', async () => {
    await setLanguage('ar')
    signIn(makeMe({ permissions: ['reports.view', 'reports.generate'] }))
    mockApi(fetchMock, { 'GET /reports/center/definitions': envelope(definitions), 'GET /reports/center/runs': envelope([], { pagination: onePage }) })
    renderWithProviders(<ReportsPage />)

    expect(await screen.findByRole('tab', { name: 'مركز التقارير' })).toBeInTheDocument()
    expect(document.documentElement.dir).toBe('rtl')
  })
})

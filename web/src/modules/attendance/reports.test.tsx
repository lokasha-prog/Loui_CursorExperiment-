import { screen, within } from '@testing-library/react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { envelope, makeMe, mockApi, onePage, renderWithProviders, setLanguage, signIn, signOut } from '@/test/utils'
import ReportsPage from './ReportsPage'

const fetchMock = vi.fn()

const routes = {
  'GET /attendance/report/monthly': envelope({
    month: '2026-10', person_type: 'student',
    rows: [{ person_id: 's1', name: 'Layla Hassan', present: 12, late: 2, early_leave: 1, absent: 3, days_recorded: 18 }],
  }),
  'GET /attendance/forecast': envelope({
    date: '2026-10-21', total_expected: 9,
    by_program: [{ program_id: 'p1', program_name: { en: 'Full day', ar: 'يوم كامل' }, expected: 9, on_leave: 1, enrolled: 10 }],
  }),
  'GET /attendance/ratio/history': envelope([
    { id: 'b1', room_id: 'r1', room_name: null, detected_at: '2026-10-20T05:00:00Z', resolved_at: null, students_present: 5, staff_present: 1, staff_required: 2, created_at: '' },
  ], { pagination: onePage }),
  'GET /rooms': envelope([{ id: 'r1', name: { en: 'Little Stars', ar: 'النجوم الصغيرة' } }], { pagination: onePage }),
}

beforeEach(async () => {
  fetchMock.mockReset()
  vi.stubGlobal('fetch', fetchMock)
  await setLanguage('en')
  signIn(makeMe({ permissions: ['attendance.override', 'attendance.write', 'centre_structure.read'] }))
})

afterEach(() => {
  vi.unstubAllGlobals()
  signOut()
})

describe('ReportsPage', () => {
  it('shows the month per child, the forecast by programme and the shortage history', async () => {
    mockApi(fetchMock, routes)
    renderWithProviders(<ReportsPage />)

    const row = (await screen.findByText('Layla Hassan')).closest('tr')!
    expect(within(row).getByText('12')).toBeInTheDocument()
    expect(await screen.findByText('9 children expected on 21 Oct 2026')).toBeInTheDocument()
    expect(screen.getByText('9 expected, 1 on leave')).toBeInTheDocument()
    expect(await screen.findByText('Still short')).toBeInTheDocument()
    expect(screen.getByText('1 here, 2 needed')).toBeInTheDocument()
    expect(screen.getByRole('link', { name: /Download for Excel/ })).toHaveAttribute('href', expect.stringContaining('/attendance/export?month='))
  })

  it('works in Arabic', async () => {
    await setLanguage('ar')
    mockApi(fetchMock, routes)
    renderWithProviders(<ReportsPage />)

    expect(await screen.findByText('الحضور الشهري')).toBeInTheDocument()
    expect(await screen.findByText('لا يزال ناقصاً')).toBeInTheDocument()
    expect(document.documentElement.dir).toBe('rtl')
  })
})

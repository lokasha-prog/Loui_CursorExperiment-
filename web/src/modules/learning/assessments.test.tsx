import { screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { SystemOverviewSection } from '@/modules/dashboard/SystemOverviewSection'
import { envelope, makeMe, mockApi, onePage, renderWithProviders, setLanguage, signIn, signOut } from '@/test/utils'
import { JourneyView } from './JourneyView'
import { ObservationsTab } from './ObservationsTab'

const fetchMock = vi.fn()

beforeEach(async () => {
  fetchMock.mockReset()
  vi.stubGlobal('fetch', fetchMock)
  await setLanguage('en')
})

afterEach(() => {
  vi.unstubAllGlobals()
  signOut()
})

const overview = {
  date: '2026-10-21', people: { students: 12, staff: 5, rooms: 4, present_today: 9, rooms_short: 1 },
  billing: { billed_fils: 1250000, collected_fils: 800000, overdue_fils: 150000, overdue_invoices: 2, expenses_fils: 300000 },
  admissions: { open_enquiries: 7, new_last30_days: 3, tours_next7_days: 2, stale_over7_days: 1, admission_steps_overdue: 0 },
  learning: { curricula: 1, lessons: 11, observations_last30_days: 14, concerns_last30_days: 1, assessments_to_approve: 2, assessments_in_progress: 1, reports_shared: 3, children_without_observation30_days: 5 },
  family: null,
  communication: { posts_to_approve: 0, newsletters_last30_days: 2, live_forms: 3, documents: 8 },
}

describe('SystemOverviewSection', () => {
  it('shows every part of the system for a manager, including learning', async () => {
    signIn(makeMe({ permissions: ['dashboard.view'] }))
    mockApi(fetchMock, { 'GET /dashboard/system': envelope(overview) })
    renderWithProviders(<SystemOverviewSection />)

    expect(await screen.findByRole('heading', { name: 'The whole centre at a glance' })).toBeInTheDocument()
    for (const title of ['People and rooms', 'Money this month', 'Enrolment', 'Learning', 'Communication and library']) {
      expect(screen.getByRole('heading', { name: title })).toBeInTheDocument()
    }
    expect(screen.getByText('Reports to approve')).toBeInTheDocument()
    expect(screen.getByRole('link', { name: /Learning/ })).toHaveAttribute('href', '/learning')
  })

  it('shows a family only its own learning block, in Arabic when asked', async () => {
    await setLanguage('ar')
    signIn(makeMe({ permissions: ['dashboard.view', 'journey.read'] }))
    mockApi(fetchMock, { 'GET /dashboard/system': envelope({ ...overview, people: null, billing: null, admissions: null, learning: null, communication: null, family: { children: 2, shared_observations: 4, reports_ready: 1 } }) })
    renderWithProviders(<SystemOverviewSection />)

    expect(await screen.findByRole('heading', { name: 'تعلّم أطفالي' })).toBeInTheDocument()
    expect(screen.queryByRole('heading', { name: 'المال هذا الشهر' })).not.toBeInTheDocument()
  })
})

describe('JourneyView', () => {
  it('shows approved reports in plain levels and the timeline of shared moments', async () => {
    signIn(makeMe({ permissions: ['journey.read'] }))
    mockApi(fetchMock, {
      'GET /learning/journey/s1': envelope({
        student_id: 's1', student_name: 'Layla',
        items: [{ kind: 'observation', id: 'o1', at: '2026-10-20T08:00:00Z', type: null, area_id: null, area_name: { en: 'Literacy' }, title: null, text: { en: 'Wrote her first letter' }, media_ids: [], percent: null }],
      }),
      'GET /learning/parent-reports/s1': envelope([{ id: 'r1', template_name: { en: 'Toddler check' }, student_name: 'Layla', term: 'Autumn 2026', assessed_on: '2026-10-10', approved_at: '2026-10-12T08:00:00Z', summary: { en: 'Growing in confidence.' }, areas: [{ name: { en: 'Communication' }, level: 'secure', percent: 80 }] }]),
    })
    renderWithProviders(<JourneyView studentId="s1" />)

    expect(await screen.findByText('Wrote her first letter')).toBeInTheDocument()
    expect(await screen.findByText('Growing in confidence.')).toBeInTheDocument()
    expect(screen.getByText('Going strong')).toBeInTheDocument()
  })
})

describe('ObservationsTab', () => {
  it('lists observations with their type and lets only writers add one', async () => {
    signIn(makeMe({ permissions: ['learning.read'] }))
    mockApi(fetchMock, {
      'GET /learning/observations': envelope([{ id: 'o1', student_id: 's1', student_name: 'Layla Al Suwaidi', observer_name: 'Test Teacher', observed_at: '2026-10-20T08:00:00Z', type: 'concern', framework_id: null, area_id: null, age_band_id: null, milestone_ids: [], notes: { en: 'Needs a closer look' }, media_ids: [], share_with_parents: false, created_at: '' }], { pagination: onePage }),
      'GET /students': envelope([], { pagination: onePage }),
    })
    renderWithProviders(<ObservationsTab canWrite={false} />)

    expect(await screen.findByText('Needs a closer look')).toBeInTheDocument()
    expect(screen.getAllByText('Concern').length).toBeGreaterThan(0)
    expect(screen.queryByRole('button', { name: 'New observation' })).not.toBeInTheDocument()
    await userEvent.click(screen.getByRole('combobox', { name: 'Type' }))
  })
})

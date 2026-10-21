import { screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { envelope, makeMe, mockApi, onePage, renderWithProviders, setLanguage, signIn, signOut } from '@/test/utils'
import LearningPage from './LearningPage'

const fetchMock = vi.fn()

const framework = { id: 'f1', code: 'eyfs', name: { en: 'EYFS', ar: 'المرحلة التأسيسية' }, description: null, areas: 1, age_bands: 1, milestones: 1, lessons: 1 }
const detail = {
  id: 'f1', code: 'eyfs', name: framework.name, description: null,
  areas: [{ id: 'a1', code: 'cal', name: { en: 'Communication and Language', ar: 'التواصل واللغة' }, sort_order: 1 }],
  age_bands: [{ id: 'b1', code: 'b8-20', name: { en: '8-20 months', ar: '8-20 شهراً' }, min_months: 8, max_months: 20 }],
  milestones: [{ id: 'm1', area_id: 'a1', age_band_id: 'b1', code: 'cal-1', statement: { en: 'Responds to their name', ar: 'يستجيب لاسمه' }, sort_order: 1 }],
}
const lesson = {
  id: 'l1', title: { en: 'Rainbow painting', ar: 'رسم قوس قزح' }, description: null, framework_id: 'f1', area_id: 'a1', age_band_id: 'b1', room_id: null,
  duration_minutes: 40, materials: ['Paint'], objectives: ['Name colours'], links: {}, milestone_ids: [], created_at: '',
}

beforeEach(async () => {
  fetchMock.mockReset()
  vi.stubGlobal('fetch', fetchMock)
  await setLanguage('en')
})

afterEach(() => {
  vi.unstubAllGlobals()
  signOut()
})

describe('LearningPage', () => {
  it('lists lessons with their objectives and lets a teacher add one', async () => {
    signIn(makeMe({ permissions: ['learning.read', 'learning.write'] }))
    mockApi(fetchMock, {
      'GET /learning/lessons': envelope([lesson], { pagination: onePage }),
      'GET /learning/frameworks': envelope([framework]),
      'GET /rooms': envelope([], { pagination: onePage }),
    })
    renderWithProviders(<LearningPage />)

    expect(await screen.findByText('Rainbow painting')).toBeInTheDocument()
    expect(screen.getByText('Name colours')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'New lesson' })).toBeInTheDocument()
  })

  it('hides editing from people who may only read, and shows the curriculum tree', async () => {
    signIn(makeMe({ permissions: ['learning.read'] }))
    mockApi(fetchMock, {
      'GET /learning/lessons': envelope([lesson], { pagination: onePage }),
      'GET /learning/frameworks/f1': envelope(detail),
      'GET /learning/frameworks': envelope([framework]),
    })
    renderWithProviders(<LearningPage />)

    expect(await screen.findByText('Rainbow painting')).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'New lesson' })).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Edit' })).not.toBeInTheDocument()

    await userEvent.click(screen.getByRole('tab', { name: 'Curriculum' }))
    expect(await screen.findByText('Responds to their name')).toBeInTheDocument()
    expect(screen.getByText('Communication and Language')).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'New curriculum' })).not.toBeInTheDocument()
  })

  it('shows Arabic text and tells a manager how to start when there is no curriculum', async () => {
    await setLanguage('ar')
    signIn(makeMe({ permissions: ['learning.read', 'settings.write'] }))
    mockApi(fetchMock, { 'GET /learning/lessons': envelope([], { pagination: onePage }), 'GET /learning/frameworks': envelope([]) })
    renderWithProviders(<LearningPage />)

    expect(await screen.findByText('لا توجد دروس بعد')).toBeInTheDocument()
    await userEvent.click(screen.getByRole('tab', { name: 'المنهج' }))
    expect(await screen.findByText('لا يوجد منهج بعد')).toBeInTheDocument()
  })
})

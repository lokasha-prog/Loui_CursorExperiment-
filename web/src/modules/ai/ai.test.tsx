import { screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { envelope, failure, makeMe, mockApi, renderWithProviders, setLanguage, signIn, signOut } from '@/test/utils'
import { AiInsightsCard } from './AiInsightsCard'

const fetchMock = vi.fn()

beforeEach(async () => {
  fetchMock.mockReset()
  vi.stubGlobal('fetch', fetchMock)
  await setLanguage('en')
  signIn(makeMe({ permissions: ['attendance.override'] }))
})

afterEach(() => {
  vi.unstubAllGlobals()
  signOut()
})

describe('AiInsightsCard', () => {
  it('asks for a summary in the active language and shows it with its source and the privacy note', async () => {
    const calls = mockApi(fetchMock, {
      'GET /ai/attendance-summary': envelope({
        summary: 'Attendance was steady.', days: 14, language: 'en', model: 'openai/gpt-oss-120b',
        generated_at: '2026-10-20T10:00:00Z', shortage_events: 2, approved_leave_requests: 1,
      }),
    })
    renderWithProviders(<AiInsightsCard />)

    expect(screen.getByText(/Only daily totals are shared/)).toBeInTheDocument()
    await userEvent.click(screen.getByRole('button', { name: 'Summarise attendance' }))

    expect(await screen.findByText('Attendance was steady.')).toBeInTheDocument()
    expect(calls[0].url).toContain('days=14')
    expect(calls[0].url).toContain('language=en')
    expect(screen.getByText(/2 staff shortages and 1 approved leave/)).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Refresh summary' })).toBeInTheDocument()
  })

  it('requests Arabic and renders the answer right-to-left', async () => {
    await setLanguage('ar')
    const calls = mockApi(fetchMock, {
      'GET /ai/attendance-summary': envelope({
        summary: 'كان الحضور مستقراً.', days: 14, language: 'ar', model: 'm',
        generated_at: '2026-10-20T10:00:00Z', shortage_events: 0, approved_leave_requests: 0,
      }),
    })
    renderWithProviders(<AiInsightsCard />)

    await userEvent.click(screen.getByRole('button', { name: 'لخّص الحضور' }))

    const text = await screen.findByText('كان الحضور مستقراً.')
    expect(text).toHaveAttribute('dir', 'rtl')
    expect(calls[0].url).toContain('language=ar')
  })

  it('explains plainly when the AI Brain is unavailable', async () => {
    mockApi(fetchMock, { 'GET /ai/attendance-summary': failure(422, 'INVALID_STATE_TRANSITION') })
    renderWithProviders(<AiInsightsCard />)

    await userEvent.click(screen.getByRole('button', { name: 'Summarise attendance' }))

    expect(await screen.findByRole('alert')).toHaveTextContent('The AI Brain is not available right now')
  })

  it('says the AI Brain is not set up, rather than asking to try again, when the server has no AI key', async () => {
    mockApi(fetchMock, { 'GET /ai/attendance-summary': failure(422, 'AI_NOT_SET_UP') })
    renderWithProviders(<AiInsightsCard />)

    await userEvent.click(screen.getByRole('button', { name: 'Summarise attendance' }))

    const alert = await screen.findByRole('alert')
    expect(alert).toHaveTextContent("The AI Brain isn't set up yet")
    expect(alert).not.toHaveTextContent('try again')
  })
})

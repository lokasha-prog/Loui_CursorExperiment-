import { screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { envelope, failure, makeMe, mockApi, renderWithProviders, setLanguage, signIn, signOut } from '@/test/utils'
import { AiBriefingCard } from './AiBriefingCard'

const fetchMock = vi.fn()

beforeEach(async () => {
  fetchMock.mockReset()
  vi.stubGlobal('fetch', fetchMock)
  await setLanguage('en')
  signIn(makeMe({ permissions: ['billing.write'] }))
})

afterEach(() => {
  vi.unstubAllGlobals()
  signOut()
})

describe('AiBriefingCard', () => {
  it('asks for the chosen area in the active language and lists the figures the advice came from', async () => {
    const calls = mockApi(fetchMock, {
      'GET /ai/briefing/finance': envelope({
        area: 'finance', summary: 'Collections are slipping.', language: 'en', model: 'm', generated_at: '2026-10-21T08:00:00Z',
        facts: [{ label: 'Collection rate this month', value: '62%' }],
      }),
    })
    renderWithProviders(<AiBriefingCard area="finance" />)

    expect(screen.getByRole('heading', { name: 'Money briefing' })).toBeInTheDocument()
    await userEvent.click(screen.getByRole('button', { name: 'Write briefing' }))

    expect(await screen.findByText('Collections are slipping.')).toBeInTheDocument()
    expect(calls[0].url).toContain('/ai/briefing/finance')
    expect(calls[0].url).toContain('language=en')
    expect(screen.getByText('Collection rate this month')).toBeInTheDocument()
    expect(screen.getByText('62%')).toBeInTheDocument()
  })

  it('shows a plain message when the AI Brain is not available', async () => {
    mockApi(fetchMock, { 'GET /ai/briefing/people': failure(422, 'INVALID_STATE') })
    renderWithProviders(<AiBriefingCard area="people" />)

    await userEvent.click(screen.getByRole('button', { name: 'Write briefing' }))

    expect(await screen.findByText(/AI Brain is not available/)).toBeInTheDocument()
  })

  it('is available in Arabic', async () => {
    await setLanguage('ar')
    renderWithProviders(<AiBriefingCard area="admissions" />)

    expect(screen.getByRole('heading', { name: 'موجز التسجيل' })).toBeInTheDocument()
  })
})

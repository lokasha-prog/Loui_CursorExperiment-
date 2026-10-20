import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { envelope, makeMe, mockApi, onePage, renderWithProviders, setLanguage, signIn, signOut } from '@/test/utils'
import FeedbackPage from './FeedbackPage'
import { FeedbackButton } from './FeedbackButton'

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

describe('FeedbackButton', () => {
  it('sends the note with the screen the person was on', async () => {
    signIn(makeMe())
    const calls = mockApi(fetchMock, { 'POST /feedback': envelope({ id: 'f1' }, { status: 201 }) })
    renderWithProviders(<FeedbackButton />, { route: '/attendance' })

    await userEvent.click(screen.getByRole('button', { name: 'Feedback' }))
    const dialog = await screen.findByRole('dialog')
    await userEvent.click(within(dialog).getByRole('button', { name: 'Send' }))
    expect(await within(dialog).findByRole('alert')).toBeInTheDocument()
    expect(calls.some((c) => c.method === 'POST')).toBe(false)

    await userEvent.type(within(dialog).getByLabelText(/Your message/), 'The scan box lost focus')
    await userEvent.selectOptions(within(dialog).getByLabelText(/This is a/), 'idea')
    await userEvent.click(within(dialog).getByRole('button', { name: 'Send' }))

    await waitFor(() => expect(calls.some((c) => c.method === 'POST')).toBe(true))
    expect(calls.find((c) => c.method === 'POST')!.body).toEqual({ kind: 'idea', message: 'The scan box lost focus', page: '/attendance', rating: null })
  })

  it('is available in Arabic', async () => {
    await setLanguage('ar')
    signIn(makeMe())
    mockApi(fetchMock, {})
    renderWithProviders(<FeedbackButton />)

    expect(screen.getByRole('button', { name: 'ملاحظات' })).toBeInTheDocument()
  })
})

describe('FeedbackPage', () => {
  it('lists notes and lets a manager change their status', async () => {
    signIn(makeMe({ permissions: ['settings.read', 'settings.write'] }))
    const calls = mockApi(fetchMock, {
      'PATCH /feedback/f1': envelope({ id: 'f1' }),
      'GET /feedback': envelope([{ id: 'f1', user_id: 'u', user_name: 'Noor Teacher', kind: 'problem', message: 'Photo did not load', page: '/feed', rating: 2, status: 'new', created_at: '2026-10-20T08:00:00Z' }], { pagination: onePage }),
    })
    renderWithProviders(<FeedbackPage />)

    expect(await screen.findByText('Photo did not load')).toBeInTheDocument()
    expect(screen.getByText('/feed')).toBeInTheDocument()
    await userEvent.selectOptions(screen.getByLabelText('Status', { selector: 'select' }), 'done')

    await waitFor(() => expect(calls.some((c) => c.method === 'PATCH')).toBe(true))
    expect(calls.find((c) => c.method === 'PATCH')!.body).toEqual({ status: 'done' })
  })
})

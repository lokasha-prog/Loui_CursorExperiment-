import { screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import axe from 'axe-core'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { envelope, failure, makeMe, mockApi, renderWithProviders, setLanguage, signIn, signOut } from '@/test/utils'
import { DataChatWidget } from './DataChatWidget'

const fetchMock = vi.fn()

const reply = (answer: string) =>
  envelope({
    answer,
    language: 'en',
    model: 'fake-model',
    generated_at: '2026-10-24T08:00:00Z',
    lookups: [{ tool: 'count_children', arguments: '{"room":"Little Stars","gender":"female","attendance":"present"}', result: '{"count":2}' }],
  })

const openChat = () => userEvent.click(screen.getByRole('button', { name: 'Chat with data' }))

beforeEach(async () => {
  fetchMock.mockReset()
  vi.stubGlobal('fetch', fetchMock)
  await setLanguage('en')
  signIn(makeMe({ roles: ['super_admin'], permissions: ['ai.chat'] }))
})

afterEach(() => {
  vi.unstubAllGlobals()
  signOut()
})

describe('DataChatWidget', () => {
  it('is not shown without permission to chat with data', () => {
    signIn(makeMe({ roles: ['centre_admin'], permissions: ['students.read'] }))
    renderWithProviders(<DataChatWidget />)
    expect(screen.queryByRole('button', { name: 'Chat with data' })).not.toBeInTheDocument()
  })

  it('answers a question, shows how it worked it out and sends the conversation with the follow-up', async () => {
    const calls = mockApi(fetchMock, { 'POST /ai/chat': reply('2 girls are present in Little Stars today.') })
    renderWithProviders(<main><DataChatWidget /></main>)

    await openChat()
    const panel = screen.getByRole('dialog', { name: 'Chat with data' })
    expect(screen.getByLabelText('Your question')).toHaveFocus()
    await userEvent.type(screen.getByLabelText('Your question'), 'How many girls are present today in Little Stars?{Enter}')
    const log = within(panel).getByRole('log', { name: 'Conversation' })
    expect(await within(log).findByText('2 girls are present in Little Stars today.')).toBeInTheDocument()
    await userEvent.click(within(log).getByText('How I worked it out (1 look-ups)'))
    expect(within(log).getByText(/count_children/)).toBeVisible()
    expect(calls[0].body).toMatchObject({ question: 'How many girls are present today in Little Stars?', history: [], language: 'en' })

    await userEvent.type(screen.getByLabelText('Your question'), 'And boys?{Enter}')
    await within(log).findByText('And boys?')
    expect(calls[1].body).toMatchObject({
      question: 'And boys?',
      history: [
        { role: 'user', content: 'How many girls are present today in Little Stars?' },
        { role: 'assistant', content: '2 girls are present in Little Stars today.' },
      ],
    })

    const results = await axe.run(document.body, { rules: { 'color-contrast': { enabled: false } } })
    expect(results.violations.map((v) => v.id)).toEqual([])
  })

  it('closes with Escape, gives focus back to its button and keeps the conversation when opened again', async () => {
    mockApi(fetchMock, { 'POST /ai/chat': reply('3 teachers are on leave today.') })
    renderWithProviders(<DataChatWidget />)

    await openChat()
    await userEvent.click(screen.getByRole('button', { name: 'How many teachers are on leave today?' }))
    expect(await screen.findByText('3 teachers are on leave today.')).toBeInTheDocument()

    await userEvent.keyboard('{Escape}')
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
    const launcher = screen.getByRole('button', { name: 'Chat with data' })
    expect(launcher).toHaveFocus()
    expect(launcher).toHaveAttribute('aria-expanded', 'false')

    await openChat()
    expect(screen.getByText('3 teachers are on leave today.')).toBeInTheDocument()
    await userEvent.click(screen.getByRole('button', { name: 'Clear conversation' }))
    expect(screen.queryByText('3 teachers are on leave today.')).not.toBeInTheDocument()
    expect(screen.getByText('What would you like to know?')).toBeInTheDocument()
  })

  it('says plainly when the AI Brain is not set up, and works in Arabic', async () => {
    mockApi(fetchMock, { 'POST /ai/chat': failure(422, 'AI_NOT_SET_UP') })
    await setLanguage('ar')
    renderWithProviders(<DataChatWidget />)

    await userEvent.click(screen.getByRole('button', { name: 'محادثة مع البيانات' }))
    expect(screen.getByRole('dialog', { name: 'محادثة مع البيانات' })).toBeInTheDocument()
    await userEvent.type(screen.getByLabelText('سؤالك'), 'كم عدد الأطفال؟{Enter}')
    expect(await screen.findByRole('alert')).toHaveTextContent('لم يتم إعداد مساعد الذكاء الاصطناعي بعد')
  })
})

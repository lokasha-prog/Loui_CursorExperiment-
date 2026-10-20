import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { envelope, makeMe, mockApi, onePage, renderWithProviders, setLanguage, signIn, signOut } from '@/test/utils'
import MessagesPage from '../messages/MessagesPage'
import FeedPage from './FeedPage'

const fetchMock = vi.fn()

const post = (overrides = {}) => ({
  id: 'p1', type: 'diary', caption: { en: 'Built a tower', ar: 'بنى برجاً' }, posted_by_user_id: 'u1', posted_by_name: 'Noor Teacher',
  room_id: null, student_ids: ['s1'], media: [], status: 'approved', rejection_reason: null, created_at: '2026-10-20T08:00:00Z', ...overrides,
})

const base = {
  'GET /students': envelope([{ id: 's1', first_name: 'Layla', last_name: 'Hassan' }], { pagination: onePage }),
  'GET /daily-reports': envelope([{ id: 'd1', student_id: 's1', student_name: 'Layla', date: '2026-10-20', activity_count: 1, check_in_at: null, check_out_at: null, summary: { en: 'Layla was with us today (07:45 - 14:30).', ar: 'كانت ليلى معنا اليوم.' }, created_at: '' }], { pagination: onePage }),
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

describe('FeedPage', () => {
  it('shows posts with the tagged child, the poster and the daily report', async () => {
    signIn(makeMe({ roles: ['parent'], permissions: ['activities.read', 'students.read'] }))
    mockApi(fetchMock, { ...base, 'GET /activities': envelope([post()], { pagination: onePage }) })
    renderWithProviders(<FeedPage />)

    expect(await screen.findByText('Built a tower')).toBeInTheDocument()
    expect(await screen.findByText('Layla Hassan')).toBeInTheDocument()
    expect(screen.getByText('By Noor Teacher')).toBeInTheDocument()
    expect(await screen.findByText('Layla was with us today (07:45 - 14:30).')).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'New post' })).not.toBeInTheDocument()
  })

  it('lets a manager approve a waiting post', async () => {
    signIn(makeMe({ permissions: ['activities.read', 'activities.write', 'activities.approve', 'students.read'] }))
    const calls = mockApi(fetchMock, {
      ...base,
      'POST /activities/p1/actions/approve': envelope(post()),
      'GET /activities': envelope([post({ status: 'pending', type: 'announcement' })], { pagination: onePage }),
    })
    renderWithProviders(<FeedPage />)

    expect(await screen.findByText('Waiting')).toBeInTheDocument()
    await userEvent.click(screen.getByRole('button', { name: /Approve/ }))

    await waitFor(() => expect(calls.some((c) => c.method === 'POST' && c.url.includes('/actions/approve'))).toBe(true))
  })

  it('warns a teacher that announcements need approval and sends text posts', async () => {
    signIn(makeMe({ permissions: ['activities.read', 'activities.write', 'students.read'] }))
    const calls = mockApi(fetchMock, {
      ...base,
      'POST /activities': envelope(post(), { status: 201 }),
      'GET /activities': envelope([], { pagination: onePage }),
    })
    renderWithProviders(<FeedPage />)

    await userEvent.click(await screen.findByRole('button', { name: 'New post' }))
    const dialog = await screen.findByRole('dialog')
    await userEvent.selectOptions(within(dialog).getByLabelText('Kind of post'), 'announcement')
    expect(within(dialog).getByText(/checked by a manager/)).toBeInTheDocument()
    await userEvent.selectOptions(within(dialog).getByLabelText('Kind of post'), 'diary')
    await userEvent.type(within(dialog).getByLabelText('Text (English)'), 'Painting day')
    await userEvent.click(within(dialog).getByRole('button', { name: 'Share' }))

    await waitFor(() => expect(calls.some((c) => c.method === 'POST' && c.url.endsWith('/activities'))).toBe(true))
    expect(calls.find((c) => c.method === 'POST')!.body).toMatchObject({ type: 'diary', caption: { en: 'Painting day' }, media_ids: [] })
  })

  it('shows the Arabic text and labels', async () => {
    await setLanguage('ar')
    signIn(makeMe({ roles: ['parent'], permissions: ['activities.read', 'students.read'] }))
    mockApi(fetchMock, { ...base, 'GET /activities': envelope([post()], { pagination: onePage }) })
    renderWithProviders(<FeedPage />)

    expect(await screen.findByText('بنى برجاً')).toBeInTheDocument()
    expect(screen.getByText('يوميات')).toBeInTheDocument()
    expect(document.documentElement.dir).toBe('rtl')
  })
})

describe('MessagesPage', () => {
  const thread = { id: 't1', subject: 'Pick-up change', student_id: 's1', is_broadcast: false, last_message_preview: 'Noted, thank you', last_sender_name: 'Reception', last_message_at: '2026-10-20T09:00:00Z', unread: true, created_at: '' }

  it('lists conversations with an unread badge and opens one to read and reply', async () => {
    signIn(makeMe({ roles: ['parent'], permissions: ['messaging.read', 'messaging.write', 'students.read'] }))
    const calls = mockApi(fetchMock, {
      'POST /messages/threads/t1/messages': envelope({ id: 'm3', sender_user_id: 'me', sender_name: 'Me', from_staff: false, body: 'Thanks', created_at: '' }, { status: 201 }),
      'GET /messages/threads/t1': envelope({
        id: 't1', subject: 'Pick-up change', student_id: 's1', is_broadcast: false, can_reply: true,
        messages: [
          { id: 'm1', sender_user_id: 'me', sender_name: 'Me', from_staff: false, body: 'Grandma collects today', created_at: '2026-10-20T08:00:00Z' },
          { id: 'm2', sender_user_id: 'x', sender_name: 'Reception', from_staff: true, body: 'Noted, thank you', created_at: '2026-10-20T09:00:00Z' },
        ],
      }),
      'GET /messages/threads': envelope([thread], { pagination: onePage }),
    })
    renderWithProviders(<MessagesPage />)

    await userEvent.click(await screen.findByRole('button', { name: /Pick-up change/ }))
    expect(await screen.findByText('Grandma collects today')).toBeInTheDocument()
    expect(screen.getAllByText('Staff').length).toBeGreaterThan(0)
    await userEvent.type(screen.getByLabelText('Write a reply'), 'Thanks')
    await userEvent.click(screen.getByRole('button', { name: 'Send' }))

    await waitFor(() => expect(calls.some((c) => c.method === 'POST' && c.url.includes('/messages'))).toBe(true))
    expect(calls.find((c) => c.method === 'POST')!.body).toEqual({ body: 'Thanks' })
  })

  it('switches replies off for announcements', async () => {
    signIn(makeMe({ roles: ['parent'], permissions: ['messaging.read', 'messaging.write'] }))
    mockApi(fetchMock, {
      'GET /messages/threads/t1': envelope({ id: 't1', subject: 'Closed Friday', student_id: null, is_broadcast: true, can_reply: false, messages: [] }),
      'GET /messages/threads': envelope([{ ...thread, subject: 'Closed Friday', is_broadcast: true }], { pagination: onePage }),
    })
    renderWithProviders(<MessagesPage />)

    await userEvent.click(await screen.findByRole('button', { name: /Closed Friday/ }))

    expect(await screen.findByText('This is an announcement. Replies are switched off.')).toBeInTheDocument()
    expect(screen.queryByLabelText('Write a reply')).not.toBeInTheDocument()
  })

  it('shows Arabic labels and an empty state', async () => {
    await setLanguage('ar')
    signIn(makeMe({ permissions: ['messaging.read', 'messaging.write'] }))
    mockApi(fetchMock, { 'GET /messages/threads': envelope([], { pagination: onePage }) })
    renderWithProviders(<MessagesPage />)

    expect(await screen.findByText('لا توجد محادثات بعد')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'رسالة جديدة' })).toBeInTheDocument()
  })
})

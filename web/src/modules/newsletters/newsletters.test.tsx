import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { clean } from '@/components/shared/SafeHtml'
import { envelope, makeMe, mockApi, onePage, renderWithProviders, setLanguage, signIn, signOut } from '@/test/utils'
import NewslettersPage from './NewslettersPage'
import ResourcesPage from '../resources/ResourcesPage'

const fetchMock = vi.fn()

const item = (overrides = {}) => ({
  id: 'n1', title: { en: 'October news', ar: 'أخبار أكتوبر' }, audience_type: 'all', audience_ids: [], status: 'published', scheduled_for: null, published_at: '2026-10-10T08:00:00Z', recipients: 10, opened: 4, created_at: '', ...overrides,
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

describe('safe HTML', () => {
  it('removes scripts, event handlers and images and keeps simple formatting', () => {
    const out = clean('<p onclick="x()">Hi <strong>there</strong><script>alert(1)</script><img src=x onerror=alert(2)></p><a href="javascript:alert(3)">bad</a>')
    expect(out).toContain('<strong>there</strong>')
    expect(out).not.toMatch(/script|onclick|onerror|<img|javascript:/)
  })
})

describe('NewslettersPage', () => {
  it('lists newsletters for a manager with how many parents read each', async () => {
    signIn(makeMe({ permissions: ['messaging.read', 'messaging.broadcast'] }))
    mockApi(fetchMock, { 'GET /newsletters': envelope([item(), item({ id: 'n2', title: { en: 'Draft news' }, status: 'draft', published_at: null, recipients: 0, opened: 0 })], { pagination: onePage }) })
    renderWithProviders(<NewslettersPage />)

    expect(await screen.findByText('October news')).toBeInTheDocument()
    expect(screen.getByText(/4 of 10 read it/)).toBeInTheDocument()
    expect(screen.getByRole('button', { name: /Results/ })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: /New newsletter/ })).toBeInTheDocument()
  })

  it('saves a draft with both languages and the chosen audience', async () => {
    signIn(makeMe({ permissions: ['messaging.read', 'messaging.broadcast'] }))
    const calls = mockApi(fetchMock, {
      'POST /newsletters': envelope({ newsletter: item({ status: 'draft' }), body: { en: '<p>x</p>' } }, { status: 201 }),
      'GET /newsletters': envelope([], { pagination: onePage }),
      'GET /rooms': envelope([], { pagination: onePage }),
      'GET /programs': envelope([], { pagination: onePage }),
    })
    renderWithProviders(<NewslettersPage />)

    await userEvent.click(await screen.findByRole('button', { name: /New newsletter/ }))
    const dialog = await screen.findByRole('dialog')
    await userEvent.type(within(dialog).getByLabelText(/Title \(English\)/), 'Open day')
    await userEvent.type(within(dialog).getByLabelText(/Title \(Arabic\)/), 'يوم مفتوح')
    const editor = within(dialog).getByRole('textbox', { name: /Newsletter text \(English\)/ })
    editor.innerHTML = '<p>Join us on Sunday</p>'
    editor.dispatchEvent(new Event('input', { bubbles: true }))
    await userEvent.click(within(dialog).getByRole('button', { name: 'Save draft' }))

    await waitFor(() => expect(calls.some((c) => c.method === 'POST')).toBe(true))
    expect(calls.find((c) => c.method === 'POST')!.body).toMatchObject({ title: { en: 'Open day', ar: 'يوم مفتوح' }, body: { en: '<p>Join us on Sunday</p>' }, audience_type: 'all', audience_ids: [] })
  })

  it('shows a parent only their received newsletters and opens one to read', async () => {
    signIn(makeMe({ roles: ['parent'], permissions: ['messaging.read'] }))
    mockApi(fetchMock, {
      'GET /newsletters/n1/read': envelope({ id: 'n1', title: { en: 'October news' }, body: '<p>Hello <strong>parents</strong><script>alert(1)</script></p>', language: 'en', published_at: '2026-10-10T08:00:00Z' }),
      'GET /newsletters/mine': envelope([{ id: 'n1', title: { en: 'October news' }, published_at: '2026-10-10T08:00:00Z', opened: false, created_at: '' }], { pagination: onePage }),
    })
    renderWithProviders(<NewslettersPage />)

    await userEvent.click(await screen.findByRole('button', { name: /October news/ }))
    const dialog = await screen.findByRole('dialog')
    expect(await within(dialog).findByText('parents')).toBeInTheDocument()
    expect(dialog.querySelector('script')).toBeNull()
    expect(screen.queryByRole('button', { name: /New newsletter/ })).not.toBeInTheDocument()
  })

  it('works in Arabic', async () => {
    await setLanguage('ar')
    signIn(makeMe({ permissions: ['messaging.read', 'messaging.broadcast'] }))
    mockApi(fetchMock, { 'GET /newsletters': envelope([], { pagination: onePage }) })
    renderWithProviders(<NewslettersPage />)

    expect(await screen.findByText('لا توجد نشرات بعد')).toBeInTheDocument()
    expect(document.documentElement.dir).toBe('rtl')
  })
})

describe('ResourcesPage', () => {
  const folder = { id: 'f1', name: { en: 'Handbooks', ar: 'الأدلة' }, audience: 'parents', parent_folder_id: null, documents: 1 }
  const doc = { id: 'd1', folder_id: 'f1', title: 'Parent handbook', description: null, tags: 'handbook', current_version: 2, file_name: 'handbook.pdf', content_type: 'application/pdf', size_bytes: 2048, updated_at: '2026-10-01T00:00:00Z', downloads: 3, created_at: '' }

  it('gives staff upload, version and history tools', async () => {
    signIn(makeMe({ permissions: ['resources.read', 'resources.write'] }))
    mockApi(fetchMock, { 'GET /resources/folders': envelope([folder]), 'GET /resources/documents': envelope([doc], { pagination: onePage }) })
    renderWithProviders(<ResourcesPage />)

    expect(await screen.findByText('Parent handbook')).toBeInTheDocument()
    expect(screen.getByText(/v2/)).toBeInTheDocument()
    expect(screen.getAllByRole('button', { name: 'Upload document' }).length).toBeGreaterThan(0)
    expect(screen.getByRole('button', { name: 'New version' })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Versions and downloads' })).toBeInTheDocument()
  })

  it('gives a parent only download', async () => {
    signIn(makeMe({ roles: ['parent'], permissions: ['resources.read'] }))
    mockApi(fetchMock, { 'GET /resources/folders': envelope([folder]), 'GET /resources/documents': envelope([doc], { pagination: onePage }) })
    renderWithProviders(<ResourcesPage />)

    expect(await screen.findByRole('button', { name: 'Download' })).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Upload document' })).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'New version' })).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /New folder/ })).not.toBeInTheDocument()
  })

  it('works in Arabic', async () => {
    await setLanguage('ar')
    signIn(makeMe({ permissions: ['resources.read', 'resources.write'] }))
    mockApi(fetchMock, { 'GET /resources/folders': envelope([]), 'GET /resources/documents': envelope([], { pagination: onePage }) })
    renderWithProviders(<ResourcesPage />)

    expect(await screen.findByText('لا توجد مستندات هنا')).toBeInTheDocument()
    expect(document.documentElement.dir).toBe('rtl')
  })
})

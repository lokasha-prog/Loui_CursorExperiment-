import { screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { Route, Routes } from 'react-router-dom'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { envelope, makeMe, mockApi, onePage, renderWithProviders, setLanguage, signIn, signOut } from '@/test/utils'
import GalleryPage from './GalleryPage'

const fetchMock = vi.fn()

const post = (overrides = {}) => ({
  id: 'p1', type: 'photo', caption: { en: 'Our nursery', ar: 'حضانتنا' }, posted_by_user_id: 'u2', posted_by_name: 'Salam Zabalawi', room_id: null,
  student_ids: [], media: [{ id: 'm1', content_type: 'image/png' }], status: 'approved', rejection_reason: null, created_at: '2026-10-24T08:00:00Z',
  ...overrides,
})

function renderPage() {
  return renderWithProviders(
    <Routes>
      <Route path="/gallery" element={<GalleryPage />} />
    </Routes>,
    { route: '/gallery' },
  )
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

describe('GalleryPage', () => {
  it('shows a parent "My gallery" with whole-nursery photos, read-only', async () => {
    signIn(makeMe({ roles: ['parent'], permissions: ['students.read', 'activities.read'] }))
    const calls = mockApi(fetchMock, { 'GET /activities': envelope([post()], { pagination: onePage }) })
    renderPage()

    expect(await screen.findByRole('heading', { name: 'My gallery' })).toBeInTheDocument()
    await userEvent.click(await screen.findByRole('button', { name: 'Open photo: Our nursery' }))
    const dialog = await screen.findByRole('dialog')
    expect(within(dialog).getByText('Whole nursery')).toBeInTheDocument()
    expect(within(dialog).queryByRole('button', { name: 'Remove' })).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Add photos' })).not.toBeInTheDocument()
    expect(calls[0].url).toContain('with_media=true')
  })

  it('lets an administrator choose who sees new photos, with other branches only for group managers', async () => {
    signIn(makeMe({ permissions: ['students.read', 'activities.read', 'activities.write', 'activities.approve', 'centre_structure.read'] }))
    mockApi(fetchMock, {
      'GET /activities': envelope([], { pagination: onePage }),
      'GET /rooms': envelope([{ id: 'r1', name: { en: 'Little Stars' } }], { pagination: onePage }),
      'GET /groups': envelope([{ id: 'g1', name: { en: 'Busy Bees' }, room_id: 'r1', created_at: '2026-01-01T00:00:00Z' }], { pagination: onePage }),
      'GET /students': envelope([], { pagination: onePage }),
    })
    const admin = renderPage()

    await userEvent.click(await screen.findByRole('button', { name: 'Add photos' }))
    const dialog = await screen.findByRole('dialog', { name: 'Add photos to the gallery' })
    const audience = within(dialog).getByLabelText('Share with')
    expect(within(audience).queryByRole('option', { name: 'All branches' })).not.toBeInTheDocument()
    await userEvent.selectOptions(audience, 'group')
    expect(await within(dialog).findByRole('option', { name: 'Busy Bees' })).toBeInTheDocument()
    await userEvent.selectOptions(audience, 'room')
    expect(await within(dialog).findByRole('option', { name: 'Little Stars' })).toBeInTheDocument()
    admin.unmount()

    signIn(makeMe({ permissions: ['activities.read', 'activities.write', 'activities.approve', 'group.manage'] }))
    renderPage()
    await userEvent.click(await screen.findByRole('button', { name: 'Add photos' }))
    const owner = await screen.findByRole('dialog', { name: 'Add photos to the gallery' })
    expect(within(within(owner).getByLabelText('Share with')).getByRole('option', { name: 'All branches' })).toBeInTheDocument()
  })

  it('does not let teachers add from the gallery page', async () => {
    signIn(makeMe({ permissions: ['students.read', 'activities.read', 'activities.write'] }))
    mockApi(fetchMock, { 'GET /activities': envelope([], { pagination: onePage }) })
    renderPage()

    expect(await screen.findByRole('heading', { name: 'Gallery' })).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Add photos' })).not.toBeInTheDocument()
  })

  it('is translated in Arabic', async () => {
    signIn(makeMe({ roles: ['parent'], permissions: ['activities.read'] }))
    mockApi(fetchMock, { 'GET /activities': envelope([], { pagination: onePage }) })
    await setLanguage('ar')
    renderPage()

    expect(await screen.findByRole('heading', { name: 'معرض صوري' })).toBeInTheDocument()
  })
})

import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { envelope, makeMe, mockApi, renderWithProviders, setLanguage, signIn, signOut } from '@/test/utils'
import SettingsPage from './SettingsPage'

const fetchMock = vi.fn()

const settings = {
  name: { en: 'Bright Futures', ar: 'برايت فيوتشرز' }, contact_email: 'hello@bf.test', contact_phone: null, khda_license_number: null, weekend_days: ['fri', 'sat'], timezone: 'Asia/Dubai',
  address: null, website: null, brand_color: '#0E7C86', logo_media_id: null, late_grace_minutes: 15, early_leave_grace_minutes: 15, parents_see_invoices: true,
}

const role = (overrides = {}) => ({ id: 'r1', name: 'custom_helper', display_name: { en: 'Helper' }, description: null, is_system_role: false, permissions: ['enquiries.read'], user_count: 0, ...overrides })

beforeEach(async () => {
  fetchMock.mockReset()
  vi.stubGlobal('fetch', fetchMock)
  await setLanguage('en')
})

afterEach(() => {
  vi.unstubAllGlobals()
  signOut()
})

describe('SettingsPage', () => {
  it('saves the centre profile and rules', async () => {
    signIn(makeMe({ permissions: ['settings.read', 'settings.write'] }))
    const calls = mockApi(fetchMock, { 'PUT /settings/centre': envelope(settings), 'GET /settings/centre': envelope(settings) })
    renderWithProviders(<SettingsPage />)

    const late = await screen.findByLabelText(/Late after/)
    await userEvent.clear(late)
    await userEvent.type(late, '20')
    await userEvent.click(screen.getByLabelText(/Parents can see their invoices/))
    await userEvent.click(screen.getByRole('button', { name: 'Save settings' }))

    await waitFor(() => expect(calls.some((c) => c.method === 'PUT')).toBe(true))
    expect(calls.find((c) => c.method === 'PUT')!.body).toMatchObject({ late_grace_minutes: 20, parents_see_invoices: false, weekend_days: ['fri', 'sat'], name: { en: 'Bright Futures' } })
  })

  it('shows no roles tab and a read-only form to someone without those rights', async () => {
    signIn(makeMe({ permissions: ['settings.read'] }))
    mockApi(fetchMock, { 'GET /settings/centre': envelope(settings) })
    renderWithProviders(<SettingsPage />)

    expect(await screen.findByLabelText(/Centre name \(English\)/)).toBeDisabled()
    expect(screen.queryByRole('tab', { name: 'Roles' })).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Save settings' })).not.toBeInTheDocument()
  })

  it('creates a custom role from chosen permissions', async () => {
    signIn(makeMe({ permissions: ['settings.read', 'settings.write', 'user_management.manage'] }))
    const calls = mockApi(fetchMock, {
      'POST /roles': envelope(role(), { status: 201 }),
      'GET /roles/permissions': envelope(['enquiries.read', 'enquiries.write', 'tours.read']),
      'GET /roles': envelope([role({ id: 'r0', name: 'teacher', is_system_role: true, permissions: ['students.read'] })]),
      'GET /settings/centre': envelope(settings),
    })
    renderWithProviders(<SettingsPage />)

    await userEvent.click(await screen.findByRole('tab', { name: 'Roles' }))
    await userEvent.click(await screen.findByRole('button', { name: /New role/ }))
    const dialog = await screen.findByRole('dialog')
    await userEvent.type(within(dialog).getByLabelText(/Role name \(English\)/), 'Open day helper')
    await userEvent.click((await within(dialog).findAllByRole('checkbox'))[0])
    await userEvent.click(within(dialog).getByRole('button', { name: 'Save role' }))

    await waitFor(() => expect(calls.some((c) => c.method === 'POST')).toBe(true))
    expect(calls.find((c) => c.method === 'POST')!.body).toMatchObject({ display_name: { en: 'Open day helper' }, permissions: ['enquiries.read'] })
  })

  it('works in Arabic', async () => {
    await setLanguage('ar')
    signIn(makeMe({ permissions: ['settings.read', 'settings.write'] }))
    mockApi(fetchMock, { 'GET /settings/centre': envelope(settings) })
    renderWithProviders(<SettingsPage />)

    expect(await screen.findByText('بيانات المركز')).toBeInTheDocument()
    expect(document.documentElement.dir).toBe('rtl')
  })
})

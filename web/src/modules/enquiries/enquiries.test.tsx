import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { Route, Routes } from 'react-router-dom'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { envelope, makeMe, mockApi, onePage, renderWithProviders, setLanguage, signIn, signOut } from '@/test/utils'
import EnquiryFormPage from '../public/EnquiryFormPage'
import EnquiriesPage from './EnquiriesPage'

const fetchMock = vi.fn()

const enquiry = (overrides = {}) => ({
  id: 'e1', family_name: 'Haddad', child_name: 'Sara Haddad', phone: '+971500000000', email: 'h@test.local', preferred_contact: 'phone', child_date_of_birth: '2024-06-01',
  desired_start_date: null, program_id: null, source: 'website', assigned_staff_id: null, assigned_staff_name: null, stage: 'interested', enquiry_date: '2026-10-01',
  last_contact_date: null, notes: null, is_archived: false, student_id: null, created_at: '', ...overrides,
})

const summary = envelope({
  total: 3, new_this_month: 2, enrolled: 1,
  by_stage: ['no_stage', 'interested', 'contacted', 'tour_appointment', 'toured', 'enrolled'].map((stage, i) => ({ stage, count: i === 1 ? 2 : i === 5 ? 1 : 0 })),
  by_source: [{ source: 'website', total: 3, enrolled: 1 }],
})

const detail = (overrides = {}) => ({
  enquiry: enquiry(overrides), history: [{ from_stage: null, to_stage: 'no_stage', reason: 'Enquiry received', at: '2026-10-01T08:00:00Z' }], activities: [],
})

const perms = ['enquiries.read', 'enquiries.write', 'tours.read', 'tours.write']

beforeEach(async () => {
  fetchMock.mockReset()
  vi.stubGlobal('fetch', fetchMock)
  await setLanguage('en')
})

afterEach(() => {
  vi.unstubAllGlobals()
  signOut()
})

describe('EnquiriesPage', () => {
  it('shows how many families are at each stage and where they come from', async () => {
    signIn(makeMe({ permissions: perms }))
    mockApi(fetchMock, { 'GET /enquiries/summary': summary, 'GET /enquiries': envelope([enquiry()], { pagination: onePage }) })
    renderWithProviders(<EnquiriesPage />)

    expect(await screen.findByRole('button', { name: 'Haddad' })).toBeInTheDocument()
    expect(screen.getByText('Sara Haddad')).toBeInTheDocument()
    expect(screen.getByText(/Website · 3 · 1 enrolled/)).toBeInTheDocument()
    expect(screen.getByRole('button', { name: /Interested/ })).toHaveTextContent('2')
  })

  it('adds an enquiry and refuses one with no way to reach the family', async () => {
    signIn(makeMe({ permissions: perms }))
    const calls = mockApi(fetchMock, {
      'POST /enquiries': envelope(detail(), { status: 201 }),
      'GET /enquiries/summary': summary,
      'GET /enquiries': envelope([], { pagination: onePage }),
      'GET /programs': envelope([], { pagination: onePage }),
      'GET /staff': envelope([], { pagination: onePage }),
      'GET /rooms': envelope([], { pagination: onePage }),
    })
    renderWithProviders(<EnquiriesPage />)

    await userEvent.click(await screen.findByRole('button', { name: /Add enquiry/ }))
    const dialog = await screen.findByRole('dialog')
    await userEvent.type(within(dialog).getByLabelText(/Family name/), 'Mansour')
    await userEvent.type(within(dialog).getByLabelText(/Child name/), 'Lina Mansour')
    await userEvent.click(within(dialog).getByRole('button', { name: 'Save enquiry' }))
    expect(await within(dialog).findByText(/Give a phone number or an e-mail/)).toBeInTheDocument()
    expect(calls.some((c) => c.method === 'POST')).toBe(false)

    await userEvent.type(within(dialog).getByLabelText('Phone'), '+971501112222')
    await userEvent.click(within(dialog).getByRole('button', { name: 'Save enquiry' }))

    await waitFor(() => expect(calls.some((c) => c.method === 'POST' && c.url.endsWith('/enquiries'))).toBe(true))
    expect(calls.find((c) => c.method === 'POST')!.body).toMatchObject({ family_name: 'Mansour', child_name: 'Lina Mansour', phone: '+971501112222', source: 'walk_in' })
  })

  it('works in Arabic', async () => {
    await setLanguage('ar')
    signIn(makeMe({ permissions: perms }))
    mockApi(fetchMock, { 'GET /enquiries/summary': summary, 'GET /enquiries': envelope([enquiry()], { pagination: onePage }) })
    renderWithProviders(<EnquiriesPage />)

    expect(await screen.findByRole('tab', { name: 'الزيارات' })).toBeInTheDocument()
    expect(document.documentElement.dir).toBe('rtl')
  })
})

describe('EnquiryFormPage', () => {
  const renderForm = () => renderWithProviders(<Routes><Route path="/enquire/:centre" element={<EnquiryFormPage />} /></Routes>, { route: '/enquire/bright' })

  it('lets a family send an enquiry and thanks them', async () => {
    const calls = mockApi(fetchMock, {
      'POST /public/enquiries/bright': envelope({ received: true }),
      'GET /public/enquiry-form/bright': envelope({ name: { en: 'Bright Futures', ar: 'برايت فيوتشرز' }, captcha_site_key: null }),
    })
    renderForm()

    await userEvent.type(await screen.findByLabelText(/Your name/), 'Al Marri')
    await userEvent.type(screen.getByLabelText(/Child name/), 'Hamad')
    await userEvent.type(screen.getByLabelText('E-mail'), 'marri@test.local')
    await userEvent.click(screen.getByRole('button', { name: 'Send enquiry' }))

    expect(await screen.findByText('Thank you!')).toBeInTheDocument()
    expect(calls.find((c) => c.method === 'POST')!.body).toMatchObject({ family_name: 'Al Marri', child_name: 'Hamad', email: 'marri@test.local', website: '' })
  })

  it('says so when the centre does not exist', async () => {
    mockApi(fetchMock, { 'GET /public/enquiry-form/bright': () => new Response(JSON.stringify({ success: false, data: null, meta: { request_id: 'x', pagination: null }, errors: [{ code: 'RESOURCE_NOT_FOUND', message: 'x', field: null }] }), { status: 404 }) })
    renderForm()

    expect(await screen.findByText('This form is not available')).toBeInTheDocument()
  })
})

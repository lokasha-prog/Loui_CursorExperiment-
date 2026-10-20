import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { envelope, makeMe, mockApi, onePage, renderWithProviders, setLanguage, signIn, signOut } from '@/test/utils'
import BillingPage from './BillingPage'
import { toFils } from './InvoiceDialog'

const fetchMock = vi.fn()

const invoice = (overrides = {}) => ({
  id: 'i1', invoice_number: 'INV-2026-00001', student_id: 's1', student_name: 'Layla Hassan', issue_date: '2026-10-01', due_date: '2026-10-15',
  period_start: '2026-10-01', period_end: '2026-10-31', total_fils: 210_000, paid_fils: 0, status: 'issued', created_at: '', ...overrides,
})

const detail = (overrides = {}) => ({
  invoice: invoice(overrides), fee_plan_id: null, subtotal_fils: 200_000, discount_fils: 0, tax_fils: 10_000, notes: null, sent_at: null,
  lines: [{ id: 'l1', description: { en: 'Tuition', ar: 'الرسوم الدراسية' }, quantity: 1, unit_price_fils: 200_000, discount_fils: 0, tax_fils: 10_000, total_fils: 210_000 }],
  payments: [],
})

const snapshot = envelope({
  month: '2026-10', billed_fils: 840_000, collected_fils: 210_000, overdue_fils: 50_000, outstanding_fils: 580_000, new_invoices_today: 1, new_invoices_today_fils: 210_000,
  recent_payments: [{ invoice_id: 'i1', invoice_number: 'INV-2026-00001', student_name: 'Layla Hassan', amount_fils: 210_000, payment_date: '2026-10-05', method: 'cash' }],
  top_overdue: [{ student_id: 's2', student_name: 'Omar Haddad', outstanding_fils: 50_000, invoices: 1 }],
  trend: Array.from({ length: 6 }, (_, i) => ({ month: `2026-0${i + 5}`, billed_fils: 100_000 * (i + 1), collected_fils: 50_000 * (i + 1) })),
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

describe('toFils', () => {
  it.each([['1,250.50', 125050], ['10', 1000], ['0.05', 5]])('turns %s into %i fils', (text, fils) => expect(toFils(text)).toBe(fils))
  it.each([['', null], ['abc', null], ['0', null], ['-5', null]])('refuses %j', (text, fils) => expect(toFils(text)).toBe(fils))
})

describe('BillingPage', () => {
  it('shows the manager the month in numbers, who owes most and recent payments', async () => {
    signIn(makeMe({ permissions: ['billing.read', 'billing.write', 'payments.record', 'invoices.generate'] }))
    mockApi(fetchMock, { 'GET /dashboard/billing-snapshot': snapshot, 'GET /invoices': envelope([], { pagination: onePage }) })
    renderWithProviders(<BillingPage />)

    expect(await screen.findByText('Billed this month')).toBeInTheDocument()
    expect(screen.getByText(/8,400\.00/)).toBeInTheDocument()
    expect(screen.getByText('Omar Haddad')).toBeInTheDocument()
    expect(screen.getByText(/INV-2026-00001/)).toBeInTheDocument()
  })

  it('records a payment and refuses an amount that is not a number', async () => {
    signIn(makeMe({ permissions: ['billing.read', 'billing.write', 'payments.record'] }))
    const calls = mockApi(fetchMock, {
      'POST /invoices/i1/payments': envelope(detail({ status: 'partially_paid', paid_fils: 100_000 }), { status: 201 }),
      'GET /invoices/i1': envelope(detail()),
      'GET /invoices': envelope([invoice()], { pagination: onePage }),
      'GET /dashboard/billing-snapshot': snapshot,
    })
    renderWithProviders(<BillingPage />)

    await userEvent.click(await screen.findByRole('tab', { name: 'Invoices' }))
    await userEvent.click(await screen.findByRole('button', { name: 'INV-2026-00001' }))
    const dialog = await screen.findByRole('dialog')
    expect(await within(dialog).findByText('Tuition')).toBeInTheDocument()
    const amount = within(dialog).getByLabelText('Amount (AED)')
    await userEvent.clear(amount)
    await userEvent.type(amount, 'abc')
    await userEvent.click(within(dialog).getByRole('button', { name: 'Record payment' }))
    expect(calls.some((c) => c.method === 'POST')).toBe(false)

    await userEvent.clear(amount)
    await userEvent.type(amount, '1000')
    await userEvent.click(within(dialog).getByRole('button', { name: 'Record payment' }))

    await waitFor(() => expect(calls.some((c) => c.method === 'POST' && c.url.includes('/payments'))).toBe(true))
    expect(calls.find((c) => c.method === 'POST')!.body).toMatchObject({ amount_fils: 100_000, method: 'cash' })
  })

  it('shows a parent only their invoices, with no payment or management tools', async () => {
    signIn(makeMe({ roles: ['parent'], permissions: ['billing.read', 'students.read'] }))
    mockApi(fetchMock, { 'GET /invoices/i1': envelope(detail()), 'GET /invoices': envelope([invoice()], { pagination: onePage }) })
    renderWithProviders(<BillingPage />)

    await userEvent.click(await screen.findByRole('button', { name: 'INV-2026-00001' }))
    const dialog = await screen.findByRole('dialog')

    expect(screen.queryByRole('tab')).not.toBeInTheDocument()
    expect(await within(dialog).findByText('Tuition')).toBeInTheDocument()
    expect(within(dialog).queryByText('Record a payment')).not.toBeInTheDocument()
    expect(within(dialog).queryByRole('button', { name: /Cancel invoice/ })).not.toBeInTheDocument()
    expect(within(dialog).getByRole('button', { name: /Download PDF/ })).toBeInTheDocument()
  })

  it('creates a fee plan with an item and discount', async () => {
    signIn(makeMe({ permissions: ['billing.read', 'billing.write'] }))
    const calls = mockApi(fetchMock, {
      'POST /fee-plans': envelope({ plan: { id: 'p1' }, students: [] }, { status: 201 }),
      'GET /programs': envelope([], { pagination: onePage }),
      'GET /fee-plans': envelope([], { pagination: onePage }),
      'GET /dashboard/billing-snapshot': snapshot,
      'GET /invoices': envelope([], { pagination: onePage }),
    })
    renderWithProviders(<BillingPage />)

    await userEvent.click(await screen.findByRole('tab', { name: 'Fee plans' }))
    await userEvent.click(await screen.findByRole('button', { name: /New fee plan/ }))
    const dialog = await screen.findByRole('dialog')
    await userEvent.type(within(dialog).getByLabelText(/Name \(English\)/), 'Full day monthly')
    await userEvent.type(within(dialog).getByLabelText('Item'), 'Tuition')
    await userEvent.type(within(dialog).getByLabelText(/Amount \(AED\)/), '2000')
    await userEvent.click(within(dialog).getByRole('button', { name: 'Save plan' }))

    await waitFor(() => expect(calls.some((c) => c.method === 'POST')).toBe(true))
    expect(calls.find((c) => c.method === 'POST')!.body).toMatchObject({
      name: { en: 'Full day monthly' }, period: 'monthly', status: 'active',
      components: [{ name: { en: 'Tuition' }, component_type: 'tuition', amount_fils: 200_000, tax_rate_percent: 5 }],
    })
  })

  it('works in Arabic', async () => {
    await setLanguage('ar')
    signIn(makeMe({ permissions: ['billing.read', 'billing.write'] }))
    mockApi(fetchMock, { 'GET /dashboard/billing-snapshot': snapshot, 'GET /invoices': envelope([], { pagination: onePage }) })
    renderWithProviders(<BillingPage />)

    expect(await screen.findByText('المفوتر هذا الشهر')).toBeInTheDocument()
    expect(screen.getByRole('tab', { name: 'خطط الرسوم' })).toBeInTheDocument()
    expect(document.documentElement.dir).toBe('rtl')
  })
})

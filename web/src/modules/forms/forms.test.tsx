import { render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import type { FormFieldDef } from '@/api/types'
import { envelope, makeMe, mockApi, onePage, renderWithProviders, setLanguage, signIn, signOut } from '@/test/utils'
import FormsPage from './FormsPage'
import { FormRenderer, cleanAnswers, isVisible } from './FormRenderer'

const fetchMock = vi.fn()

const field = (overrides: Partial<FormFieldDef> = {}): FormFieldDef => ({
  id: 'q1', type: 'text', label: { en: 'Child name', ar: 'اسم الطفل' }, required: false, options: [], show_when_field_id: null, show_when_equals: null, ...overrides,
})

const transport = field({ id: 'transport', type: 'dropdown', label: { en: 'Transport' }, options: [{ value: 'bus', label: { en: 'Bus' } }, { value: 'own', label: { en: 'Own' } }] })
const stop = field({ id: 'stop', label: { en: 'Bus stop' }, required: true, show_when_field_id: 'transport', show_when_equals: 'bus' })

const formItem = (overrides = {}) => ({ id: 'f1', name: { en: 'Admission form' }, description: null, type: 'admission', status: 'live', is_public: false, published_version: 1, submissions: 2, created_at: '', ...overrides })

beforeEach(async () => {
  fetchMock.mockReset()
  vi.stubGlobal('fetch', fetchMock)
  await setLanguage('en')
})

afterEach(() => {
  vi.unstubAllGlobals()
  signOut()
})

describe('show-only-if rules', () => {
  it('shows a question only when the answer it depends on matches', () => {
    expect(isVisible(stop, { transport: 'bus' })).toBe(true)
    expect(isVisible(stop, { transport: 'own' })).toBe(false)
    expect(isVisible(stop, {})).toBe(false)
    expect(isVisible(field(), {})).toBe(true)
  })

  it('keeps only visible, non-empty answers and turns numbers into numbers', () => {
    const fields = [transport, stop, field({ id: 'age', type: 'number', label: { en: 'Age' } }), field({ id: 'head', type: 'section', label: { en: 'Head' } })]
    expect(cleanAnswers(fields, { transport: 'own', stop: 'Gate 4', age: '3', head: 'x' })).toEqual({ transport: 'own', age: 3 })
  })
})

describe('FormRenderer', () => {
  it('blocks sending until the required visible questions are answered, then sends only what matters', async () => {
    const onSubmit = vi.fn()
    render(<FormRenderer fields={[transport, stop]} submitLabel="Send" onSubmit={onSubmit} />)

    expect(screen.queryByLabelText(/Bus stop/)).not.toBeInTheDocument()
    await userEvent.selectOptions(screen.getByLabelText('Transport'), 'bus')
    await userEvent.click(screen.getByRole('button', { name: 'Send' }))
    expect(onSubmit).not.toHaveBeenCalled()
    expect(await screen.findByText('This field is required')).toBeInTheDocument()

    await userEvent.type(screen.getByLabelText(/Bus stop/), 'Gate 4')
    await userEvent.click(screen.getByRole('button', { name: 'Send' }))
    expect(onSubmit).toHaveBeenCalledWith({ transport: 'bus', stop: 'Gate 4' })
  })

  it('tells which question was last touched', async () => {
    const onProgress = vi.fn()
    render(<FormRenderer fields={[field()]} submitLabel="Send" onSubmit={vi.fn()} onProgress={onProgress} />)

    await userEvent.type(screen.getByLabelText('Child name'), 'S')
    expect(onProgress).toHaveBeenLastCalledWith('q1')
  })
})

describe('FormsPage', () => {
  it('lets a form manager list forms and open the builder', async () => {
    signIn(makeMe({ permissions: ['forms.read', 'forms.write'] }))
    mockApi(fetchMock, { 'GET /forms': envelope([formItem(), formItem({ id: 'f2', name: { en: 'Trip slip' }, status: 'draft', published_version: 0 })], { pagination: onePage }) })
    renderWithProviders(<FormsPage />)

    expect(await screen.findByText('Admission form')).toBeInTheDocument()
    expect(screen.getByText('Trip slip')).toBeInTheDocument()
    expect(screen.getByRole('tab', { name: 'Admissions' })).toBeInTheDocument()
    await userEvent.click(screen.getByRole('button', { name: /New form/ }))
    expect(await screen.findByRole('dialog')).toBeInTheDocument()
  })

  it('creates a form from a name and kind', async () => {
    signIn(makeMe({ permissions: ['forms.read', 'forms.write'] }))
    const calls = mockApi(fetchMock, {
      'POST /forms': envelope({ form: formItem({ id: 'new', status: 'draft', published_version: 0 }), editing_version: 1, has_unpublished_changes: false, fields: [], published_fields: [] }, { status: 201 }),
      'GET /forms/new': envelope({ form: formItem({ id: 'new', status: 'draft', published_version: 0 }), editing_version: 1, has_unpublished_changes: false, fields: [], published_fields: [] }),
      'GET /forms': envelope([], { pagination: onePage }),
    })
    renderWithProviders(<FormsPage />)

    await userEvent.click(await screen.findByRole('button', { name: /New form/ }))
    const dialog = await screen.findByRole('dialog')
    await userEvent.type(within(dialog).getByLabelText(/Form name \(English\)/), 'Incident report')
    await userEvent.selectOptions(within(dialog).getByLabelText('Kind of form'), 'incident')
    await userEvent.click(within(dialog).getByRole('button', { name: 'Save' }))

    await waitFor(() => expect(calls.some((c) => c.method === 'POST')).toBe(true))
    expect(calls.find((c) => c.method === 'POST')!.body).toMatchObject({ name: { en: 'Incident report' }, type: 'incident', is_public: false })
  })

  it('shows a parent only fill-in buttons and no builder or answers', async () => {
    signIn(makeMe({ roles: ['parent'], permissions: ['forms.read', 'students.read'] }))
    mockApi(fetchMock, { 'GET /forms': envelope([formItem()], { pagination: onePage }), 'GET /students': envelope([], { pagination: onePage }) })
    renderWithProviders(<FormsPage />)

    expect(await screen.findByRole('button', { name: 'Fill in' })).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /New form/ })).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Answers' })).not.toBeInTheDocument()
    expect(screen.queryByRole('tab')).not.toBeInTheDocument()
  })

  it('works in Arabic', async () => {
    await setLanguage('ar')
    signIn(makeMe({ permissions: ['forms.read', 'forms.write'] }))
    mockApi(fetchMock, { 'GET /forms': envelope([], { pagination: onePage }) })
    renderWithProviders(<FormsPage />)

    expect(await screen.findByText('لا توجد نماذج بعد')).toBeInTheDocument()
    expect(document.documentElement.dir).toBe('rtl')
  })
})

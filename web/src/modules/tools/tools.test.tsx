import { screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { envelope, makeMe, mockApi, onePage, renderWithProviders, setLanguage, signIn, signOut } from '@/test/utils'
import ToolsPage from './ToolsPage'

const fetchMock = vi.fn()

const template = (id: string, scope: string, name: string) => ({ id, scope, name: { en: name }, layout: 'band', primary: '#F59E0B', secondary: '#FEF3C7', accent: '#B45309' })

beforeEach(async () => {
  fetchMock.mockReset()
  vi.stubGlobal('fetch', fetchMock)
  vi.stubGlobal('URL', Object.assign(URL, { createObjectURL: vi.fn(() => 'blob:x'), revokeObjectURL: vi.fn() }))
  vi.stubGlobal('open', vi.fn())
  await setLanguage('en')
})

afterEach(() => {
  vi.unstubAllGlobals()
  signOut()
})

describe('ToolsPage', () => {
  it('asks for at least one person, then sends the chosen design and people and opens the PDF', async () => {
    signIn(makeMe({ permissions: ['tools.use', 'students.read', 'staff.read'] }))
    const calls = mockApi(fetchMock, {
      'POST /tools/id-cards': () => new Response(new Blob(['%PDF-1.4'], { type: 'application/pdf' }), { status: 200 }),
      'GET /tools/id-card-templates': envelope([template('student-sunshine', 'student', 'Sunshine'), template('staff-modern', 'staff', 'Modern')]),
      'GET /students': envelope([{ id: 's1', first_name: 'Layla', last_name: 'Hassan' }, { id: 's2', first_name: 'Omar', last_name: 'Haddad' }], { pagination: onePage }),
      'GET /staff': envelope([], { pagination: onePage }),
    })
    renderWithProviders(<ToolsPage />)

    await userEvent.click(await screen.findByRole('button', { name: 'Create PDF' }))
    expect(await screen.findByText('Choose at least one person.')).toBeInTheDocument()

    await userEvent.click(await screen.findByLabelText('Layla Hassan'))
    await userEvent.click(screen.getByLabelText('Expiry date'))
    await userEvent.click(screen.getByRole('button', { name: 'Create PDF' }))

    await waitFor(() => expect(calls.some((c) => c.method === 'POST')).toBe(true))
    expect(calls.find((c) => c.method === 'POST')!.body).toMatchObject({ scope: 'student', ids: ['s1'], template: 'student-sunshine', size: 'a6', language: 'en' })
    expect((calls.find((c) => c.method === 'POST')!.body as { fields: string[] }).fields).not.toContain('expiry')
    await waitFor(() => expect(window.open).toHaveBeenCalled())
  })

  it('works in Arabic', async () => {
    await setLanguage('ar')
    signIn(makeMe({ permissions: ['tools.use', 'students.read', 'staff.read'] }))
    mockApi(fetchMock, { 'GET /tools/id-card-templates': envelope([]), 'GET /students': envelope([], { pagination: onePage }) })
    renderWithProviders(<ToolsPage />)

    expect(await screen.findByText('بطاقات الهوية')).toBeInTheDocument()
    expect(document.documentElement.dir).toBe('rtl')
  })
})

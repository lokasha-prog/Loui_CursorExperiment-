import { screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { Route, Routes } from 'react-router-dom'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { envelope, failure, renderWithProviders, setLanguage, signOut } from '@/test/utils'
import { useAuth } from '@/stores/auth'
import { makeMe } from '@/test/utils'
import LoginPage from './LoginPage'
import SetupPage from './SetupPage'

const fetchMock = vi.fn()

function routeFetch(handlers: Record<string, () => Response>) {
  fetchMock.mockImplementation((url: string) => {
    const key = Object.keys(handlers).find((k) => String(url).includes(k))
    return Promise.resolve(key ? handlers[key]() : failure(404, 'RESOURCE_NOT_FOUND'))
  })
}

beforeEach(async () => {
  fetchMock.mockReset()
  vi.stubGlobal('fetch', fetchMock)
  signOut()
  await setLanguage('en')
})

afterEach(() => vi.unstubAllGlobals())

function renderLogin() {
  return renderWithProviders(
    <Routes>
      <Route path="/login" element={<LoginPage />} />
      <Route path="/dashboard" element={<p>DASHBOARD</p>} />
      <Route path="/setup" element={<p>SETUP</p>} />
    </Routes>,
    { route: '/login' },
  )
}

describe('LoginPage', () => {
  it('shows required-field messages in English', async () => {
    routeFetch({ 'setup-status': () => envelope({ setup_required: false, password_min_length: 12 }) })
    renderLogin()

    await userEvent.click(screen.getByRole('button', { name: 'Sign in' }))

    expect(await screen.findAllByText('This field is required')).toHaveLength(2)
  })

  it('shows the same messages in Arabic, and mirrors the page', async () => {
    routeFetch({ 'setup-status': () => envelope({ setup_required: false, password_min_length: 12 }) })
    await setLanguage('ar')
    renderLogin()

    await userEvent.click(screen.getByRole('button', { name: 'تسجيل الدخول' }))

    expect(await screen.findAllByText('هذا الحقل مطلوب')).toHaveLength(2)
    expect(document.documentElement.dir).toBe('rtl')
  })

  it('rejects an invalid email before calling the server', async () => {
    routeFetch({ 'setup-status': () => envelope({ setup_required: false, password_min_length: 12 }) })
    renderLogin()

    await userEvent.type(screen.getByLabelText(/Email/), 'not-an-email')
    await userEvent.type(screen.getByLabelText(/Password/), 'secret')
    await userEvent.click(screen.getByRole('button', { name: 'Sign in' }))

    expect(await screen.findByText('Enter a valid email address')).toBeInTheDocument()
    expect(fetchMock.mock.calls.some(([url]) => String(url).includes('/auth/login'))).toBe(false)
  })

  it('signs in and moves to the dashboard', async () => {
    routeFetch({
      'setup-status': () => envelope({ setup_required: false, password_min_length: 12 }),
      '/auth/login': () => envelope({ access_token: 'tok', expires_at: '', me: makeMe() }),
    })
    renderLogin()

    await userEvent.type(screen.getByLabelText(/Email/), 'admin@test.local')
    await userEvent.type(screen.getByLabelText(/Password/), 'secret-password')
    await userEvent.click(screen.getByRole('button', { name: 'Sign in' }))

    expect(await screen.findByText('DASHBOARD')).toBeInTheDocument()
    expect(useAuth.getState().accessToken).toBe('tok')
  })

  it('explains wrong credentials without saying which part was wrong', async () => {
    routeFetch({
      'setup-status': () => envelope({ setup_required: false, password_min_length: 12 }),
      '/auth/login': () => failure(401, 'UNAUTHORIZED', 'Invalid email or password.'),
    })
    renderLogin()

    await userEvent.type(screen.getByLabelText(/Email/), 'admin@test.local')
    await userEvent.type(screen.getByLabelText(/Password/), 'wrong')
    await userEvent.click(screen.getByRole('button', { name: 'Sign in' }))

    expect(await screen.findByRole('alert')).toHaveTextContent('The email or password is not correct.')
  })

  it('explains a locked account', async () => {
    routeFetch({
      'setup-status': () => envelope({ setup_required: false, password_min_length: 12 }),
      '/auth/login': () => failure(401, 'UNAUTHORIZED', 'This account is temporarily locked. Try again later.'),
    })
    renderLogin()

    await userEvent.type(screen.getByLabelText(/Email/), 'admin@test.local')
    await userEvent.type(screen.getByLabelText(/Password/), 'x')
    await userEvent.click(screen.getByRole('button', { name: 'Sign in' }))

    expect(await screen.findByRole('alert')).toHaveTextContent(/locked/i)
  })

  it('toggles password visibility with an accessible control', async () => {
    routeFetch({ 'setup-status': () => envelope({ setup_required: false, password_min_length: 12 }) })
    renderLogin()
    const input = screen.getByLabelText(/Password/)

    expect(input).toHaveAttribute('type', 'password')
    await userEvent.click(screen.getByRole('button', { name: 'Show password' }))
    expect(input).toHaveAttribute('type', 'text')
  })

  it('offers first-time setup only when the centre has not been set up', async () => {
    routeFetch({ 'setup-status': () => envelope({ setup_required: true, password_min_length: 12 }) })
    renderLogin()

    expect(await screen.findByRole('link', { name: 'Set up your centre' })).toBeInTheDocument()
  })

  it('does not offer setup once a centre exists', async () => {
    routeFetch({ 'setup-status': () => envelope({ setup_required: false, password_min_length: 12 }) })
    renderLogin()

    await waitFor(() => expect(fetchMock).toHaveBeenCalled())
    expect(screen.queryByRole('link', { name: 'Set up your centre' })).not.toBeInTheDocument()
  })
})

describe('SetupPage', () => {
  function renderSetup() {
    return renderWithProviders(
      <Routes>
        <Route path="/setup" element={<SetupPage />} />
        <Route path="/login" element={<p>LOGIN</p>} />
      </Routes>,
      { route: '/setup' },
    )
  }

  it('sends people to sign in when setup is already done', async () => {
    routeFetch({ 'setup-status': () => envelope({ setup_required: false, password_min_length: 12 }) })
    renderSetup()

    expect(await screen.findByText('LOGIN')).toBeInTheDocument()
  })

  it('uses the password rule supplied by the server', async () => {
    routeFetch({ 'setup-status': () => envelope({ setup_required: true, password_min_length: 14 }) })
    renderSetup()

    expect(await screen.findByText('At least 14 characters with letters and numbers')).toBeInTheDocument()
  })

  it('validates every required field and the centre code format', async () => {
    routeFetch({ 'setup-status': () => envelope({ setup_required: true, password_min_length: 12 }) })
    renderSetup()
    await screen.findByRole('button', { name: 'Create centre' })

    await userEvent.type(screen.getByLabelText(/Centre code/), 'Bad Code!')
    await userEvent.click(screen.getByRole('button', { name: 'Create centre' }))

    expect(await screen.findByText('Use lowercase letters, numbers and hyphens only')).toBeInTheDocument()
    expect(screen.getAllByText('This field is required').length).toBeGreaterThanOrEqual(5)
  })

  it('shows a clear message for a wrong setup key', async () => {
    routeFetch({
      'setup-status': () => envelope({ setup_required: true, password_min_length: 12 }),
      '/bootstrap-super-admin': () => failure(404, 'RESOURCE_NOT_FOUND'),
    })
    renderSetup()
    await screen.findByRole('button', { name: 'Create centre' })

    await userEvent.type(screen.getByLabelText(/Setup key/), 'wrong')
    await userEvent.type(screen.getByLabelText(/Centre name \(English\)/), 'Sunrise')
    await userEvent.type(screen.getByLabelText(/Centre code/), 'sunrise')
    await userEvent.type(screen.getByLabelText(/First name/), 'Layla')
    await userEvent.type(screen.getByLabelText(/Last name/), 'Hassan')
    await userEvent.type(screen.getByLabelText(/^Email/), 'layla@test.local')
    await userEvent.type(screen.getByLabelText(/^Password/), 'Str0ngPassword12')
    await userEvent.click(screen.getByRole('button', { name: 'Create centre' }))

    expect(await screen.findByRole('alert')).toHaveTextContent('The setup key is not correct.')
  })

  it('creates the centre and offers to sign in', async () => {
    let created = false
    routeFetch({
      'setup-status': () => envelope({ setup_required: !created, password_min_length: 12 }),
      '/bootstrap-super-admin': () => {
        created = true
        return envelope({}, { status: 201 })
      },
    })
    renderSetup()
    await screen.findByRole('button', { name: 'Create centre' })

    await userEvent.type(screen.getByLabelText(/Setup key/), 'right-key')
    await userEvent.type(screen.getByLabelText(/Centre name \(English\)/), 'Sunrise')
    await userEvent.type(screen.getByLabelText(/Centre name \(Arabic\)/), 'الشروق')
    await userEvent.type(screen.getByLabelText(/Centre code/), 'sunrise')
    await userEvent.type(screen.getByLabelText(/First name/), 'Layla')
    await userEvent.type(screen.getByLabelText(/Last name/), 'Hassan')
    await userEvent.type(screen.getByLabelText(/^Email/), 'layla@test.local')
    await userEvent.type(screen.getByLabelText(/^Password/), 'Str0ngPassword12')
    await userEvent.click(screen.getByRole('button', { name: 'Create centre' }))

    expect(await screen.findByText('Your centre is ready')).toBeInTheDocument()
    const body = JSON.parse(fetchMock.mock.calls.find(([u]) => String(u).includes('bootstrap'))![1].body)
    expect(body.tenant_name).toEqual({ en: 'Sunrise', ar: 'الشروق' })
    expect(body.tenant_slug).toBe('sunrise')
  })
})

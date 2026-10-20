import { screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { Route, Routes } from 'react-router-dom'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { AppShell } from '@/components/layout/AppShell'
import { LanguageSwitcher } from '@/components/layout/LanguageSwitcher'
import { StatusBadge } from '@/components/shared/StatusBadge'
import { Field, Input } from '@/components/ui/form-controls'
import { Toaster, toast } from '@/components/ui/toaster'
import { envelope, makeMe, renderWithProviders, setLanguage, signIn, signOut } from '@/test/utils'
import { useAuth } from '@/stores/auth'
import { act } from '@testing-library/react'

beforeEach(async () => {
  await setLanguage('en')
  signOut()
})

describe('LanguageSwitcher', () => {
  it('switches to Arabic and flips the page direction, then back', async () => {
    renderWithProviders(<LanguageSwitcher />)
    const arabic = screen.getByRole('button', { name: 'العربية' })
    const english = screen.getByRole('button', { name: 'English' })

    expect(english).toHaveAttribute('aria-pressed', 'true')
    await userEvent.click(arabic)

    expect(arabic).toHaveAttribute('aria-pressed', 'true')
    expect(document.documentElement.dir).toBe('rtl')

    await userEvent.click(english)
    expect(document.documentElement.dir).toBe('ltr')
  })
})

describe('StatusBadge', () => {
  it('shows the status in the active language', async () => {
    const { unmount } = renderWithProviders(<StatusBadge status="on_hold" labelKey="students.status" />)
    expect(screen.getByText('On hold')).toBeInTheDocument()
    unmount()

    await setLanguage('ar')
    renderWithProviders(<StatusBadge status="on_hold" labelKey="students.status" />)
    expect(screen.getByText('معلّق')).toBeInTheDocument()
  })
})

describe('Field', () => {
  it('links the error to the control and announces it', () => {
    renderWithProviders(
      <Field label="Name" htmlFor="name" error="Required" required>
        <Input id="name" aria-invalid aria-describedby="name-error" />
      </Field>,
    )

    expect(screen.getByRole('alert')).toHaveTextContent('Required')
    expect(screen.getByLabelText(/Name/)).toHaveAttribute('aria-describedby', 'name-error')
  })
})

describe('Toaster', () => {
  it('announces success messages politely', () => {
    renderWithProviders(<Toaster />)

    act(() => toast.success('Saved'))

    expect(screen.getByRole('status')).toHaveTextContent('Saved')
  })
})

describe('AppShell navigation', () => {
  const fetchMock = vi.fn()
  beforeEach(() => {
    fetchMock.mockResolvedValue(envelope(null))
    vi.stubGlobal('fetch', fetchMock)
  })
  afterEach(() => vi.unstubAllGlobals())

  function renderShell() {
    return renderWithProviders(
      <Routes>
        <Route element={<AppShell />}>
          <Route path="/dashboard" element={<p>PAGE</p>} />
        </Route>
        <Route path="/login" element={<p>LOGIN</p>} />
      </Routes>,
      { route: '/dashboard' },
    )
  }

  it('shows only what the role may use', () => {
    signIn(makeMe({ roles: ['teacher'], permissions: ['students.read', 'staff.read', 'centre_structure.read'] }))
    renderShell()
    const nav = screen.getByRole('navigation', { name: 'Main menu' })

    expect(nav).toHaveTextContent('Students')
    expect(nav).toHaveTextContent('Staff')
    expect(nav).toHaveTextContent('Rooms & programs')
    expect(nav).not.toHaveTextContent('User accounts')
  })

  it('calls the parent area "My children" and hides staff areas', () => {
    signIn(makeMe({ roles: ['parent'], permissions: ['students.read'] }))
    renderShell()
    const nav = screen.getByRole('navigation', { name: 'Main menu' })

    expect(nav).toHaveTextContent('My children')
    expect(nav).not.toHaveTextContent('Staff')
    expect(nav).not.toHaveTextContent('Rooms')
  })

  it('shows user management to those who hold the permission', () => {
    signIn(makeMe({ roles: ['super_admin'], permissions: ['students.read', 'staff.read', 'centre_structure.read', 'user_management.manage'] }))
    renderShell()

    expect(screen.getByRole('link', { name: 'User accounts' })).toBeInTheDocument()
  })

  it('translates and mirrors the navigation in Arabic', async () => {
    signIn(makeMe({ roles: ['centre_admin'] }))
    await setLanguage('ar')
    renderShell()

    expect(screen.getByRole('navigation', { name: 'القائمة الرئيسية' })).toHaveTextContent('الطلاب')
    expect(document.documentElement.dir).toBe('rtl')
  })

  it('has a skip link and shows the signed-in person and role', () => {
    signIn(makeMe({ first_name: 'Layla', last_name: 'Hassan', roles: ['centre_admin'] }))
    renderShell()

    expect(screen.getByRole('link', { name: 'Skip to main content' })).toHaveAttribute('href', '#main')
    expect(screen.getByText('Layla Hassan')).toBeInTheDocument()
    expect(screen.getByText('Centre administrator')).toBeInTheDocument()
  })

  it('signs out and returns to the login page', async () => {
    signIn()
    renderShell()

    await userEvent.click(screen.getByRole('button', { name: /Sign out/ }))

    expect(await screen.findByText('LOGIN')).toBeInTheDocument()
    expect(useAuth.getState().me).toBeNull()
  })
})

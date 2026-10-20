import { useQueryClient } from '@tanstack/react-query'
import { Baby, CalendarOff, LayoutDashboard, LogOut, Menu, ScanLine, School, ShieldCheck, Users, UsersRound, X } from 'lucide-react'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { NavLink, Outlet, useNavigate } from 'react-router-dom'
import { authApi } from '@/api/client'
import { Avatar } from '@/components/ui/surfaces'
import { Button } from '@/components/ui/button'
import { cn, initials, localized } from '@/lib/utils'
import { can, isParentOnly, useAuth } from '@/stores/auth'
import { useLiveUpdates } from '@/hooks/use-live-updates'
import { ShortageBanner } from '@/modules/attendance/ShortageBanner'
import { LanguageSwitcher } from './LanguageSwitcher'

interface NavItem {
  to: string
  label: string
  icon: typeof Users
  permission?: string
}

export function useNavItems(): NavItem[] {
  const { t } = useTranslation()
  const me = useAuth((s) => s.me)
  const parent = isParentOnly(me)
  const items: NavItem[] = [
    { to: '/dashboard', label: t('nav.dashboard'), icon: LayoutDashboard },
    { to: '/students', label: parent ? t('nav.myChildren') : t('nav.students'), icon: Baby, permission: 'students.read' },
    { to: '/staff', label: t('nav.staff'), icon: UsersRound, permission: 'staff.read' },
    { to: '/attendance', label: t('nav.attendance'), icon: ScanLine, permission: 'attendance.read' },
    { to: '/leave', label: t('nav.leave'), icon: CalendarOff, permission: 'attendance.read' },
    { to: '/rooms', label: t('nav.rooms'), icon: School, permission: 'centre_structure.read' },
    { to: '/users', label: t('nav.users'), icon: ShieldCheck, permission: 'user_management.manage' },
  ]
  return items.filter((item) => !item.permission || can(me, item.permission))
}

function Brand() {
  const { t } = useTranslation()
  return (
    <div className="flex items-center gap-3">
      <span className="flex h-10 w-10 items-center justify-center rounded-xl bg-slate-900" aria-hidden="true">
        <span className="flex h-5 w-5 items-center justify-center rounded-full border-2 border-amber">
          <span className="h-1.5 w-1.5 rounded-full bg-primary" />
        </span>
      </span>
      <div className="leading-tight">
        <p className="font-heading text-base font-bold">
          {t('common.appName')}
        </p>
        <p className="font-mono text-[10px] text-muted-foreground">{t('common.appTagline')}</p>
      </div>
    </div>
  )
}

export function AppShell() {
  const { t, i18n } = useTranslation()
  const me = useAuth((s) => s.me)
  const navigate = useNavigate()
  const queryClient = useQueryClient()
  const [open, setOpen] = useState(false)
  const items = useNavItems()
  useLiveUpdates()

  const signOut = async () => {
    await authApi.logout()
    queryClient.clear()
    navigate('/login', { replace: true })
  }

  const fullName = [me?.first_name, me?.last_name].filter(Boolean).join(' ') || me?.email || ''

  return (
    <div className="min-h-screen">
      <a
        href="#main"
        className="sr-only focus:not-sr-only focus:fixed focus:start-4 focus:top-4 focus:z-[70] focus:rounded-md focus:bg-white focus:px-3 focus:py-2 focus:shadow"
      >
        {t('common.skipToContent')}
      </a>

      {open && (
        <button
          type="button"
          aria-label={t('nav.closeMenu')}
          className="fixed inset-0 z-30 bg-slate-900/40 lg:hidden"
          onClick={() => setOpen(false)}
        />
      )}

      <aside
        className={cn(
          'fixed inset-y-0 start-0 z-40 flex w-64 flex-col border-e bg-white transition-transform lg:ltr:translate-x-0 lg:rtl:translate-x-0',
          open ? 'translate-x-0' : 'ltr:-translate-x-full rtl:translate-x-full',
        )}
      >
        <div className="flex items-center justify-between border-b px-4 py-4">
          <Brand />
          <button
            type="button"
            className="rounded-md p-1 text-muted-foreground hover:bg-secondary lg:hidden"
            aria-label={t('nav.closeMenu')}
            onClick={() => setOpen(false)}
          >
            <X className="h-5 w-5" />
          </button>
        </div>
        <nav aria-label={t('nav.main')} className="flex-1 space-y-1 overflow-y-auto p-3">
          {items.map((item) => (
            <NavLink
              key={item.to}
              to={item.to}
              onClick={() => setOpen(false)}
              className={({ isActive }) =>
                cn(
                  'flex items-center gap-3 rounded-md px-3 py-2.5 text-sm font-semibold transition-colors',
                  isActive ? 'bg-accent text-accent-foreground' : 'text-muted-foreground hover:bg-secondary hover:text-foreground',
                )
              }
            >
              <item.icon className="h-4 w-4 shrink-0" aria-hidden="true" />
              {item.label}
            </NavLink>
          ))}
        </nav>
        <div className="border-t p-3">
          <p className="truncate px-3 pb-2 font-mono text-[10px] text-muted-foreground">
            {localized(me?.tenant.name, i18n.language)}
          </p>
        </div>
      </aside>

      <div className="lg:ps-64">
        <header className="sticky top-0 z-20 flex items-center justify-between gap-3 border-b bg-white/90 px-4 py-3 backdrop-blur lg:px-8">
          <button
            type="button"
            className="rounded-md p-2 text-muted-foreground hover:bg-secondary lg:hidden"
            aria-label={t('nav.openMenu')}
            onClick={() => setOpen(true)}
          >
            <Menu className="h-5 w-5" />
          </button>
          <div className="hidden lg:block" />
          <div className="flex items-center gap-3">
            <LanguageSwitcher />
            <div className="hidden items-center gap-2 sm:flex">
              <Avatar text={initials(me?.first_name, me?.last_name)} />
              <div className="leading-tight">
                <p className="text-sm font-bold">{fullName}</p>
                <p className="text-[11px] text-muted-foreground">
                  {me?.roles.map((role) => t(`roles.${role}`)).join(', ')}
                </p>
              </div>
            </div>
            <Button variant="ghost" size="sm" onClick={() => void signOut()}>
              <LogOut className="h-4 w-4 rtl:rotate-180" aria-hidden="true" />
              <span className="hidden sm:inline">{t('common.signOut')}</span>
              <span className="sr-only sm:hidden">{t('common.signOut')}</span>
            </Button>
          </div>
        </header>
        <ShortageBanner />
        <main id="main" tabIndex={-1} className="mx-auto max-w-6xl px-4 py-6 focus:outline-none lg:px-8 lg:py-8">
          <Outlet />
        </main>
      </div>
    </div>
  )
}

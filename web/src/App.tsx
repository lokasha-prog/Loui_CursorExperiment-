import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { Suspense, lazy, useEffect } from 'react'
import { useTranslation } from 'react-i18next'
import { BrowserRouter, Link, Navigate, Route, Routes, useLocation } from 'react-router-dom'
import { ApiError, refreshSession } from '@/api/client'
import { AppShell } from '@/components/layout/AppShell'
import { Button } from '@/components/ui/button'
import { Skeleton } from '@/components/ui/surfaces'
import { Toaster } from '@/components/ui/toaster'
import { can, useAuth } from '@/stores/auth'

const LoginPage = lazy(() => import('@/modules/auth/LoginPage'))
const SetupPage = lazy(() => import('@/modules/auth/SetupPage'))
const DashboardPage = lazy(() => import('@/modules/dashboard/DashboardPage'))
const StudentsPage = lazy(() => import('@/modules/students/StudentsPage'))
const StudentDetailPage = lazy(() => import('@/modules/students/StudentDetailPage'))
const StaffPage = lazy(() => import('@/modules/staff/StaffPage'))
const RoomsPage = lazy(() => import('@/modules/rooms/RoomsPage'))
const UsersPage = lazy(() => import('@/modules/users/UsersPage'))

export function createQueryClient() {
  return new QueryClient({
    defaultOptions: {
      queries: {
        staleTime: 30_000,
        refetchOnWindowFocus: false,
        // Permission and not-found answers will not change by asking again.
        retry: (count, error) => !(error instanceof ApiError && [401, 403, 404].includes(error.status)) && count < 2,
      },
    },
  })
}

function PageLoader() {
  const { t } = useTranslation()
  return (
    <div role="status" aria-label={t('common.loading')} className="space-y-3 p-8">
      <Skeleton className="h-8 w-48" />
      <Skeleton className="h-64 w-full" />
    </div>
  )
}

/** Sends signed-out visitors to the login page and remembers where they were going. */
function RequireAuth() {
  const { me, ready } = useAuth()
  const location = useLocation()
  if (!ready) return <PageLoader />
  if (!me) return <Navigate to="/login" replace state={{ from: location.pathname + location.search }} />
  return <AppShell />
}

function RequirePermission({ permission, children }: { permission: string; children: React.ReactNode }) {
  const me = useAuth((s) => s.me)
  return can(me, permission) ? <>{children}</> : <Navigate to="/dashboard" replace />
}

function NotFound() {
  const { t } = useTranslation()
  return (
    <div className="py-20 text-center">
      <h1 className="text-3xl font-bold">{t('notFound.title')}</h1>
      <p className="mb-6 mt-2 text-muted-foreground">{t('notFound.body')}</p>
      <Button asChild>
        <Link to="/dashboard">{t('notFound.home')}</Link>
      </Button>
    </div>
  )
}

export function AppRoutes() {
  return (
    <Suspense fallback={<PageLoader />}>
      <Routes>
        <Route path="/login" element={<LoginPage />} />
        <Route path="/setup" element={<SetupPage />} />
        <Route element={<RequireAuth />}>
          <Route index element={<Navigate to="/dashboard" replace />} />
          <Route path="/dashboard" element={<DashboardPage />} />
          <Route path="/students" element={<RequirePermission permission="students.read"><StudentsPage /></RequirePermission>} />
          <Route path="/students/:id" element={<RequirePermission permission="students.read"><StudentDetailPage /></RequirePermission>} />
          <Route path="/staff" element={<RequirePermission permission="staff.read"><StaffPage /></RequirePermission>} />
          <Route path="/rooms" element={<RequirePermission permission="centre_structure.read"><RoomsPage /></RequirePermission>} />
          <Route path="/users" element={<RequirePermission permission="user_management.manage"><UsersPage /></RequirePermission>} />
          <Route path="*" element={<NotFound />} />
        </Route>
      </Routes>
    </Suspense>
  )
}

const queryClient = createQueryClient()

export default function App() {
  const setReady = useAuth((s) => s.setReady)

  // Try to continue an earlier session using the HttpOnly refresh cookie before deciding what to show.
  useEffect(() => {
    void refreshSession().finally(setReady)
  }, [setReady])

  return (
    <QueryClientProvider client={queryClient}>
      <BrowserRouter>
        <AppRoutes />
      </BrowserRouter>
      <Toaster />
    </QueryClientProvider>
  )
}

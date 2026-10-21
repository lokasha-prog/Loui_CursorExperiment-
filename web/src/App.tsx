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
const LearningPage = lazy(() => import('@/modules/learning/LearningPage'))
const UsersPage = lazy(() => import('@/modules/users/UsersPage'))
const AttendancePage = lazy(() => import('@/modules/attendance/AttendancePage'))
const ReportsPage = lazy(() => import('@/modules/attendance/ReportsPage'))
const FeedPage = lazy(() => import('@/modules/feed/FeedPage'))
const MessagesPage = lazy(() => import('@/modules/messages/MessagesPage'))
const NotificationsPage = lazy(() => import('@/modules/notifications/NotificationsPage'))
const AuditPage = lazy(() => import('@/modules/audit/AuditPage'))
const FeedbackPage = lazy(() => import('@/modules/feedback/FeedbackPage'))
const BillingPage = lazy(() => import('@/modules/billing/BillingPage'))
const LeavesPage = lazy(() => import('@/modules/attendance/LeavesPage'))
const NewslettersPage = lazy(() => import('@/modules/newsletters/NewslettersPage'))
const ResourcesPage = lazy(() => import('@/modules/resources/ResourcesPage'))
const FormsPage = lazy(() => import('@/modules/forms/FormsPage'))
const PublicFormPage = lazy(() => import('@/modules/public/PublicFormPage'))
const CalendarPage = lazy(() => import('@/modules/calendar/CalendarPage'))
const ToolsPage = lazy(() => import('@/modules/tools/ToolsPage'))
const SettingsPage = lazy(() => import('@/modules/settings/SettingsPage'))
const EnquiriesPage = lazy(() => import('@/modules/enquiries/EnquiriesPage'))
const EnquiryFormPage = lazy(() => import('@/modules/public/EnquiryFormPage'))

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
        <Route path="/enquire/:centre" element={<EnquiryFormPage />} />
        <Route path="/forms/public/:centre/:id" element={<PublicFormPage />} />
        <Route element={<RequireAuth />}>
          <Route index element={<Navigate to="/dashboard" replace />} />
          <Route path="/dashboard" element={<DashboardPage />} />
          <Route path="/students" element={<RequirePermission permission="students.read"><StudentsPage /></RequirePermission>} />
          <Route path="/students/:id" element={<RequirePermission permission="students.read"><StudentDetailPage /></RequirePermission>} />
          <Route path="/staff" element={<RequirePermission permission="staff.read"><StaffPage /></RequirePermission>} />
          <Route path="/attendance" element={<RequirePermission permission="attendance.read"><AttendancePage /></RequirePermission>} />
          <Route path="/reports" element={<RequirePermission permission="reports.view"><ReportsPage /></RequirePermission>} />
          <Route path="/feed" element={<RequirePermission permission="activities.read"><FeedPage /></RequirePermission>} />
          <Route path="/messages" element={<RequirePermission permission="messaging.read"><MessagesPage /></RequirePermission>} />
          <Route path="/notifications" element={<NotificationsPage />} />
          <Route path="/audit" element={<RequirePermission permission="audit_log.view"><AuditPage /></RequirePermission>} />
          <Route path="/feedback" element={<RequirePermission permission="settings.read"><FeedbackPage /></RequirePermission>} />
          <Route path="/billing" element={<RequirePermission permission="billing.read"><BillingPage /></RequirePermission>} />
          <Route path="/enquiries" element={<RequirePermission permission="enquiries.read"><EnquiriesPage /></RequirePermission>} />
          <Route path="/settings" element={<RequirePermission permission="settings.read"><SettingsPage /></RequirePermission>} />
          <Route path="/calendar" element={<RequirePermission permission="calendar.read"><CalendarPage /></RequirePermission>} />
          <Route path="/forms" element={<RequirePermission permission="forms.read"><FormsPage /></RequirePermission>} />
          <Route path="/newsletters" element={<RequirePermission permission="messaging.read"><NewslettersPage /></RequirePermission>} />
          <Route path="/learning" element={<RequirePermission permission="learning.read"><LearningPage /></RequirePermission>} />
          <Route path="/resources" element={<RequirePermission permission="resources.read"><ResourcesPage /></RequirePermission>} />
          <Route path="/tools" element={<RequirePermission permission="tools.use"><ToolsPage /></RequirePermission>} />
          <Route path="/leave" element={<RequirePermission permission="attendance.read"><LeavesPage /></RequirePermission>} />
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

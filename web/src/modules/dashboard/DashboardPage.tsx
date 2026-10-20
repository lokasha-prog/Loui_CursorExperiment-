import { AlertTriangle, Baby, CalendarOff, DoorOpen, LogIn, Plus, School, Sparkles, UserCheck, UserX, UsersRound } from 'lucide-react'
import type { ReactNode } from 'react'
import { useTranslation } from 'react-i18next'
import { Link } from 'react-router-dom'
import { useDashboardSummary, useRooms, useStaff, useStudents } from '@/api/hooks'
import { PageHeader } from '@/components/layout/PageHeader'
import { Card, Skeleton } from '@/components/ui/surfaces'
import { RatioCards } from '@/modules/attendance/RatioCards'
import { can, isParentOnly, useAuth } from '@/stores/auth'

function Count({ pages, loading }: { pages?: { items: unknown[]; pagination: { has_more: boolean } }[]; loading: boolean }) {
  const { t } = useTranslation()
  if (loading) return <Skeleton className="h-9 w-16" />
  const total = pages?.reduce((sum, page) => sum + page.items.length, 0) ?? 0
  const more = pages?.[pages.length - 1]?.pagination.has_more
  return <span>{more ? t('dashboard.orMore', { count: total }) : total}</span>
}

function Kpi({ icon, label, to, children }: { icon: ReactNode; label: string; to: string; children: ReactNode }) {
  return (
    <Link to={to} className="nursery-card block p-5 transition-all hover:-translate-y-0.5 hover:border-primary/40 hover:shadow-md focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring">
      <div className="mb-3 flex h-10 w-10 items-center justify-center rounded-xl bg-amber/30 text-amber-800">{icon}</div>
      <p className="font-heading text-3xl font-bold">{children}</p>
      <p className="text-xs font-bold uppercase tracking-wide text-muted-foreground">{label}</p>
    </Link>
  )
}

export default function DashboardPage() {
  const { t } = useTranslation()
  const me = useAuth((s) => s.me)
  const parent = isParentOnly(me)
  const students = useStudents({})
  const staff = useStaff({})
  const rooms = useRooms(can(me, 'centre_structure.read'))
  const canSeeAttendance = can(me, 'attendance.read')
  const canSeeCover = can(me, 'attendance.write')
  const summary = useDashboardSummary(canSeeAttendance)
  const name = me?.first_name ?? me?.email ?? ''

  const actions = [
    { to: '/students?new=1', label: t('dashboard.addStudent'), icon: Baby, permission: 'students.write' },
    { to: '/staff', label: t('dashboard.addStaff'), icon: UsersRound, permission: 'staff.write' },
    { to: '/rooms', label: t('dashboard.addRoom'), icon: DoorOpen, permission: 'centre_structure.write' },
  ].filter((a) => can(me, a.permission))

  return (
    <>
      <PageHeader title={t('dashboard.welcome', { name })} subtitle={parent ? t('dashboard.parentSubtitle') : t('dashboard.subtitle')} />

      <div className="mb-8 grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
        {can(me, 'students.read') && (
          <Kpi icon={<Baby className="h-5 w-5" aria-hidden="true" />} label={parent ? t('nav.myChildren') : t('dashboard.students')} to="/students">
            <Count pages={students.data?.pages} loading={students.isLoading} />
          </Kpi>
        )}
        {can(me, 'staff.read') && (
          <Kpi icon={<UsersRound className="h-5 w-5" aria-hidden="true" />} label={t('dashboard.staff')} to="/staff">
            <Count pages={staff.data?.pages} loading={staff.isLoading} />
          </Kpi>
        )}
        {can(me, 'centre_structure.read') && (
          <Kpi icon={<School className="h-5 w-5" aria-hidden="true" />} label={t('dashboard.rooms')} to="/rooms">
            <Count pages={rooms.data?.pages} loading={rooms.isLoading} />
          </Kpi>
        )}
      </div>

      {canSeeAttendance && (
        <section className="mb-8" aria-labelledby="today-heading">
          <h2 id="today-heading" className="mb-3 font-heading text-lg font-semibold">{t('dashboard.attendanceToday')}</h2>
          <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
            <Kpi icon={<UserCheck className="h-5 w-5" aria-hidden="true" />} label={t('dashboard.present')} to="/attendance">
              {summary.isLoading ? <Skeleton className="h-9 w-16" /> : (summary.data?.students_present ?? 0)}
            </Kpi>
            <Kpi icon={<LogIn className="h-5 w-5 rtl:rotate-180" aria-hidden="true" />} label={t('dashboard.notYetArrived')} to="/attendance">
              {summary.isLoading ? <Skeleton className="h-9 w-16" /> : (summary.data?.students_not_yet_arrived ?? 0)}
            </Kpi>
            <Kpi icon={<UserX className="h-5 w-5" aria-hidden="true" />} label={t('dashboard.absent')} to="/attendance">
              {summary.isLoading ? <Skeleton className="h-9 w-16" /> : (summary.data?.students_absent ?? 0)}
            </Kpi>
            <Kpi icon={<CalendarOff className="h-5 w-5" aria-hidden="true" />} label={t('dashboard.onLeave')} to="/leave">
              {summary.isLoading ? <Skeleton className="h-9 w-16" /> : (summary.data?.students_on_leave ?? 0)}
            </Kpi>
            {canSeeCover && (
              <>
                <Kpi icon={<UsersRound className="h-5 w-5" aria-hidden="true" />} label={t('dashboard.staffHere')} to="/attendance">
                  {summary.isLoading ? <Skeleton className="h-9 w-16" /> : `${summary.data?.staff_present ?? 0} / ${summary.data?.active_staff ?? 0}`}
                </Kpi>
                <Kpi icon={<AlertTriangle className="h-5 w-5" aria-hidden="true" />} label={t('dashboard.roomsShort')} to="/attendance">
                  {summary.isLoading ? <Skeleton className="h-9 w-16" /> : (summary.data?.rooms_in_breach ?? 0)}
                </Kpi>
              </>
            )}
            {can(me, 'attendance.override') && (
              <Kpi icon={<CalendarOff className="h-5 w-5" aria-hidden="true" />} label={t('dashboard.pendingLeave')} to="/leave">
                {summary.isLoading ? <Skeleton className="h-9 w-16" /> : (summary.data?.pending_leave_requests ?? 0)}
              </Kpi>
            )}
          </div>
        </section>
      )}

      {canSeeCover && (
        <section className="mb-8" aria-labelledby="cover-heading">
          <h2 id="cover-heading" className="mb-3 font-heading text-lg font-semibold">{t('dashboard.liveCover')}</h2>
          <RatioCards />
        </section>
      )}

      <div className="grid gap-6 lg:grid-cols-2">
        {actions.length > 0 && (
          <Card className="p-5">
            <h2 className="mb-4 font-heading text-lg font-semibold">{t('dashboard.quickActions')}</h2>
            <ul className="space-y-2">
              {actions.map((action) => (
                <li key={action.to}>
                  <Link to={action.to} className="flex items-center gap-3 rounded-md border px-3 py-2.5 text-sm font-semibold transition-colors hover:border-primary/40 hover:bg-accent focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring">
                    <action.icon className="h-4 w-4 text-primary-dark" aria-hidden="true" />
                    {action.label}
                    <Plus className="ms-auto h-4 w-4 text-muted-foreground" aria-hidden="true" />
                  </Link>
                </li>
              ))}
            </ul>
          </Card>
        )}
        <Card className="flex items-start gap-4 bg-gradient-to-br from-accent to-white p-5">
          <span className="flex h-10 w-10 shrink-0 items-center justify-center rounded-xl bg-white shadow-sm">
            <Sparkles className="h-5 w-5 text-primary-dark" aria-hidden="true" />
          </span>
          <div>
            <h2 className="font-heading text-lg font-semibold">{t('dashboard.comingTitle')}</h2>
            <p className="mt-1 text-sm text-muted-foreground">{t('dashboard.comingBody')}</p>
          </div>
        </Card>
      </div>
    </>
  )
}

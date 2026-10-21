import { AlertTriangle, Baby, ClipboardCheck, GraduationCap, Inbox, Newspaper, Wallet } from 'lucide-react'
import type { ReactNode } from 'react'
import { useTranslation } from 'react-i18next'
import { Link } from 'react-router-dom'
import { useSystemOverview } from '@/api/hooks'
import { Card, Skeleton } from '@/components/ui/surfaces'
import { cn } from '@/lib/utils'

function Block({ title, icon, to, tone, children }: { title: string; icon: ReactNode; to: string; tone: string; children: ReactNode }) {
  return (
    <Link to={to} className="nursery-card block p-5 transition-all hover:-translate-y-0.5 hover:border-primary/40 hover:shadow-md focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring">
      <div className="mb-3 flex items-center gap-3">
        <span className={cn('flex h-10 w-10 items-center justify-center rounded-xl', tone)}>{icon}</span>
        <h3 className="font-heading text-base font-semibold">{title}</h3>
      </div>
      <dl className="grid grid-cols-2 gap-x-4 gap-y-2">{children}</dl>
    </Link>
  )
}

function Stat({ label, value, alert }: { label: string; value: ReactNode; alert?: boolean }) {
  return (
    <div>
      <dd className={cn('font-heading text-2xl font-bold', alert && 'text-red-700')}>{value}</dd>
      <dt className="text-xs font-semibold text-muted-foreground">{label}</dt>
    </div>
  )
}

const money = (fils: number, lang: string) => new Intl.NumberFormat(lang === 'ar' ? 'ar-AE-u-nu-latn' : 'en-AE', { maximumFractionDigits: 0 }).format(fils / 100)

/** One collective picture of every part of the system. Each block shows only for people whose role may see it. */
export function SystemOverviewSection() {
  const { t, i18n } = useTranslation()
  const lang = i18n.language
  const overview = useSystemOverview()
  const d = overview.data

  if (overview.isLoading) return <Skeleton className="mb-8 h-40" />
  if (!d) return null
  const none = !d.people && !d.billing && !d.admissions && !d.learning && !d.family && !d.communication
  if (none) return null

  return (
    <section className="mb-8" aria-labelledby="overview-heading">
      <h2 id="overview-heading" className="font-heading text-lg font-semibold">{t('dashboard.overview.title')}</h2>
      <p className="mb-3 text-sm text-muted-foreground">{t('dashboard.overview.subtitle')}</p>
      <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-3">
        {d.people && (
          <Block title={t('dashboard.overview.people')} icon={<Baby className="h-5 w-5" aria-hidden="true" />} to="/students" tone="bg-amber/30 text-amber-800">
            <Stat label={t('dashboard.overview.students')} value={d.people.students} />
            <Stat label={t('dashboard.overview.staff')} value={d.people.staff} />
            <Stat label={t('dashboard.overview.present')} value={d.people.present_today} />
            <Stat label={t('dashboard.overview.short')} value={d.people.rooms_short} alert={d.people.rooms_short > 0} />
          </Block>
        )}
        {d.billing && (
          <Block title={t('dashboard.overview.billing')} icon={<Wallet className="h-5 w-5" aria-hidden="true" />} to="/billing" tone="bg-emerald-100 text-emerald-800">
            <Stat label={`${t('dashboard.overview.billed')} (${t('dashboard.overview.currency')})`} value={money(d.billing.billed_fils, lang)} />
            <Stat label={`${t('dashboard.overview.collected')} (${t('dashboard.overview.currency')})`} value={money(d.billing.collected_fils, lang)} />
            <Stat label={`${t('dashboard.overview.overdue')} (${t('dashboard.overview.currency')})`} value={money(d.billing.overdue_fils, lang)} alert={d.billing.overdue_fils > 0} />
            <Stat label={`${t('dashboard.overview.expenses')} (${t('dashboard.overview.currency')})`} value={money(d.billing.expenses_fils, lang)} />
          </Block>
        )}
        {d.admissions && (
          <Block title={t('dashboard.overview.admissions')} icon={<Inbox className="h-5 w-5" aria-hidden="true" />} to="/enquiries" tone="bg-sky-100 text-sky-800">
            <Stat label={t('dashboard.overview.open')} value={d.admissions.open_enquiries} />
            <Stat label={t('dashboard.overview.fresh')} value={d.admissions.new_last30_days} />
            <Stat label={t('dashboard.overview.tours')} value={d.admissions.tours_next7_days} />
            <Stat label={t('dashboard.overview.stale')} value={d.admissions.stale_over7_days} alert={d.admissions.stale_over7_days > 0} />
          </Block>
        )}
        {d.learning && (
          <Block title={t('dashboard.overview.learning')} icon={<GraduationCap className="h-5 w-5" aria-hidden="true" />} to="/learning" tone="bg-violet-100 text-violet-800">
            <Stat label={t('dashboard.overview.lessons')} value={d.learning.lessons} />
            <Stat label={t('dashboard.overview.obs')} value={d.learning.observations_last30_days} />
            <Stat label={t('dashboard.overview.concerns')} value={d.learning.concerns_last30_days} alert={d.learning.concerns_last30_days > 0} />
            <Stat label={t('dashboard.overview.toApprove')} value={d.learning.assessments_to_approve} alert={d.learning.assessments_to_approve > 0} />
            <Stat label={t('dashboard.overview.shared')} value={d.learning.reports_shared} />
            <Stat label={t('dashboard.overview.unobserved')} value={d.learning.children_without_observation30_days} />
            <Stat label={t('dashboard.overview.teachersActive')} value={`${d.learning.teachers_recording_this_week} / ${d.learning.teachers}`} />
          </Block>
        )}
        {d.family && (
          <Block title={t('dashboard.overview.family')} icon={<ClipboardCheck className="h-5 w-5" aria-hidden="true" />} to="/journey" tone="bg-violet-100 text-violet-800">
            <Stat label={t('dashboard.overview.children')} value={d.family.children} />
            <Stat label={t('dashboard.overview.sharedObs')} value={d.family.shared_observations} />
            <Stat label={t('dashboard.overview.reports')} value={d.family.reports_ready} />
          </Block>
        )}
        {d.communication && (
          <Block title={t('dashboard.overview.communication')} icon={<Newspaper className="h-5 w-5" aria-hidden="true" />} to="/newsletters" tone="bg-rose-100 text-rose-800">
            <Stat label={t('dashboard.overview.posts')} value={d.communication.posts_to_approve} alert={d.communication.posts_to_approve > 0} />
            <Stat label={t('dashboard.overview.news')} value={d.communication.newsletters_last30_days} />
            <Stat label={t('dashboard.overview.forms')} value={d.communication.live_forms} />
            <Stat label={t('dashboard.overview.docs')} value={d.communication.documents} />
          </Block>
        )}
      </div>
      {d.admissions && d.admissions.admission_steps_overdue > 0 && (
        <Card className="mt-4 flex items-center gap-3 border-amber-300 bg-amber-50 p-3 text-sm font-semibold text-amber-900">
          <AlertTriangle className="h-4 w-4" aria-hidden="true" />
          {t('dashboard.overview.steps')}: {d.admissions.admission_steps_overdue}
        </Card>
      )}
    </section>
  )
}

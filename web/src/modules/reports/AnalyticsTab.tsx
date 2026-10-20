import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { useAnalytics } from '@/api/hooks'
import { ListError } from '@/components/shared/ListState'
import { Field, Input } from '@/components/ui/form-controls'
import { Skeleton } from '@/components/ui/surfaces'
import { formatAed, todayIso } from '@/lib/format'
import { BarChart, ChartCard, DonutChart, LineChart } from './charts'

const daysAgo = (n: number) => {
  const d = new Date()
  d.setDate(d.getDate() - n)
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`
}

/** Trends over a chosen period: attendance, money (only for those allowed to see it), and enquiries. Each chart can be saved as a picture. */
export function AnalyticsTab() {
  const { t, i18n } = useTranslation()
  const [from, setFrom] = useState(daysAgo(89))
  const [to, setTo] = useState(todayIso())
  const analytics = useAnalytics(from, to)
  const a = analytics.data
  const status = (key: string) => t(`attendance.status.${key}`, { defaultValue: key })

  return (
    <div className="space-y-5">
      <div className="flex flex-wrap items-end gap-3">
        <Field label={t('reports.from')} htmlFor="an-from"><Input id="an-from" type="date" dir="ltr" className="text-start" value={from} max={to} onChange={(e) => setFrom(e.target.value)} /></Field>
        <Field label={t('reports.centre.to')} htmlFor="an-to"><Input id="an-to" type="date" dir="ltr" className="text-start" value={to} min={from} onChange={(e) => setTo(e.target.value)} /></Field>
      </div>
      {analytics.isLoading ? <Skeleton className="h-64" /> : analytics.isError || !a ? <ListError error={analytics.error} onRetry={() => void analytics.refetch()} /> : (
        <div className="grid gap-4 lg:grid-cols-2">
          <ChartCard title={t('reports.analytics.attendanceRate')} empty={a.attendance_rate.length === 0} fileName="attendance-rate">
            {(ref) => <LineChart svgRef={ref} points={a.attendance_rate} label={t('reports.analytics.attendanceRate')} format={(v) => `${Math.round(v)}%`} />}
          </ChartCard>
          <ChartCard title={t('reports.analytics.attendanceStatus')} empty={a.attendance_by_status.length === 0} fileName="attendance-status">
            {(ref) => <DonutChart svgRef={ref} label={t('reports.analytics.attendanceStatus')} slices={a.attendance_by_status.map((s) => ({ label: status(s.label), value: s.count }))} />}
          </ChartCard>
          {a.revenue && (
            <ChartCard title={t('reports.analytics.revenue')} empty={a.revenue.every((m) => m.billed_fils === 0 && m.collected_fils === 0 && m.expenses_fils === 0)} fileName="revenue">
              {(ref) => (
                <BarChart
                  svgRef={ref}
                  label={t('reports.analytics.revenue')}
                  categories={a.revenue!.map((m) => m.month)}
                  series={[
                    { name: t('billing.kpi.billed'), values: a.revenue!.map((m) => m.billed_fils / 100) },
                    { name: t('billing.kpi.collected'), values: a.revenue!.map((m) => m.collected_fils / 100) },
                    { name: t('billing.kpi.expenses'), values: a.revenue!.map((m) => m.expenses_fils / 100) },
                  ]}
                  format={(v) => formatAed(Math.round(v * 100), i18n.language).replace(/\.00$/, '')}
                />
              )}
            </ChartCard>
          )}
          {a.invoice_status && (
            <ChartCard title={t('reports.analytics.invoiceStatus')} empty={a.invoice_status.length === 0} fileName="invoice-status">
              {(ref) => <DonutChart svgRef={ref} label={t('reports.analytics.invoiceStatus')} slices={a.invoice_status!.map((s) => ({ label: t(`billing.status.${s.label}`, { defaultValue: s.label }), value: s.count }))} />}
            </ChartCard>
          )}
          {a.enquiries_by_month && a.enrolled_by_month && (
            <ChartCard title={t('reports.analytics.enquiries')} empty={a.enquiries_by_month.every((m) => m.count === 0)} fileName="enquiries">
              {(ref) => (
                <BarChart
                  svgRef={ref}
                  label={t('reports.analytics.enquiries')}
                  categories={a.enquiries_by_month!.map((m) => m.label)}
                  series={[{ name: t('reports.analytics.newEnquiries'), values: a.enquiries_by_month!.map((m) => m.count) }, { name: t('enquiries.stages.enrolled'), values: a.enrolled_by_month!.map((m) => m.count) }]}
                />
              )}
            </ChartCard>
          )}
          {a.enquiries_by_source && (
            <ChartCard title={t('reports.analytics.sources')} empty={a.enquiries_by_source.length === 0} fileName="enquiry-sources">
              {(ref) => <DonutChart svgRef={ref} label={t('reports.analytics.sources')} slices={a.enquiries_by_source!.map((s) => ({ label: t(`enquiries.sources.${s.label}`, { defaultValue: s.label }), value: s.count }))} />}
            </ChartCard>
          )}
        </div>
      )}
    </div>
  )
}

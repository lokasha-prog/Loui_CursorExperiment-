import { Wallet } from 'lucide-react'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { useOverdueReport, useRevenueReport } from '@/api/hooks'
import { ListError } from '@/components/shared/ListState'
import { Field, Input } from '@/components/ui/form-controls'
import { Card, EmptyState, Skeleton, Table, Td, Th } from '@/components/ui/surfaces'
import { formatAed, formatDate, todayIso } from '@/lib/format'
import { cn } from '@/lib/utils'

function Figure({ label, value, tone }: { label: string; value: string; tone?: 'danger' | 'success' }) {
  return (
    <Card className="p-4">
      <p className={cn('font-heading text-xl font-bold', tone === 'danger' && 'text-red-700', tone === 'success' && 'text-emerald-700')} dir="ltr">{value}</p>
      <p className="text-[11px] font-bold uppercase tracking-wide text-muted-foreground">{label}</p>
    </Card>
  )
}

/** Monthly revenue by payment method with the net after expenses, and the list of who is late and by how long. */
export function ReportsTab() {
  const { t, i18n } = useTranslation()
  const [month, setMonth] = useState(todayIso().slice(0, 7))
  const revenue = useRevenueReport(month)
  const overdue = useOverdueReport()
  const money = (v: number) => formatAed(v, i18n.language)

  return (
    <div className="space-y-8">
      <section aria-labelledby="rep-revenue" className="space-y-4">
        <div className="flex flex-wrap items-end gap-3">
          <h2 id="rep-revenue" className="font-heading text-lg font-semibold">{t('billing.reports.revenue')}</h2>
          <Field label={t('billing.expenses.month')} htmlFor="rep-month" className="ms-auto"><Input id="rep-month" type="month" dir="ltr" className="text-start" value={month} onChange={(e) => setMonth(e.target.value)} /></Field>
        </div>
        {revenue.isLoading ? <Skeleton className="h-32" /> : revenue.isError || !revenue.data ? <ListError error={revenue.error} onRetry={() => void revenue.refetch()} /> : (
          <>
            <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-4">
              <Figure label={t('billing.kpi.billed')} value={money(revenue.data.billed_fils)} />
              <Figure label={t('billing.kpi.collected')} value={money(revenue.data.collected_fils)} tone="success" />
              <Figure label={t('billing.kpi.expenses')} value={money(revenue.data.expenses_fils)} />
              <Figure label={t('billing.kpi.net')} value={money(revenue.data.net_fils)} tone={revenue.data.net_fils < 0 ? 'danger' : 'success'} />
            </div>
            <Card className="p-4">
              <h3 className="mb-2 text-sm font-bold">{t('billing.reports.byMethod')}</h3>
              {revenue.data.by_method.length === 0 ? <p className="text-sm text-muted-foreground">{t('billing.none')}</p> : (
                <ul className="divide-y text-sm">
                  {revenue.data.by_method.map((m) => (
                    <li key={m.method} className="flex items-center justify-between gap-2 py-2">
                      <span>{t(`billing.payment.methods.${m.method}`)} <span className="text-xs text-muted-foreground">({m.count})</span></span>
                      <span className="font-semibold" dir="ltr">{money(m.total_fils)}</span>
                    </li>
                  ))}
                </ul>
              )}
            </Card>
          </>
        )}
      </section>

      <section aria-labelledby="rep-overdue" className="space-y-4">
        <div className="flex items-baseline justify-between gap-3">
          <h2 id="rep-overdue" className="font-heading text-lg font-semibold">{t('billing.reports.overdue')}</h2>
          {overdue.data && <span className="font-semibold text-red-700" dir="ltr">{money(overdue.data.total_fils)}</span>}
        </div>
        <Card className="overflow-hidden">
          {overdue.isLoading ? <Skeleton className="h-24" /> : overdue.isError || !overdue.data ? <ListError error={overdue.error} onRetry={() => void overdue.refetch()} /> :
            overdue.data.rows.length === 0 ? <EmptyState icon={<Wallet className="h-7 w-7" aria-hidden="true" />} title={t('billing.reports.noneOverdue')} description={t('billing.reports.noneOverdueBody')} /> : (
              <Table>
                <caption className="sr-only">{t('billing.reports.overdue')}</caption>
                <thead><tr><Th>{t('billing.columns.number')}</Th><Th>{t('billing.columns.child')}</Th><Th>{t('billing.reports.contact')}</Th><Th>{t('billing.columns.due')}</Th><Th>{t('billing.reports.daysLate')}</Th><Th>{t('billing.reports.owed')}</Th></tr></thead>
                <tbody>
                  {overdue.data.rows.map((r) => (
                    <tr key={r.invoice_id}>
                      <Td className="font-mono text-xs" dir="ltr">{r.invoice_number}</Td>
                      <Td className="font-semibold">{r.student_name}</Td>
                      <Td dir="ltr" className="text-start">{r.contact ?? '—'}</Td>
                      <Td>{formatDate(r.due_date, i18n.language)}</Td>
                      <Td>{r.days_late}</Td>
                      <Td dir="ltr" className="text-start font-semibold text-red-700">{money(r.outstanding_fils)}</Td>
                    </tr>
                  ))}
                </tbody>
              </Table>
            )}
        </Card>
      </section>
    </div>
  )
}

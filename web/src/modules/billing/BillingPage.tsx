import * as TabsPrimitive from '@radix-ui/react-tabs'
import { Plus, ReceiptText, Wallet } from 'lucide-react'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { flatten, useBillingSnapshot, useFeePlans, useGenerateInvoices, useInvoices } from '@/api/hooks'
import { PageHeader } from '@/components/layout/PageHeader'
import { ListError, ListSkeleton, LoadMore } from '@/components/shared/ListState'
import { Button } from '@/components/ui/button'
import { Badge, Card, EmptyState, Skeleton, Table, Td, Th } from '@/components/ui/surfaces'
import { toast } from '@/components/ui/toaster'
import { useApiErrorMessage } from '@/hooks/use-api-error'
import { formatAed, formatDate } from '@/lib/format'
import { cn, localized } from '@/lib/utils'
import { can, isParentOnly, useAuth } from '@/stores/auth'
import { ExpensesTab } from './ExpensesTab'
import { FeePlanDialog } from './FeePlanDialog'
import { InvoiceDialog, statusTone } from './InvoiceDialog'
import { NewInvoiceDialog } from './NewInvoiceDialog'
import { ReportsTab } from './ReportsTab'
import { SettingsTab } from './SettingsTab'

const STATUSES = ['', 'issued', 'partially_paid', 'overdue', 'paid', 'draft', 'cancelled']

function Kpi({ label, value, tone }: { label: string; value: string; tone?: 'danger' | 'success' }) {
  return (
    <Card className="p-4">
      <p className={cn('font-heading text-2xl font-bold', tone === 'danger' && 'text-red-700', tone === 'success' && 'text-emerald-700')} dir="ltr">{value}</p>
      <p className="text-[11px] font-bold uppercase tracking-wide text-muted-foreground">{label}</p>
    </Card>
  )
}

function Overview() {
  const { t, i18n } = useTranslation()
  const snapshot = useBillingSnapshot()
  if (snapshot.isLoading) return <Skeleton className="h-48" />
  if (snapshot.isError || !snapshot.data) return <ListError error={snapshot.error} onRetry={() => void snapshot.refetch()} />
  const s = snapshot.data
  const money = (v: number) => formatAed(v, i18n.language)
  const peak = Math.max(1, ...s.trend.map((m) => Math.max(m.billed_fils, m.collected_fils)))

  return (
    <div className="space-y-6">
      <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-4">
        <Kpi label={t('billing.kpi.billed')} value={money(s.billed_fils)} />
        <Kpi label={t('billing.kpi.collected')} value={money(s.collected_fils)} tone="success" />
        <Kpi label={t('billing.kpi.overdue')} value={money(s.overdue_fils)} tone={s.overdue_fils > 0 ? 'danger' : undefined} />
        <Kpi label={t('billing.kpi.outstanding')} value={money(s.outstanding_fils)} />
        <Kpi label={t('billing.kpi.expenses')} value={money(s.expenses_fils)} />
        <Kpi label={t('billing.kpi.net')} value={money(s.net_fils)} tone={s.net_fils < 0 ? 'danger' : 'success'} />
      </div>
      <Card className="p-4">
        <div className="mb-3 flex items-center justify-between">
          <h2 className="font-heading text-lg font-semibold">{t('billing.trend')}</h2>
          <span className="text-xs text-muted-foreground">
            <span className="me-3 inline-flex items-center gap-1"><span className="inline-block h-2.5 w-2.5 rounded-sm bg-primary" aria-hidden="true" />{t('billing.billedLegend')}</span>
            <span className="inline-flex items-center gap-1"><span className="inline-block h-2.5 w-2.5 rounded-sm bg-emerald-500" aria-hidden="true" />{t('billing.collectedLegend')}</span>
          </span>
        </div>
        <ul className="grid grid-cols-6 items-end gap-2" style={{ height: '9rem' }}>
          {s.trend.map((m) => (
            <li key={m.month} className="flex h-full flex-col items-center justify-end gap-1" title={`${m.month}: ${money(m.billed_fils)} / ${money(m.collected_fils)}`}>
              <div className="flex h-full w-full items-end justify-center gap-1">
                <span className="w-1/2 rounded-t bg-primary" style={{ height: `${(m.billed_fils / peak) * 100}%` }} />
                <span className="w-1/2 rounded-t bg-emerald-500" style={{ height: `${(m.collected_fils / peak) * 100}%` }} />
              </div>
              <span className="font-mono text-[10px] text-muted-foreground" dir="ltr">{m.month.slice(2)}</span>
            </li>
          ))}
        </ul>
      </Card>
      <div className="grid gap-4 lg:grid-cols-2">
        <Card className="p-4">
          <h2 className="mb-2 font-heading text-lg font-semibold">{t('billing.recent')}</h2>
          {s.recent_payments.length === 0 ? <p className="text-sm text-muted-foreground">{t('billing.none')}</p> : (
            <ul className="divide-y text-sm">
              {s.recent_payments.map((p, i) => (
                <li key={`${p.invoice_id}-${i}`} className="flex items-center justify-between gap-2 py-2">
                  <span>{p.student_name} <span className="text-xs text-muted-foreground" dir="ltr">{p.invoice_number} · {formatDate(p.payment_date, i18n.language)}</span></span>
                  <span className="font-semibold" dir="ltr">{money(p.amount_fils)}</span>
                </li>
              ))}
            </ul>
          )}
        </Card>
        <Card className="p-4">
          <h2 className="mb-2 font-heading text-lg font-semibold">{t('billing.topOverdue')}</h2>
          {s.top_overdue.length === 0 ? <p className="text-sm text-muted-foreground">{t('billing.none')}</p> : (
            <ul className="divide-y text-sm">
              {s.top_overdue.map((o) => (
                <li key={o.student_id} className="flex items-center justify-between gap-2 py-2">
                  <span>{o.student_name}</span>
                  <span className="font-semibold text-red-700" dir="ltr">{money(o.outstanding_fils)}</span>
                </li>
              ))}
            </ul>
          )}
        </Card>
      </div>
    </div>
  )
}

function Invoices({ canWrite, onOpen }: { canWrite: boolean; onOpen: (id: string) => void }) {
  const { t, i18n } = useTranslation()
  const [status, setStatus] = useState('')
  const [creating, setCreating] = useState(false)
  const list = useInvoices({ status: status || undefined })
  const generate = useGenerateInvoices()
  const message = useApiErrorMessage()
  const me = useAuth((s) => s.me)
  const rows = flatten(list.data)

  const run = async () => {
    try {
      const result = await generate.mutateAsync(undefined)
      toast.success(t('billing.generated', { count: result.created }))
    } catch (error) {
      toast.error(message(error))
    }
  }

  return (
    <>
      <div className="mb-4 flex flex-wrap items-center gap-2">
        <div className="flex flex-wrap gap-2" role="group" aria-label={t('billing.tabs.invoices')}>
          {STATUSES.map((s) => (
            <Button key={s || 'all'} size="sm" variant={status === s ? 'default' : 'secondary'} aria-pressed={status === s} onClick={() => setStatus(s)}>
              {s ? t(`billing.status.${s}`) : t('billing.status.all')}
            </Button>
          ))}
        </div>
        {canWrite && (
          <div className="ms-auto flex flex-wrap gap-2">
            {can(me, 'invoices.generate') && <Button variant="secondary" onClick={() => void run()} disabled={generate.isPending}><ReceiptText className="h-4 w-4" aria-hidden="true" />{t('billing.generate')}</Button>}
            <Button onClick={() => setCreating(true)}><Plus className="h-4 w-4" aria-hidden="true" />{t('billing.newInvoice')}</Button>
          </div>
        )}
      </div>
      <Card className="overflow-hidden">
        {list.isLoading ? <ListSkeleton /> : list.isError ? <ListError error={list.error} onRetry={() => void list.refetch()} /> :
          rows.length === 0 ? <EmptyState icon={<Wallet className="h-7 w-7" aria-hidden="true" />} title={t('billing.empty')} description={t('billing.emptyBody')} /> : (
            <>
              <Table>
                <caption className="sr-only">{t('billing.tabs.invoices')}</caption>
                <thead>
                  <tr>
                    <Th>{t('billing.columns.number')}</Th><Th>{t('billing.columns.child')}</Th><Th>{t('billing.columns.due')}</Th>
                    <Th>{t('billing.columns.total')}</Th><Th>{t('billing.columns.paid')}</Th><Th>{t('billing.columns.status')}</Th>
                  </tr>
                </thead>
                <tbody>
                  {rows.map((r) => (
                    <tr key={r.id} className="cursor-pointer hover:bg-slate-50/70">
                      <Td className="font-mono text-xs" dir="ltr">
                        <button type="button" className="text-start font-bold text-primary-dark underline-offset-4 hover:underline focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring" onClick={() => onOpen(r.id)}>{r.invoice_number}</button>
                      </Td>
                      <Td className="font-semibold">{r.student_name}</Td>
                      <Td>{formatDate(r.due_date, i18n.language)}</Td>
                      <Td dir="ltr" className="text-start">{formatAed(r.total_fils, i18n.language)}</Td>
                      <Td dir="ltr" className="text-start">{formatAed(r.paid_fils, i18n.language)}</Td>
                      <Td><Badge tone={statusTone[r.status]}>{t(`billing.status.${r.status}`)}</Badge></Td>
                    </tr>
                  ))}
                </tbody>
              </Table>
              <LoadMore visible={!!list.hasNextPage} loading={list.isFetchingNextPage} onClick={() => void list.fetchNextPage()} />
            </>
          )}
      </Card>
      {canWrite && <NewInvoiceDialog open={creating} onOpenChange={setCreating} />}
    </>
  )
}

function Plans({ onOpen }: { onOpen: (id: string | null) => void }) {
  const { t, i18n } = useTranslation()
  const list = useFeePlans()
  const plans = flatten(list.data)
  return (
    <>
      <div className="mb-4 flex justify-end"><Button onClick={() => onOpen(null)}><Plus className="h-4 w-4" aria-hidden="true" />{t('billing.plan.add')}</Button></div>
      <Card className="overflow-hidden">
        {list.isLoading ? <ListSkeleton /> : list.isError ? <ListError error={list.error} onRetry={() => void list.refetch()} /> :
          plans.length === 0 ? <EmptyState icon={<Wallet className="h-7 w-7" aria-hidden="true" />} title={t('billing.plan.empty')} description={t('billing.plan.emptyBody')} /> : (
            <ul className="divide-y">
              {plans.map((p) => (
                <li key={p.id}>
                  <button type="button" onClick={() => onOpen(p.id)} className="flex w-full flex-wrap items-center gap-3 px-4 py-3 text-start hover:bg-slate-50 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring">
                    <span className="font-bold">{localized(p.name, i18n.language)}</span>
                    <Badge tone="info">{t(`billing.plan.period.${p.period}`)}</Badge>
                    {p.status === 'inactive' && <Badge>{t('billing.plan.form.inactive')}</Badge>}
                    <span className="text-xs text-muted-foreground">{t('billing.plan.children', { count: p.student_count })}</span>
                    <span className="ms-auto font-semibold" dir="ltr">{formatAed(p.total_per_period_fils, i18n.language)}</span>
                  </button>
                </li>
              ))}
            </ul>
          )}
      </Card>
    </>
  )
}

export default function BillingPage() {
  const { t } = useTranslation()
  const me = useAuth((s) => s.me)
  const parent = isParentOnly(me)
  const canWrite = can(me, 'billing.write')
  const [tab, setTab] = useState(canWrite ? 'overview' : 'invoices')
  const [invoice, setInvoice] = useState<string | null>(null)
  const [plan, setPlan] = useState<{ id: string | null } | null>(null)

  const tabs = [...(canWrite ? ['overview'] : []), 'invoices', ...(canWrite ? ['plans', 'expenses', 'reports', 'settings'] : [])]

  return (
    <>
      <PageHeader title={t('billing.title')} subtitle={parent ? t('billing.parentSubtitle') : t('billing.subtitle')} />
      <TabsPrimitive.Root value={tab} onValueChange={setTab}>
        {tabs.length > 1 && (
          <TabsPrimitive.List className="mb-5 flex gap-1 overflow-x-auto border-b">
            {tabs.map((key) => (
              <TabsPrimitive.Trigger
                key={key}
                value={key}
                className="whitespace-nowrap border-b-2 border-transparent px-4 py-2.5 text-sm font-semibold text-muted-foreground transition-colors hover:text-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring data-[state=active]:border-primary data-[state=active]:font-bold data-[state=active]:text-primary-dark"
              >
                {t(`billing.tabs.${key}`)}
              </TabsPrimitive.Trigger>
            ))}
          </TabsPrimitive.List>
        )}
        {canWrite && <TabsPrimitive.Content value="overview"><Overview /></TabsPrimitive.Content>}
        <TabsPrimitive.Content value="invoices"><Invoices canWrite={canWrite} onOpen={setInvoice} /></TabsPrimitive.Content>
        {canWrite && <TabsPrimitive.Content value="plans"><Plans onOpen={(id) => setPlan({ id })} /></TabsPrimitive.Content>}
        {canWrite && <TabsPrimitive.Content value="expenses"><ExpensesTab /></TabsPrimitive.Content>}
        {canWrite && <TabsPrimitive.Content value="reports"><ReportsTab /></TabsPrimitive.Content>}
        {canWrite && <TabsPrimitive.Content value="settings"><SettingsTab /></TabsPrimitive.Content>}
      </TabsPrimitive.Root>
      <InvoiceDialog id={invoice} onClose={() => setInvoice(null)} />
      {canWrite && <FeePlanDialog open={!!plan} planId={plan?.id ?? null} onClose={() => setPlan(null)} />}
    </>
  )
}

import * as TabsPrimitive from '@radix-ui/react-tabs'
import { Inbox, Plus, Search } from 'lucide-react'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { flatten, useEnquiries, useEnquirySummary } from '@/api/hooks'
import { PageHeader } from '@/components/layout/PageHeader'
import { ListError, ListSkeleton, LoadMore } from '@/components/shared/ListState'
import { Button } from '@/components/ui/button'
import { Input, Select } from '@/components/ui/form-controls'
import { Badge, Card, EmptyState, Table, Td, Th, type Tone } from '@/components/ui/surfaces'
import { formatDate } from '@/lib/format'
import { cn } from '@/lib/utils'
import { can, useAuth } from '@/stores/auth'
import { EnquiryDialog, SOURCES } from './EnquiryDialog'
import { TasksTab } from './TasksTab'
import { ToursTab } from './ToursTab'

const stageTone: Record<string, Tone> = { no_stage: 'neutral', interested: 'info', contacted: 'info', tour_appointment: 'warning', toured: 'warning', enrolled: 'success' }

function Pipeline() {
  const { t, i18n } = useTranslation()
  const me = useAuth((s) => s.me)
  const [stage, setStage] = useState('')
  const [source, setSource] = useState('')
  const [search, setSearch] = useState('')
  const [archived, setArchived] = useState(false)
  const [open, setOpen] = useState<{ id: string | null } | null>(null)
  const summary = useEnquirySummary()
  const list = useEnquiries({ stage: stage || undefined, source: source || undefined, search: search || undefined, archived })
  const rows = flatten(list.data)
  const canWrite = can(me, 'enquiries.write')

  return (
    <>
      {summary.data && (
        <ul className="mb-4 grid grid-cols-2 gap-2 sm:grid-cols-3 lg:grid-cols-6">
          {summary.data.by_stage.map((s) => (
            <li key={s.stage}>
              <button
                type="button"
                aria-pressed={stage === s.stage}
                onClick={() => setStage(stage === s.stage ? '' : s.stage)}
                className={cn('w-full rounded-lg border bg-white p-3 text-start transition-colors hover:bg-slate-50 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring', stage === s.stage && 'border-primary bg-accent')}
              >
                <span className="block font-heading text-2xl font-bold">{s.count}</span>
                <span className="text-[11px] font-bold uppercase tracking-wide text-muted-foreground">{t(`enquiries.stages.${s.stage}`)}</span>
              </button>
            </li>
          ))}
        </ul>
      )}
      <div className="mb-4 flex flex-wrap gap-3">
        <div className="relative min-w-[220px] flex-1">
          <Search className="pointer-events-none absolute start-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground" aria-hidden="true" />
          <Input type="search" value={search} onChange={(e) => setSearch(e.target.value)} placeholder={t('enquiries.searchPlaceholder')} aria-label={t('common.search')} className="ps-9" />
        </div>
        <Select value={source} onChange={(e) => setSource(e.target.value)} aria-label={t('enquiries.form.source')} className="w-48">
          <option value="">{t('enquiries.allSources')}</option>
          {SOURCES.map((s) => <option key={s} value={s}>{t(`enquiries.sources.${s}`)}</option>)}
        </Select>
        <label className="flex items-center gap-2 text-sm"><input type="checkbox" className="h-4 w-4 accent-primary" checked={archived} onChange={(e) => setArchived(e.target.checked)} />{t('enquiries.showArchived')}</label>
        {canWrite && <Button className="ms-auto" onClick={() => setOpen({ id: null })}><Plus className="h-4 w-4" aria-hidden="true" />{t('enquiries.add')}</Button>}
      </div>
      <Card className="overflow-hidden">
        {list.isLoading ? <ListSkeleton /> : list.isError ? <ListError error={list.error} onRetry={() => void list.refetch()} /> :
          rows.length === 0 ? <EmptyState icon={<Inbox className="h-7 w-7" aria-hidden="true" />} title={t('enquiries.empty')} description={t('enquiries.emptyBody')} /> : (
            <>
              <Table>
                <caption className="sr-only">{t('enquiries.tabs.pipeline')}</caption>
                <thead>
                  <tr><Th>{t('enquiries.form.family')}</Th><Th>{t('enquiries.form.child')}</Th><Th>{t('enquiries.stage')}</Th><Th>{t('enquiries.form.source')}</Th><Th>{t('enquiries.form.staff')}</Th><Th>{t('enquiries.date')}</Th></tr>
                </thead>
                <tbody>
                  {rows.map((r) => (
                    <tr key={r.id}>
                      <Td>
                        <button type="button" className="text-start font-bold text-primary-dark underline-offset-4 hover:underline focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring" onClick={() => setOpen({ id: r.id })}>{r.family_name}</button>
                      </Td>
                      <Td>{r.child_name}</Td>
                      <Td><Badge tone={stageTone[r.stage]}>{t(`enquiries.stages.${r.stage}`)}</Badge></Td>
                      <Td>{t(`enquiries.sources.${r.source}`)}</Td>
                      <Td>{r.assigned_staff_name ?? '—'}</Td>
                      <Td>{formatDate(r.enquiry_date, i18n.language)}</Td>
                    </tr>
                  ))}
                </tbody>
              </Table>
              <LoadMore visible={!!list.hasNextPage} loading={list.isFetchingNextPage} onClick={() => void list.fetchNextPage()} />
            </>
          )}
      </Card>
      {summary.data && summary.data.by_source.length > 0 && (
        <Card className="mt-4 p-4">
          <h2 className="mb-2 text-sm font-bold">{t('enquiries.bySource')}</h2>
          <ul className="flex flex-wrap gap-2 text-xs">
            {summary.data.by_source.map((s) => (
              <li key={s.source} className="rounded-full border px-2.5 py-1">{t(`enquiries.sources.${s.source}`)} · {s.total} · {t('enquiries.enrolledCount', { count: s.enrolled })}</li>
            ))}
          </ul>
        </Card>
      )}
      <EnquiryDialog id={open?.id ?? null} open={!!open} onClose={() => setOpen(null)} />
    </>
  )
}

export default function EnquiriesPage() {
  const { t } = useTranslation()
  const me = useAuth((s) => s.me)
  const [tab, setTab] = useState('pipeline')
  const tabs = ['pipeline', ...(can(me, 'tours.read') ? ['tours'] : []), 'tasks']

  return (
    <>
      <PageHeader title={t('enquiries.title')} subtitle={t('enquiries.subtitle')} />
      <TabsPrimitive.Root value={tab} onValueChange={setTab}>
        <TabsPrimitive.List className="mb-5 flex gap-1 overflow-x-auto border-b">
          {tabs.map((key) => (
            <TabsPrimitive.Trigger
              key={key}
              value={key}
              className="whitespace-nowrap border-b-2 border-transparent px-4 py-2.5 text-sm font-semibold text-muted-foreground transition-colors hover:text-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring data-[state=active]:border-primary data-[state=active]:font-bold data-[state=active]:text-primary-dark"
            >
              {t(`enquiries.tabs.${key}`)}
            </TabsPrimitive.Trigger>
          ))}
        </TabsPrimitive.List>
        <TabsPrimitive.Content value="pipeline"><Pipeline /></TabsPrimitive.Content>
        {can(me, 'tours.read') && <TabsPrimitive.Content value="tours"><ToursTab /></TabsPrimitive.Content>}
        <TabsPrimitive.Content value="tasks"><TasksTab /></TabsPrimitive.Content>
      </TabsPrimitive.Root>
    </>
  )
}


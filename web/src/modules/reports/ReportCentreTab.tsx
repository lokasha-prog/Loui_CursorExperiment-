import { Download, FileSpreadsheet, Loader2 } from 'lucide-react'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { api } from '@/api/client'
import { flatten, useProgramOptions, useReportDefinitions, useReportRuns, useRequestReport, useRoomOptions, useStaffOptions } from '@/api/hooks'
import type { ReportDefinition, ReportRun } from '@/api/types'
import { ListError, LoadMore } from '@/components/shared/ListState'
import { Button } from '@/components/ui/button'
import { Field, Input, Select } from '@/components/ui/form-controls'
import { Alert, Badge, Card, Skeleton } from '@/components/ui/surfaces'
import { toast } from '@/components/ui/toaster'
import { useApiErrorMessage } from '@/hooks/use-api-error'
import { formatDate, todayIso } from '@/lib/format'
import { localized } from '@/lib/utils'

const SOURCES = ['walk_in', 'website', 'referral', 'social_media', 'phone', 'email', 'existing_parent']
const INVOICE_STATUSES = ['issued', 'partially_paid', 'paid', 'overdue']

const monthsBack = (n: number) => Array.from({ length: n }, (_, i) => {
  const d = new Date(new Date().getFullYear(), new Date().getMonth() - i, 1)
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}`
})

function RunRow({ run, defs }: { run: ReportRun; defs: ReportDefinition[] }) {
  const { t, i18n } = useTranslation()
  const message = useApiErrorMessage()
  const name = defs.find((d) => d.kind === run.kind)?.name
  const get = async () => {
    try {
      const blob = await api.blob(`/reports/center/runs/${run.id}/file`)
      const url = URL.createObjectURL(blob)
      const a = document.createElement('a')
      a.href = url
      a.download = run.file_name ?? 'report'
      a.click()
      window.setTimeout(() => URL.revokeObjectURL(url), 30_000)
    } catch (error) {
      toast.error(message(error))
    }
  }
  const busy = run.status === 'pending' || run.status === 'running'
  return (
    <li className="flex flex-wrap items-center gap-3 px-4 py-3 text-sm">
      <span className="font-semibold">{name ? localized(name, i18n.language) : run.kind}</span>
      <Badge tone="info">{t(`reports.centre.formats.${run.format}`)}</Badge>
      <span className="text-xs text-muted-foreground">{formatDate(run.created_at, i18n.language)}</span>
      {busy && <span className="flex items-center gap-1 text-xs text-muted-foreground"><Loader2 className="h-3.5 w-3.5 animate-spin" aria-hidden="true" />{t('reports.centre.making')}</span>}
      {run.status === 'failed' && <Badge tone="danger">{t('reports.centre.failed')}</Badge>}
      {run.status === 'done' && <Button size="sm" variant="secondary" className="ms-auto" onClick={() => void get()}><Download className="h-4 w-4" aria-hidden="true" />{t('reports.centre.download')}</Button>}
    </li>
  )
}

/** Pick a report, set its filters and get a file. Reports are made in the background and listed below for 30 days. */
export function ReportCentreTab() {
  const { t, i18n } = useTranslation()
  const defs = useReportDefinitions()
  const runs = useReportRuns()
  const request = useRequestReport()
  const rooms = useRoomOptions()
  const programs = useProgramOptions()
  const staff = useStaffOptions()
  const message = useApiErrorMessage()
  const [kind, setKind] = useState('')
  const [format, setFormat] = useState('')
  const [params, setParams] = useState<Record<string, string>>({ month: monthsBack(1)[0], from: `${new Date().getFullYear()}-01-01`, to: todayIso(), date: todayIso() })
  const [language, setLanguage] = useState(i18n.language === 'ar' ? 'ar' : 'en')
  const [failure, setFailure] = useState<string | null>(null)
  const list = defs.data ?? []
  const def = list.find((d) => d.kind === kind) ?? list[0]
  const chosenFormat = def?.formats.includes(format) ? format : def?.formats[0]
  const set = (key: string, value: string) => setParams((p) => ({ ...p, [key]: value }))

  const generate = async () => {
    if (!def || !chosenFormat) return
    setFailure(null)
    const use = Object.fromEntries(def.filters.map((f) => [f, params[f] ?? '']).filter(([, v]) => v))
    try {
      await request.mutateAsync({ kind: def.kind, format: chosenFormat, parameters: use, language })
      toast.success(t('reports.centre.started'))
    } catch (error) {
      setFailure(message(error))
    }
  }

  if (defs.isLoading) return <Skeleton className="h-64" />
  if (defs.isError) return <ListError error={defs.error} onRetry={() => void defs.refetch()} />

  const filterField = (f: string) => {
    switch (f) {
      case 'month':
        return <Field key={f} label={t('reports.month')} htmlFor="rc-month"><Select id="rc-month" value={params.month} onChange={(e) => set('month', e.target.value)}>{monthsBack(12).map((m) => <option key={m} value={m}>{m}</option>)}</Select></Field>
      case 'from':
        return <Field key={f} label={t('reports.from')} htmlFor="rc-from"><Input id="rc-from" type="date" dir="ltr" className="text-start" value={params.from} onChange={(e) => set('from', e.target.value)} /></Field>
      case 'to':
        return <Field key={f} label={t('reports.centre.to')} htmlFor="rc-to"><Input id="rc-to" type="date" dir="ltr" className="text-start" value={params.to} onChange={(e) => set('to', e.target.value)} /></Field>
      case 'date':
        return <Field key={f} label={t('reports.centre.date')} htmlFor="rc-date"><Input id="rc-date" type="date" dir="ltr" className="text-start" value={params.date} onChange={(e) => set('date', e.target.value)} /></Field>
      case 'room_id':
        return <Field key={f} label={t('attendance.filters.room')} htmlFor="rc-room"><Select id="rc-room" value={params.room_id ?? ''} onChange={(e) => set('room_id', e.target.value)}><option value="">{t('reports.centre.any')}</option>{(rooms.data ?? []).map((r) => <option key={r.id} value={r.id}>{localized(r.name, i18n.language)}</option>)}</Select></Field>
      case 'program_id':
        return <Field key={f} label={t('reports.centre.program')} htmlFor="rc-prog"><Select id="rc-prog" value={params.program_id ?? ''} onChange={(e) => set('program_id', e.target.value)}><option value="">{t('reports.centre.any')}</option>{(programs.data ?? []).map((r) => <option key={r.id} value={r.id}>{localized(r.name, i18n.language)}</option>)}</Select></Field>
      case 'staff_id':
        return <Field key={f} label={t('reports.centre.staff')} htmlFor="rc-staff"><Select id="rc-staff" value={params.staff_id ?? ''} onChange={(e) => set('staff_id', e.target.value)}><option value="">{t('reports.centre.any')}</option>{(staff.data ?? []).map((s) => <option key={s.id} value={s.id}>{s.first_name} {s.last_name}</option>)}</Select></Field>
      case 'source':
        return <Field key={f} label={t('reports.centre.source')} htmlFor="rc-source"><Select id="rc-source" value={params.source ?? ''} onChange={(e) => set('source', e.target.value)}><option value="">{t('reports.centre.any')}</option>{SOURCES.map((s) => <option key={s} value={s}>{t(`enquiries.sources.${s}`)}</option>)}</Select></Field>
      case 'status':
        return <Field key={f} label={t('reports.centre.status')} htmlFor="rc-status"><Select id="rc-status" value={params.status ?? ''} onChange={(e) => set('status', e.target.value)}><option value="">{t('reports.centre.any')}</option>{INVOICE_STATUSES.map((s) => <option key={s} value={s}>{t(`billing.status.${s}`)}</option>)}</Select></Field>
      case 'user_email':
        return <Field key={f} label={t('reports.centre.userEmail')} htmlFor="rc-user"><Input id="rc-user" type="email" dir="ltr" className="text-start" value={params.user_email ?? ''} onChange={(e) => set('user_email', e.target.value)} /></Field>
      case 'entity_type':
        return <Field key={f} label={t('reports.centre.entity')} htmlFor="rc-entity"><Input id="rc-entity" dir="ltr" className="text-start" value={params.entity_type ?? ''} onChange={(e) => set('entity_type', e.target.value)} /></Field>
      case 'action':
        return <Field key={f} label={t('reports.centre.action')} htmlFor="rc-action"><Input id="rc-action" dir="ltr" className="text-start" value={params.action ?? ''} onChange={(e) => set('action', e.target.value)} /></Field>
      default:
        return null
    }
  }

  return (
    <div className="space-y-6">
      <Card className="space-y-4 p-4">
        <h2 className="flex items-center gap-2 font-heading text-lg font-semibold"><FileSpreadsheet className="h-5 w-5" aria-hidden="true" />{t('reports.centre.title')}</h2>
        {failure && <Alert>{failure}</Alert>}
        <div className="grid gap-3 sm:grid-cols-3">
          <Field label={t('reports.centre.which')} htmlFor="rc-kind" className="sm:col-span-3">
            <Select id="rc-kind" value={def?.kind ?? ''} onChange={(e) => { setKind(e.target.value); setFormat('') }}>
              {list.map((d) => <option key={d.kind} value={d.kind}>{localized(d.name, i18n.language)}</option>)}
            </Select>
          </Field>
          {def?.filters.map(filterField)}
          <Field label={t('reports.centre.format')} htmlFor="rc-format">
            <Select id="rc-format" value={chosenFormat ?? ''} onChange={(e) => setFormat(e.target.value)}>{def?.formats.map((f) => <option key={f} value={f}>{t(`reports.centre.formats.${f}`)}</option>)}</Select>
          </Field>
          <Field label={t('tools.cards.language')} htmlFor="rc-lang"><Select id="rc-lang" value={language} onChange={(e) => setLanguage(e.target.value)}><option value="en">English</option><option value="ar">العربية</option></Select></Field>
        </div>
        <div className="flex justify-end"><Button onClick={() => void generate()} disabled={request.isPending || !def}>{t('reports.centre.generate')}</Button></div>
      </Card>

      <Card className="overflow-hidden">
        <div className="border-b px-4 py-3"><h2 className="font-heading text-lg font-semibold">{t('reports.centre.recent')}</h2><p className="text-xs text-muted-foreground">{t('reports.centre.keep')}</p></div>
        {runs.isLoading ? <Skeleton className="m-4 h-16" /> : flatten(runs.data).length === 0 ? <p className="p-4 text-sm text-muted-foreground">{t('reports.centre.none')}</p> : (
          <>
            <ul className="divide-y">{flatten(runs.data).map((r) => <RunRow key={r.id} run={r} defs={list} />)}</ul>
            <LoadMore visible={!!runs.hasNextPage} loading={runs.isFetchingNextPage} onClick={() => void runs.fetchNextPage()} />
          </>
        )}
      </Card>
    </div>
  )
}

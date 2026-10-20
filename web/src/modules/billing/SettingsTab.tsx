import { useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { flatten, useBillingSettings, useReminderLog, useRunReminders, useSaveBillingSettings } from '@/api/hooks'
import type { BillingSettings } from '@/api/types'
import { ListError, ListSkeleton, LoadMore } from '@/components/shared/ListState'
import { Button } from '@/components/ui/button'
import { Field, Input, Textarea } from '@/components/ui/form-controls'
import { Alert, Badge, Card, Skeleton, Table, Td, Th } from '@/components/ui/surfaces'
import { toast } from '@/components/ui/toaster'
import { useApiErrorMessage } from '@/hooks/use-api-error'
import { formatDate } from '@/lib/format'
import { can, useAuth } from '@/stores/auth'

const parseDays = (text: string) => [...new Set(text.split(',').map((x) => Number.parseInt(x.trim(), 10)).filter((n) => Number.isFinite(n) && n > 0))].sort((a, b) => b - a)
const VARIABLES = '{child} {invoice} {amount} {due}'

/** Wording for one kind of reminder, in both languages. */
function Wording({ id, title, subject, body, onChange }: {
  id: string
  title: string
  subject: Record<string, string>
  body: Record<string, string>
  onChange: (subject: Record<string, string>, body: Record<string, string>) => void
}) {
  const { t } = useTranslation()
  return (
    <fieldset className="space-y-3 rounded-md border p-3">
      <legend className="px-1 text-sm font-bold">{title}</legend>
      <div className="grid gap-3 sm:grid-cols-2">
        <Field label={`${t('billing.settings.subject')} (EN)`} htmlFor={`${id}-se`}><Input id={`${id}-se`} value={subject.en ?? ''} onChange={(e) => onChange({ ...subject, en: e.target.value }, body)} /></Field>
        <Field label={`${t('billing.settings.subject')} (AR)`} htmlFor={`${id}-sa`}><Input id={`${id}-sa`} dir="rtl" lang="ar" value={subject.ar ?? ''} onChange={(e) => onChange({ ...subject, ar: e.target.value }, body)} /></Field>
        <Field label={`${t('billing.settings.body')} (EN)`} htmlFor={`${id}-be`}><Textarea id={`${id}-be`} rows={4} value={body.en ?? ''} onChange={(e) => onChange(subject, { ...body, en: e.target.value })} /></Field>
        <Field label={`${t('billing.settings.body')} (AR)`} htmlFor={`${id}-ba`}><Textarea id={`${id}-ba`} rows={4} dir="rtl" lang="ar" value={body.ar ?? ''} onChange={(e) => onChange(subject, { ...body, ar: e.target.value })} /></Field>
      </div>
    </fieldset>
  )
}

function Form({ settings }: { settings: BillingSettings }) {
  const { t } = useTranslation()
  const save = useSaveBillingSettings()
  const message = useApiErrorMessage()
  const [draft, setDraft] = useState(settings)
  const [before, setBefore] = useState(settings.before_due_days.join(', '))
  const [after, setAfter] = useState(settings.after_due_days.join(', '))
  const [failure, setFailure] = useState<string | null>(null)

  useEffect(() => {
    setDraft(settings)
    setBefore(settings.before_due_days.join(', '))
    setAfter(settings.after_due_days.join(', '))
  }, [settings])

  const submit = async () => {
    setFailure(null)
    try {
      await save.mutateAsync({ ...draft, before_due_days: parseDays(before), after_due_days: parseDays(after) })
      toast.success(t('billing.settings.saved'))
    } catch (error) {
      setFailure(message(error))
    }
  }

  return (
    <form onSubmit={(e) => { e.preventDefault(); void submit() }} className="space-y-4" noValidate>
      {failure && <Alert>{failure}</Alert>}
      <div className="grid gap-3 sm:grid-cols-2">
        <Field label={t('billing.settings.dueDays')} htmlFor="bs-due" hint={t('billing.settings.dueDaysHint')}>
          <Input id="bs-due" inputMode="numeric" dir="ltr" className="text-start" value={draft.due_days} onChange={(e) => setDraft({ ...draft, due_days: Number(e.target.value.replace(/\D/g, '')) })} />
        </Field>
        <label className="flex items-center gap-2 self-end pb-2 text-sm font-semibold">
          <input type="checkbox" className="h-4 w-4 accent-primary" checked={draft.reminders_enabled} onChange={(e) => setDraft({ ...draft, reminders_enabled: e.target.checked })} />
          {t('billing.settings.enabled')}
        </label>
        <Field label={t('billing.settings.before')} htmlFor="bs-before" hint={t('billing.settings.daysHint')}><Input id="bs-before" dir="ltr" className="text-start" value={before} onChange={(e) => setBefore(e.target.value)} /></Field>
        <Field label={t('billing.settings.after')} htmlFor="bs-after" hint={t('billing.settings.daysHint')}><Input id="bs-after" dir="ltr" className="text-start" value={after} onChange={(e) => setAfter(e.target.value)} /></Field>
      </div>
      <p className="text-xs text-muted-foreground">{t('billing.settings.variables')} <span dir="ltr" className="font-mono">{VARIABLES}</span></p>
      <Wording id="bs-b" title={t('billing.settings.beforeWording')} subject={draft.before_subject} body={draft.before_body} onChange={(s, b) => setDraft({ ...draft, before_subject: s, before_body: b })} />
      <Wording id="bs-o" title={t('billing.settings.overdueWording')} subject={draft.overdue_subject} body={draft.overdue_body} onChange={(s, b) => setDraft({ ...draft, overdue_subject: s, overdue_body: b })} />
      <div className="flex justify-end"><Button type="submit" disabled={save.isPending}>{t('billing.settings.save')}</Button></div>
    </form>
  )
}

function ReminderLog() {
  const { t, i18n } = useTranslation()
  const me = useAuth((s) => s.me)
  const list = useReminderLog()
  const run = useRunReminders()
  const message = useApiErrorMessage()
  const rows = flatten(list.data)

  const sendNow = async () => {
    try {
      const result = await run.mutateAsync()
      toast.success(t('billing.settings.ran', { count: result.sent }))
    } catch (error) {
      toast.error(message(error))
    }
  }

  return (
    <section aria-labelledby="bs-log" className="space-y-3">
      <div className="flex items-center justify-between gap-3">
        <h2 id="bs-log" className="font-heading text-lg font-semibold">{t('billing.settings.log')}</h2>
        {can(me, 'invoices.generate') && <Button variant="secondary" onClick={() => void sendNow()} disabled={run.isPending}>{t('billing.settings.runNow')}</Button>}
      </div>
      <Card className="overflow-hidden">
        {list.isLoading ? <ListSkeleton /> : list.isError ? <ListError error={list.error} onRetry={() => void list.refetch()} /> :
          rows.length === 0 ? <p className="p-4 text-sm text-muted-foreground">{t('billing.settings.logEmpty')}</p> : (
            <>
              <Table>
                <caption className="sr-only">{t('billing.settings.log')}</caption>
                <thead><tr><Th>{t('billing.columns.number')}</Th><Th>{t('billing.columns.child')}</Th><Th>{t('billing.settings.kind')}</Th><Th>{t('billing.settings.recipients')}</Th><Th>{t('billing.settings.sentOn')}</Th></tr></thead>
                <tbody>
                  {rows.map((r) => (
                    <tr key={r.id}>
                      <Td className="font-mono text-xs" dir="ltr">{r.invoice_number}</Td>
                      <Td className="font-semibold">{r.student_name}</Td>
                      <Td><Badge tone={r.kind === 'overdue' ? 'danger' : 'info'}>{t(`billing.settings.kinds.${r.kind}`, { days: r.offset_days })}</Badge></Td>
                      <Td>{r.status === 'no_recipients' ? t('billing.settings.noRecipients') : r.recipients}</Td>
                      <Td>{formatDate(r.created_at, i18n.language)}</Td>
                    </tr>
                  ))}
                </tbody>
              </Table>
              <LoadMore visible={!!list.hasNextPage} loading={list.isFetchingNextPage} onClick={() => void list.fetchNextPage()} />
            </>
          )}
      </Card>
    </section>
  )
}

export function SettingsTab() {
  const settings = useBillingSettings()
  if (settings.isLoading) return <Skeleton className="h-64" />
  if (settings.isError || !settings.data) return <ListError error={settings.error} onRetry={() => void settings.refetch()} />
  return (
    <div className="space-y-8">
      <Form settings={settings.data} />
      <ReminderLog />
    </div>
  )
}

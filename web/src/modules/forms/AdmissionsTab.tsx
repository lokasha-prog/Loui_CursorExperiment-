import { ArrowDown, ArrowUp, Plus, Trash2 } from 'lucide-react'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { flatten, useAdmissionStatus, useAdmissionSteps, useDeleteStep, useForms, useReorderSteps, useSaveStep, useStartAdmission, useStepAction, useStudentAdmission, useStudentOptions } from '@/api/hooks'
import type { AdmissionStepItem } from '@/api/types'
import { ListError } from '@/components/shared/ListState'
import { Button } from '@/components/ui/button'
import { Field, Input, Select } from '@/components/ui/form-controls'
import { Alert, Badge, Card, Dialog, DialogContent, DialogHeader, Skeleton, type Tone } from '@/components/ui/surfaces'
import { toast } from '@/components/ui/toaster'
import { useApiErrorMessage } from '@/hooks/use-api-error'
import { formatDate } from '@/lib/format'
import { localized } from '@/lib/utils'

const stepTone: Record<string, Tone> = { pending: 'neutral', current: 'info', done: 'success', skipped: 'warning' }

function StepDialog({ step, onClose }: { step: AdmissionStepItem | null; onClose: () => void }) {
  const { t, i18n } = useTranslation()
  const forms = useForms('live')
  const save = useSaveStep(step?.id)
  const message = useApiErrorMessage()
  const [en, setEn] = useState(step?.name.en ?? '')
  const [ar, setAr] = useState(step?.name.ar ?? '')
  const [form, setForm] = useState(step?.form_id ?? '')
  const [days, setDays] = useState(String(step?.due_days ?? 7))
  const [failure, setFailure] = useState<string | null>(null)

  const submit = async () => {
    if (!en.trim()) return setFailure(t('validation.required'))
    setFailure(null)
    try {
      await save.mutateAsync({ name: ar.trim() ? { en: en.trim(), ar: ar.trim() } : { en: en.trim() }, form_id: form || null, due_days: Number(days) || 0 })
      toast.success(t('forms.admissions.stepSaved'))
      onClose()
    } catch (error) {
      setFailure(message(error))
    }
  }

  return (
    <Dialog open onOpenChange={(o) => !o && onClose()}>
      <DialogContent closeLabel={t('common.close')} className="max-w-lg">
        <DialogHeader title={t(step ? 'forms.admissions.editStep' : 'forms.admissions.addStep')} />
        <form onSubmit={(e) => { e.preventDefault(); void submit() }} className="space-y-4" noValidate>
          {failure && <Alert>{failure}</Alert>}
          <div className="grid gap-3 sm:grid-cols-2">
            <Field label={t('forms.admissions.nameEn')} htmlFor="as-en" required><Input id="as-en" value={en} maxLength={150} onChange={(e) => setEn(e.target.value)} /></Field>
            <Field label={t('forms.admissions.nameAr')} htmlFor="as-ar"><Input id="as-ar" dir="rtl" lang="ar" value={ar} maxLength={150} onChange={(e) => setAr(e.target.value)} /></Field>
            <Field label={t('forms.admissions.form')} htmlFor="as-form" hint={t('forms.admissions.formHint')}>
              <Select id="as-form" value={form} onChange={(e) => setForm(e.target.value)}>
                <option value="">{t('forms.admissions.noForm')}</option>
                {flatten(forms.data).map((f) => <option key={f.id} value={f.id}>{localized(f.name, i18n.language)}</option>)}
              </Select>
            </Field>
            <Field label={t('forms.admissions.dueDays')} htmlFor="as-days"><Input id="as-days" inputMode="numeric" dir="ltr" className="text-start" value={days} onChange={(e) => setDays(e.target.value.replace(/\D/g, ''))} /></Field>
          </div>
          <div className="flex justify-end gap-2 border-t pt-4">
            <Button type="button" variant="secondary" onClick={onClose}>{t('common.cancel')}</Button>
            <Button type="submit" disabled={save.isPending}>{t('forms.admissions.saveStep')}</Button>
          </div>
        </form>
      </DialogContent>
    </Dialog>
  )
}

function ChildDialog({ studentId, onClose }: { studentId: string; onClose: () => void }) {
  const { t, i18n } = useTranslation()
  const view = useStudentAdmission(studentId)
  const act = useStepAction()
  const message = useApiErrorMessage()
  const run = async (stepId: string, action: 'complete' | 'skip') => {
    try {
      await act.mutateAsync({ studentId, stepId, action })
    } catch (error) {
      toast.error(message(error))
    }
  }
  return (
    <Dialog open onOpenChange={(o) => !o && onClose()}>
      <DialogContent closeLabel={t('common.close')} className="max-w-xl">
        <DialogHeader title={view.data?.student_name ?? t('forms.admissions.progress')} />
        {!view.data ? <Skeleton className="h-40" /> : (
          <ol className="space-y-2">
            {view.data.steps.map((s) => (
              <li key={s.step_id} className="flex flex-wrap items-center gap-2 rounded-md border p-2 text-sm">
                <span className="font-semibold">{localized(s.name, i18n.language)}</span>
                <Badge tone={s.overdue ? 'danger' : stepTone[s.status]}>{s.overdue ? t('forms.admissions.overdue') : t(`forms.admissions.status.${s.status}`)}</Badge>
                {s.due_date && s.status === 'current' && <span className="text-xs text-muted-foreground">{t('forms.admissions.due', { date: formatDate(s.due_date, i18n.language) })}</span>}
                {s.status === 'current' && (
                  <span className="ms-auto flex gap-1">
                    <Button size="sm" variant="secondary" onClick={() => void run(s.step_id, 'complete')}>{t('forms.admissions.complete')}</Button>
                    <Button size="sm" variant="ghost" onClick={() => void run(s.step_id, 'skip')}>{t('forms.admissions.skip')}</Button>
                  </span>
                )}
              </li>
            ))}
          </ol>
        )}
      </DialogContent>
    </Dialog>
  )
}

/** What a parent sees for their own child: the steps and where they are. */
export function MyChildAdmission({ studentId, name }: { studentId: string; name: string }) {
  const { t, i18n } = useTranslation()
  const view = useStudentAdmission(studentId)
  if (!view.data || view.data.total === 0) return null
  return (
    <Card className="p-4">
      <h3 className="mb-2 font-heading text-base font-semibold">{t('forms.admissions.progressFor', { name })} <span className="text-sm font-normal text-muted-foreground">({view.data.done}/{view.data.total})</span></h3>
      <ol className="space-y-1 text-sm">
        {view.data.steps.map((s) => (
          <li key={s.step_id} className="flex flex-wrap items-center gap-2">
            <Badge tone={s.overdue ? 'danger' : stepTone[s.status]}>{s.overdue ? t('forms.admissions.overdue') : t(`forms.admissions.status.${s.status}`)}</Badge>
            <span>{localized(s.name, i18n.language)}</span>
            {s.due_date && s.status === 'current' && <span className="text-xs text-muted-foreground">{t('forms.admissions.due', { date: formatDate(s.due_date, i18n.language) })}</span>}
          </li>
        ))}
      </ol>
    </Card>
  )
}

/** The admission process: steps in order, a dashboard of where children are, and starting a new child. */
export function AdmissionsTab() {
  const { t, i18n } = useTranslation()
  const steps = useAdmissionSteps()
  const status = useAdmissionStatus()
  const students = useStudentOptions(true, '')
  const reorder = useReorderSteps()
  const remove = useDeleteStep()
  const start = useStartAdmission()
  const message = useApiErrorMessage()
  const [editing, setEditing] = useState<{ step: AdmissionStepItem | null } | null>(null)
  const [child, setChild] = useState('')
  const [viewing, setViewing] = useState<string | null>(null)
  const list = steps.data ?? []

  const run = async (action: () => Promise<unknown>, done?: string) => {
    try {
      await action()
      if (done) toast.success(done)
    } catch (error) {
      toast.error(message(error))
    }
  }
  const move = (i: number, by: number) => {
    const ids = list.map((s) => s.id)
    ;[ids[i], ids[i + by]] = [ids[i + by], ids[i]]
    void run(() => reorder.mutateAsync(ids))
  }

  return (
    <div className="space-y-6">
      <Card className="space-y-3 p-4">
        <div className="flex flex-wrap items-center justify-between gap-2">
          <div>
            <h2 className="font-heading text-lg font-semibold">{t('forms.admissions.steps')}</h2>
            <p className="text-xs text-muted-foreground">{t('forms.admissions.stepsHint')}</p>
          </div>
          <Button onClick={() => setEditing({ step: null })}><Plus className="h-4 w-4" aria-hidden="true" />{t('forms.admissions.addStep')}</Button>
        </div>
        {steps.isLoading ? <Skeleton className="h-24" /> : steps.isError ? <ListError error={steps.error} onRetry={() => void steps.refetch()} /> :
          list.length === 0 ? <p className="text-sm text-muted-foreground">{t('forms.admissions.noSteps')}</p> : (
            <ol className="divide-y text-sm">
              {list.map((s, i) => (
                <li key={s.id} className="flex flex-wrap items-center gap-2 py-2">
                  <span className="w-6 text-muted-foreground" dir="ltr">{i + 1}.</span>
                  <button type="button" className="font-semibold text-primary-dark underline-offset-4 hover:underline focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring" onClick={() => setEditing({ step: s })}>{localized(s.name, i18n.language)}</button>
                  {s.form_name && <Badge tone="info">{s.form_name}</Badge>}
                  <span className="text-xs text-muted-foreground">{t('forms.admissions.dueIn', { count: s.due_days })}</span>
                  <span className="ms-auto flex gap-1">
                    <Button size="sm" variant="ghost" aria-label={t('forms.builder.up')} disabled={i === 0} onClick={() => move(i, -1)}><ArrowUp className="h-4 w-4" aria-hidden="true" /></Button>
                    <Button size="sm" variant="ghost" aria-label={t('forms.builder.down')} disabled={i === list.length - 1} onClick={() => move(i, 1)}><ArrowDown className="h-4 w-4" aria-hidden="true" /></Button>
                    <Button size="sm" variant="ghost" aria-label={t('common.delete')} onClick={() => { if (window.confirm(t('forms.admissions.confirmDelete'))) void run(() => remove.mutateAsync(s.id)) }}><Trash2 className="h-4 w-4" aria-hidden="true" /></Button>
                  </span>
                </li>
              ))}
            </ol>
          )}
      </Card>

      <Card className="space-y-3 p-4">
        <h2 className="font-heading text-lg font-semibold">{t('forms.admissions.dashboard')}</h2>
        {status.data && status.data.steps.length > 0 && (
          <ul className="grid gap-2 sm:grid-cols-2 lg:grid-cols-4">
            {status.data.steps.map((s) => (
              <li key={s.step_id} className="rounded-lg border p-3 text-sm">
                <p className="font-semibold">{localized(s.name, i18n.language)}</p>
                <p className="mt-1 text-xs text-muted-foreground">{t('forms.admissions.counts', { current: s.current, overdue: s.overdue, done: s.done, total: s.total })}</p>
                {s.next_due && <p className="text-xs text-muted-foreground">{t('forms.admissions.nextDue', { date: formatDate(s.next_due, i18n.language) })}</p>}
              </li>
            ))}
          </ul>
        )}
        <div className="flex flex-wrap items-end gap-2">
          <Field label={t('forms.admissions.startFor')} htmlFor="ad-child" className="min-w-56">
            <Select id="ad-child" value={child} onChange={(e) => setChild(e.target.value)}>
              <option value="">—</option>
              {(students.data ?? []).map((s) => <option key={s.id} value={s.id}>{s.first_name} {s.last_name}</option>)}
            </Select>
          </Field>
          <Button disabled={!child || start.isPending} onClick={() => void run(async () => { await start.mutateAsync(child); setChild('') }, t('forms.admissions.started'))}>{t('forms.admissions.start')}</Button>
        </div>
        {status.data && status.data.children.length === 0 ? <p className="text-sm text-muted-foreground">{t('forms.admissions.noChildren')}</p> : (
          <ul className="divide-y text-sm">
            {(status.data?.children ?? []).map((c) => (
              <li key={c.student_id} className="flex flex-wrap items-center gap-2 py-2">
                <button type="button" className="font-semibold text-primary-dark underline-offset-4 hover:underline focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring" onClick={() => setViewing(c.student_id)}>{c.student_name}</button>
                <span className="text-xs text-muted-foreground" dir="ltr">{c.done}/{c.total}</span>
                {c.current_step && <Badge tone={c.overdue ? 'danger' : 'info'}>{c.current_step}</Badge>}
                {c.due_date && <span className="text-xs text-muted-foreground">{t('forms.admissions.due', { date: formatDate(c.due_date, i18n.language) })}</span>}
              </li>
            ))}
          </ul>
        )}
      </Card>
      {editing && <StepDialog key={editing.step?.id ?? 'new'} step={editing.step} onClose={() => setEditing(null)} />}
      {viewing && <ChildDialog studentId={viewing} onClose={() => setViewing(null)} />}
    </div>
  )
}

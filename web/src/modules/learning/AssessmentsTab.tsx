import { CheckCircle2, ClipboardCheck, Plus } from 'lucide-react'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { flatten, useApproveRun, useAssessmentRun, useAssessmentRuns, useAssessmentTemplates, useCompleteRun, useFramework, useFrameworks, useSaveScores, useSaveTemplate, useStartRun, useStudentOptions } from '@/api/hooks'
import { ListError, ListSkeleton, LoadMore } from '@/components/shared/ListState'
import { Button } from '@/components/ui/button'
import { Field, Input, Select, Textarea } from '@/components/ui/form-controls'
import { Alert, Badge, Card, Dialog, DialogContent, DialogHeader, EmptyState, Skeleton } from '@/components/ui/surfaces'
import { toast } from '@/components/ui/toaster'
import { useApiErrorMessage } from '@/hooks/use-api-error'
import { formatDate } from '@/lib/format'
import { localized } from '@/lib/utils'

function TemplateDialog({ onClose }: { onClose: () => void }) {
  const { t, i18n } = useTranslation()
  const lang = i18n.language
  const message = useApiErrorMessage()
  const frameworks = useFrameworks()
  const save = useSaveTemplate()
  const [en, setEn] = useState('')
  const [ar, setAr] = useState('')
  const [fw, setFw] = useState('')
  const [band, setBand] = useState('')
  const [kind, setKind] = useState('three_point')
  const [picked, setPicked] = useState<string[]>([])
  const [failure, setFailure] = useState<string | null>(null)
  const detail = useFramework(fw)
  const milestones = (detail.data?.milestones ?? []).filter((m) => !band || m.age_band_id === band)

  const submit = async () => {
    if (!en.trim() || !fw || picked.length === 0) return setFailure(t('learning.assess.templateNeeds'))
    try {
      await save.mutateAsync({ name: ar.trim() ? { en: en.trim(), ar: ar.trim() } : { en: en.trim() }, framework_id: fw, age_band_id: band || null, scoring_kind: kind, milestone_ids: picked })
      toast.success(t('learning.assess.templateSaved'))
      onClose()
    } catch (error) {
      setFailure(message(error))
    }
  }
  return (
    <Dialog open onOpenChange={(o) => !o && onClose()}>
      <DialogContent closeLabel={t('common.close')} className="max-w-2xl">
        <DialogHeader title={t('learning.assess.newTemplate')} />
        <form onSubmit={(e) => { e.preventDefault(); void submit() }} className="max-h-[70vh] space-y-4 overflow-y-auto pe-1" noValidate>
          {failure && <Alert>{failure}</Alert>}
          <div className="grid gap-3 sm:grid-cols-2">
            <Field label={t('learning.lesson.titleEn')} htmlFor="at-en" required><Input id="at-en" value={en} onChange={(e) => setEn(e.target.value)} /></Field>
            <Field label={t('learning.lesson.titleAr')} htmlFor="at-ar"><Input id="at-ar" dir="rtl" lang="ar" value={ar} onChange={(e) => setAr(e.target.value)} /></Field>
            <Field label={t('learning.framework')} htmlFor="at-fw"><Select id="at-fw" value={fw} onChange={(e) => { setFw(e.target.value); setBand(''); setPicked([]) }}><option value="">—</option>{(frameworks.data ?? []).map((f) => <option key={f.id} value={f.id}>{localized(f.name, lang)}</option>)}</Select></Field>
            <Field label={t('learning.band')} htmlFor="at-band"><Select id="at-band" value={band} onChange={(e) => setBand(e.target.value)}><option value="">{t('learning.allBands')}</option>{(detail.data?.age_bands ?? []).map((b) => <option key={b.id} value={b.id}>{localized(b.name, lang)}</option>)}</Select></Field>
            <Field label={t('learning.assess.scoring')} htmlFor="at-kind"><Select id="at-kind" value={kind} onChange={(e) => setKind(e.target.value)}><option value="three_point">{t('learning.assess.kinds.three_point')}</option><option value="binary">{t('learning.assess.kinds.binary')}</option></Select></Field>
          </div>
          {milestones.length > 0 && (
            <fieldset className="rounded-md border p-3">
              <legend className="px-1 text-sm font-bold">{t('learning.assess.pickMilestones', { count: picked.length })}</legend>
              <div className="mb-2 flex gap-2"><Button type="button" size="sm" variant="secondary" onClick={() => setPicked(milestones.map((m) => m.id))}>{t('learning.assess.all')}</Button><Button type="button" size="sm" variant="ghost" onClick={() => setPicked([])}>{t('learning.assess.none')}</Button></div>
              <ul className="max-h-48 space-y-1 overflow-y-auto text-sm">
                {milestones.map((m) => <li key={m.id}><label className="flex items-start gap-2"><input type="checkbox" className="mt-1" checked={picked.includes(m.id)} onChange={(e) => setPicked((p) => (e.target.checked ? [...p, m.id] : p.filter((x) => x !== m.id)))} /><span>{localized(m.statement, lang)}</span></label></li>)}
              </ul>
            </fieldset>
          )}
          <div className="flex justify-end gap-2 border-t pt-4"><Button type="button" variant="secondary" onClick={onClose}>{t('common.cancel')}</Button><Button type="submit" disabled={save.isPending}>{t('common.save')}</Button></div>
        </form>
      </DialogContent>
    </Dialog>
  )
}

function StartDialog({ onClose, onStarted }: { onClose: () => void; onStarted: (id: string) => void }) {
  const { t, i18n } = useTranslation()
  const lang = i18n.language
  const message = useApiErrorMessage()
  const templates = useAssessmentTemplates()
  const students = useStudentOptions()
  const start = useStartRun()
  const [template, setTemplate] = useState('')
  const [student, setStudent] = useState('')
  const [term, setTerm] = useState('')
  const [failure, setFailure] = useState<string | null>(null)
  const submit = async () => {
    if (!template || !student || !term.trim()) return setFailure(t('validation.required'))
    try {
      const run = await start.mutateAsync({ template_id: template, student_id: student, term: term.trim() })
      onStarted(run.id)
    } catch (error) {
      setFailure(message(error))
    }
  }
  return (
    <Dialog open onOpenChange={(o) => !o && onClose()}>
      <DialogContent closeLabel={t('common.close')} className="max-w-md">
        <DialogHeader title={t('learning.assess.start')} />
        <form onSubmit={(e) => { e.preventDefault(); void submit() }} className="space-y-3" noValidate>
          {failure && <Alert>{failure}</Alert>}
          <Field label={t('learning.assess.template')} htmlFor="st-t" required><Select id="st-t" value={template} onChange={(e) => setTemplate(e.target.value)}><option value="">—</option>{(templates.data ?? []).map((x) => <option key={x.id} value={x.id}>{localized(x.name, lang)}</option>)}</Select></Field>
          <Field label={t('learning.obs.child')} htmlFor="st-s" required><Select id="st-s" value={student} onChange={(e) => setStudent(e.target.value)}><option value="">—</option>{(students.data ?? []).map((s) => <option key={s.id} value={s.id}>{s.first_name} {s.last_name}</option>)}</Select></Field>
          <Field label={t('learning.assess.term')} htmlFor="st-term" hint={t('learning.assess.termHint')} required><Input id="st-term" value={term} maxLength={60} onChange={(e) => setTerm(e.target.value)} /></Field>
          <div className="flex justify-end gap-2 border-t pt-4"><Button type="button" variant="secondary" onClick={onClose}>{t('common.cancel')}</Button><Button type="submit" disabled={start.isPending}>{t('learning.assess.start')}</Button></div>
        </form>
      </DialogContent>
    </Dialog>
  )
}

function RunDialog({ id, canApprove, onClose }: { id: string; canApprove: boolean; onClose: () => void }) {
  const { t, i18n } = useTranslation()
  const lang = i18n.language
  const message = useApiErrorMessage()
  const run = useAssessmentRun(id)
  const save = useSaveScores(id)
  const complete = useCompleteRun(id)
  const approve = useApproveRun(id)
  const [scores, setScores] = useState<Record<string, { score: number; note: string }>>({})
  const [parentEn, setParentEn] = useState<string | null>(null)
  const [parentAr, setParentAr] = useState<string | null>(null)
  const [staff, setStaff] = useState<string | null>(null)
  const [failure, setFailure] = useState<string | null>(null)
  const data = run.data

  if (!data) return <Dialog open onOpenChange={(o) => !o && onClose()}><DialogContent closeLabel={t('common.close')}><Skeleton className="h-40" /></DialogContent></Dialog>

  const max = data.scoring_kind === 'binary' ? 1 : 2
  const current = (m: string, fallback: number, note: string | null) => scores[m] ?? { score: fallback, note: note ?? '' }
  const doSave = async () => {
    setFailure(null)
    try {
      await save.mutateAsync({
        scores: data.scores.map((s) => ({ milestone_id: s.milestone_id, score: current(s.milestone_id, s.score, s.note).score, note: current(s.milestone_id, s.score, s.note).note })),
        staff_summary: { en: staff ?? data.staff_summary?.en ?? '' },
        parent_summary: { en: parentEn ?? data.parent_summary?.en ?? '', ar: parentAr ?? data.parent_summary?.ar ?? '' },
      })
      toast.success(t('learning.assess.saved'))
    } catch (error) {
      setFailure(message(error))
    }
  }
  const act = async (fn: () => Promise<unknown>, ok: string) => {
    setFailure(null)
    try { await fn(); toast.success(ok) } catch (error) { setFailure(message(error)) }
  }

  return (
    <Dialog open onOpenChange={(o) => !o && onClose()}>
      <DialogContent closeLabel={t('common.close')} className="max-w-3xl">
        <DialogHeader title={`${data.student_name ?? ''} · ${localized(data.template_name, lang)} · ${data.term}`} />
        <div className="max-h-[72vh] space-y-4 overflow-y-auto pe-1">
          {failure && <Alert>{failure}</Alert>}
          <div className="flex flex-wrap items-center gap-2 text-sm">
            <Badge tone={data.status === 'completed' ? 'success' : 'neutral'}>{t(`learning.assess.status.${data.status}`)}</Badge>
            <Badge tone={data.parent_approved ? 'success' : 'warning'}>{data.parent_approved ? t('learning.assess.approved') : t('learning.assess.notApproved')}</Badge>
            <span className="text-muted-foreground">{t('learning.assess.progress', { percent: data.progress_percent })}</span>
          </div>
          <ul className="divide-y rounded-md border">
            {data.scores.map((s) => {
              const c = current(s.milestone_id, s.score, s.note)
              return (
                <li key={s.milestone_id} className="grid gap-2 p-3 sm:grid-cols-[1fr_9rem_1fr]">
                  <span className="text-sm">{localized(s.statement, lang)}</span>
                  <Select aria-label={localized(s.statement, lang)} value={c.score} onChange={(e) => setScores((p) => ({ ...p, [s.milestone_id]: { ...c, score: Number(e.target.value) } }))}>
                    {Array.from({ length: max + 1 }, (_, v) => <option key={v} value={v}>{t(`learning.assess.${data.scoring_kind}.${v}`)}</option>)}
                  </Select>
                  <Input aria-label={t('learning.assess.note')} placeholder={t('learning.assess.note')} value={c.note} maxLength={500} onChange={(e) => setScores((p) => ({ ...p, [s.milestone_id]: { ...c, note: e.target.value } }))} />
                </li>
              )
            })}
          </ul>
          <div className="grid gap-3 sm:grid-cols-2">
            <Field label={t('learning.assess.parentEn')} htmlFor="rd-pe" hint={t('learning.assess.parentHint')}><Textarea id="rd-pe" value={parentEn ?? data.parent_summary?.en ?? ''} onChange={(e) => setParentEn(e.target.value)} /></Field>
            <Field label={t('learning.assess.parentAr')} htmlFor="rd-pa"><Textarea id="rd-pa" dir="rtl" lang="ar" value={parentAr ?? data.parent_summary?.ar ?? ''} onChange={(e) => setParentAr(e.target.value)} /></Field>
          </div>
          <Field label={t('learning.assess.staffNotes')} htmlFor="rd-st"><Textarea id="rd-st" value={staff ?? data.staff_summary?.en ?? ''} onChange={(e) => setStaff(e.target.value)} /></Field>
          <div className="flex flex-wrap justify-end gap-2 border-t pt-4">
            <Button variant="secondary" onClick={() => void doSave()} disabled={save.isPending}>{t('learning.assess.save')}</Button>
            {data.status !== 'completed' && <Button variant="secondary" onClick={() => void act(() => complete.mutateAsync(undefined), t('learning.assess.completed'))}><ClipboardCheck className="h-4 w-4" aria-hidden="true" />{t('learning.assess.complete')}</Button>}
            {canApprove && data.status === 'completed' && !data.parent_approved && <Button onClick={() => void act(() => approve.mutateAsync(undefined), t('learning.assess.approvedToast'))}><CheckCircle2 className="h-4 w-4" aria-hidden="true" />{t('learning.assess.approve')}</Button>}
          </div>
        </div>
      </DialogContent>
    </Dialog>
  )
}

export function AssessmentsTab({ canWrite, canApprove }: { canWrite: boolean; canApprove: boolean }) {
  const { t, i18n } = useTranslation()
  const lang = i18n.language
  const templates = useAssessmentTemplates()
  const runs = useAssessmentRuns({})
  const [dialog, setDialog] = useState<'template' | 'start' | null>(null)
  const [openRun, setOpenRun] = useState<string | null>(null)
  const rows = flatten(runs.data)

  return (
    <>
      <div className="mb-4 flex flex-wrap items-center gap-3">
        {canWrite && <Button onClick={() => setDialog('start')} disabled={(templates.data ?? []).length === 0}><Plus className="h-4 w-4" aria-hidden="true" />{t('learning.assess.start')}</Button>}
        {canApprove && <Button variant="secondary" onClick={() => setDialog('template')}>{t('learning.assess.newTemplate')}</Button>}
        <span className="text-sm text-muted-foreground">{t('learning.assess.templates', { count: (templates.data ?? []).length })}</span>
      </div>
      {runs.isLoading ? <ListSkeleton /> : runs.isError ? <ListError error={runs.error} onRetry={() => void runs.refetch()} /> : rows.length === 0 ? (
        <EmptyState title={t('learning.assess.empty')} description={(templates.data ?? []).length === 0 ? t('learning.assess.emptyNoTemplate') : t('learning.assess.emptyHint')} />
      ) : (
        <div className="grid gap-3 md:grid-cols-2">
          {rows.map((r) => (
            <Card key={r.id} className="p-4">
              <div className="flex flex-wrap items-center gap-2 text-xs">
                <Badge tone={r.status === 'completed' ? 'success' : 'neutral'}>{t(`learning.assess.status.${r.status}`)}</Badge>
                <Badge tone={r.parent_approved ? 'success' : 'warning'}>{r.parent_approved ? t('learning.assess.approved') : t('learning.assess.notApproved')}</Badge>
                <span className="text-muted-foreground">{formatDate(r.assessed_on, lang)}</span>
              </div>
              <h3 className="mt-1 font-bold">{r.student_name}</h3>
              <p className="text-sm text-muted-foreground">{localized(r.template_name, lang)} · {r.term} · {r.progress_percent}%</p>
              <div className="mt-3 border-t pt-3"><Button size="sm" variant="secondary" onClick={() => setOpenRun(r.id)}>{t('learning.assess.open')}</Button></div>
            </Card>
          ))}
        </div>
      )}
      <LoadMore visible={!!runs.hasNextPage} loading={runs.isFetchingNextPage} onClick={() => void runs.fetchNextPage()} />
      {dialog === 'template' && <TemplateDialog onClose={() => setDialog(null)} />}
      {dialog === 'start' && <StartDialog onClose={() => setDialog(null)} onStarted={(id) => { setDialog(null); setOpenRun(id) }} />}
      {openRun && <RunDialog id={openRun} canApprove={canApprove} onClose={() => setOpenRun(null)} />}
    </>
  )
}

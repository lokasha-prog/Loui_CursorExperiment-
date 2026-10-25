import { Eye, Plus, Trash2 } from 'lucide-react'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { flatten, useDeleteObservation, useFramework, useFrameworks, useObservations, useSaveObservation, useStudentOptions, useSuggestMilestones } from '@/api/hooks'
import { ListError, ListSkeleton, LoadMore } from '@/components/shared/ListState'
import { Button } from '@/components/ui/button'
import { Field, Select, Textarea } from '@/components/ui/form-controls'
import { Alert, Badge, Card, Dialog, DialogContent, DialogHeader, EmptyState } from '@/components/ui/surfaces'
import { toast } from '@/components/ui/toaster'
import { useApiErrorMessage } from '@/hooks/use-api-error'
import { formatDate } from '@/lib/format'
import { localized } from '@/lib/utils'
import { confirmAction } from '@/components/shared/confirm'

const tone = { formal: 'info', informal: 'neutral', concern: 'danger' } as const

function ObservationDialog({ onClose }: { onClose: () => void }) {
  const { t, i18n } = useTranslation()
  const lang = i18n.language
  const message = useApiErrorMessage()
  const students = useStudentOptions()
  const frameworks = useFrameworks()
  const save = useSaveObservation()
  const [student, setStudent] = useState('')
  const [type, setType] = useState('informal')
  const [fw, setFw] = useState('')
  const [area, setArea] = useState('')
  const [band, setBand] = useState('')
  const [notesEn, setNotesEn] = useState('')
  const [notesAr, setNotesAr] = useState('')
  const [share, setShare] = useState(false)
  const [picked, setPicked] = useState<string[]>([])
  const [failure, setFailure] = useState<string | null>(null)
  const detail = useFramework(fw)
  const suggest = useSuggestMilestones()
  const suggestNow = async () => {
    const text = `${notesEn} ${notesAr}`.trim()
    if (!fw || !text) return setFailure(t('learning.obs.suggestNeeds'))
    setFailure(null)
    const found = await suggest.mutateAsync({ frameworkId: fw, text, areaId: area, ageBandId: band })
    setPicked((p) => Array.from(new Set([...p, ...found.map((f) => f.milestone_id)])))
    toast.success(t('learning.obs.suggested', { count: found.length }))
  }
  const milestones = (detail.data?.milestones ?? []).filter((m) => (!area || m.area_id === area) && (!band || m.age_band_id === band))

  const submit = async () => {
    if (!student || (!notesEn.trim() && !notesAr.trim())) return setFailure(t('validation.required'))
    setFailure(null)
    try {
      await save.mutateAsync({
        student_id: student, type, framework_id: fw || null, area_id: area || null, age_band_id: band || null, milestone_ids: picked,
        notes: { ...(notesEn.trim() ? { en: notesEn.trim() } : {}), ...(notesAr.trim() ? { ar: notesAr.trim() } : {}) }, share_with_parents: share && type !== 'concern',
      })
      toast.success(t('learning.obs.saved'))
      onClose()
    } catch (error) {
      setFailure(message(error))
    }
  }

  return (
    <Dialog open onOpenChange={(o) => !o && onClose()}>
      <DialogContent closeLabel={t('common.close')} className="max-w-2xl">
        <DialogHeader title={t('learning.obs.new')} />
        <form onSubmit={(e) => { e.preventDefault(); void submit() }} className="max-h-[70vh] space-y-4 overflow-y-auto pe-1" noValidate>
          {failure && <Alert>{failure}</Alert>}
          <div className="grid gap-3 sm:grid-cols-2">
            <Field label={t('learning.obs.child')} htmlFor="ob-st" required><Select id="ob-st" value={student} onChange={(e) => setStudent(e.target.value)}><option value="">—</option>{(students.data ?? []).map((s) => <option key={s.id} value={s.id}>{s.first_name} {s.last_name}</option>)}</Select></Field>
            <Field label={t('learning.obs.type')} htmlFor="ob-type"><Select id="ob-type" value={type} onChange={(e) => setType(e.target.value)}>{['informal', 'formal', 'concern'].map((k) => <option key={k} value={k}>{t(`learning.obs.types.${k}`)}</option>)}</Select></Field>
            <Field label={t('learning.framework')} htmlFor="ob-fw"><Select id="ob-fw" value={fw} onChange={(e) => { setFw(e.target.value); setArea(''); setBand(''); setPicked([]) }}><option value="">—</option>{(frameworks.data ?? []).map((f) => <option key={f.id} value={f.id}>{localized(f.name, lang)}</option>)}</Select></Field>
            <Field label={t('learning.area')} htmlFor="ob-area"><Select id="ob-area" value={area} onChange={(e) => setArea(e.target.value)}><option value="">—</option>{(detail.data?.areas ?? []).map((a) => <option key={a.id} value={a.id}>{localized(a.name, lang)}</option>)}</Select></Field>
          </div>
          <div className="grid gap-3 sm:grid-cols-2">
            <Field label={t('learning.obs.notesEn')} htmlFor="ob-en" required><Textarea id="ob-en" value={notesEn} onChange={(e) => setNotesEn(e.target.value)} /></Field>
            <Field label={t('learning.obs.notesAr')} htmlFor="ob-ar"><Textarea id="ob-ar" dir="rtl" lang="ar" value={notesAr} onChange={(e) => setNotesAr(e.target.value)} /></Field>
          </div>
          <div className="flex items-center gap-2"><Button type="button" size="sm" variant="secondary" onClick={() => void suggestNow()} disabled={suggest.isPending}>{t('learning.obs.suggest')}</Button><span className="text-xs text-muted-foreground">{t('learning.obs.suggestHint')}</span></div>
          {milestones.length > 0 && (
            <fieldset className="rounded-md border p-3">
              <legend className="px-1 text-sm font-bold">{t('learning.obs.milestones')}</legend>
              <ul className="max-h-36 space-y-1 overflow-y-auto text-sm">
                {milestones.map((m) => <li key={m.id}><label className="flex items-start gap-2"><input type="checkbox" className="mt-1" checked={picked.includes(m.id)} onChange={(e) => setPicked((p) => (e.target.checked ? [...p, m.id] : p.filter((x) => x !== m.id)))} /><span>{localized(m.statement, lang)}</span></label></li>)}
              </ul>
            </fieldset>
          )}
          <label className="flex items-center gap-2 text-sm font-semibold"><input type="checkbox" checked={share && type !== 'concern'} disabled={type === 'concern'} onChange={(e) => setShare(e.target.checked)} />{type === 'concern' ? t('learning.obs.concernPrivate') : t('learning.obs.share')}</label>
          <div className="flex justify-end gap-2 border-t pt-4"><Button type="button" variant="secondary" onClick={onClose}>{t('common.cancel')}</Button><Button type="submit" disabled={save.isPending}>{t('common.save')}</Button></div>
        </form>
      </DialogContent>
    </Dialog>
  )
}

export function ObservationsTab({ canWrite }: { canWrite: boolean }) {
  const { t, i18n } = useTranslation()
  const lang = i18n.language
  const students = useStudentOptions()
  const [student, setStudent] = useState('')
  const [type, setType] = useState('')
  const query = useObservations({ studentId: student, type })
  const remove = useDeleteObservation()
  const [open, setOpen] = useState(false)
  const rows = flatten(query.data)

  return (
    <>
      <div className="mb-4 flex flex-wrap items-end gap-3">
        <Select value={student} onChange={(e) => setStudent(e.target.value)} aria-label={t('learning.obs.child')} className="w-56"><option value="">{t('learning.obs.allChildren')}</option>{(students.data ?? []).map((s) => <option key={s.id} value={s.id}>{s.first_name} {s.last_name}</option>)}</Select>
        <Select value={type} onChange={(e) => setType(e.target.value)} aria-label={t('learning.obs.type')} className="w-44"><option value="">{t('learning.obs.allTypes')}</option>{['informal', 'formal', 'concern'].map((k) => <option key={k} value={k}>{t(`learning.obs.types.${k}`)}</option>)}</Select>
        {canWrite && <Button onClick={() => setOpen(true)}><Plus className="h-4 w-4" aria-hidden="true" />{t('learning.obs.new')}</Button>}
      </div>
      {query.isLoading ? <ListSkeleton /> : query.isError ? <ListError error={query.error} onRetry={() => void query.refetch()} /> : rows.length === 0 ? (
        <EmptyState icon={<Eye className="h-7 w-7" aria-hidden="true" />} title={t('learning.obs.empty')} description={t('learning.obs.emptyHint')} />
      ) : (
        <div className="grid gap-3 md:grid-cols-2">
          {rows.map((o) => (
            <Card key={o.id} className="p-4">
              <div className="flex flex-wrap items-center gap-2 text-xs">
                <Badge tone={tone[o.type]}>{t(`learning.obs.types.${o.type}`)}</Badge>
                {o.share_with_parents && <Badge tone="success">{t('learning.obs.shared')}</Badge>}
                <span className="text-muted-foreground">{formatDate(o.observed_at, lang)}</span>
              </div>
              <h3 className="mt-1 font-bold">{o.student_name}</h3>
              <p className="mt-1 text-sm">{localized(o.notes, lang)}</p>
              <p className="mt-2 text-xs text-muted-foreground">{t('learning.obs.by', { name: o.observer_name ?? '—' })}</p>
              {canWrite && <div className="mt-2 border-t pt-2"><Button size="sm" variant="ghost" onClick={() => void confirmAction({ title: t('learning.obs.confirmDelete'), confirmLabel: t('confirm.delete') }).then((ok) => { if (ok) remove.mutate(o.id, { onSuccess: () => toast.success(t('learning.obs.deleted')) }) })}><Trash2 className="h-4 w-4" aria-hidden="true" />{t('common.delete')}</Button></div>}
            </Card>
          ))}
        </div>
      )}
      <LoadMore visible={!!query.hasNextPage} loading={query.isFetchingNextPage} onClick={() => void query.fetchNextPage()} />
      {open && <ObservationDialog onClose={() => setOpen(false)} />}
    </>
  )
}

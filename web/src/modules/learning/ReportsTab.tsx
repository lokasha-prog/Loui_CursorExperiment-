import { Download } from 'lucide-react'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { api } from '@/api/client'
import { flatten, useAssessmentRuns, useAssessmentTemplates, useRoomOptions, useStudentOptions } from '@/api/hooks'
import { Button } from '@/components/ui/button'
import { Field, Input, Select } from '@/components/ui/form-controls'
import { Alert, Card } from '@/components/ui/surfaces'
import { toast } from '@/components/ui/toaster'
import { useApiErrorMessage } from '@/hooks/use-api-error'
import { localized } from '@/lib/utils'

const KINDS = ['progress', 'snapshot', 'comparison', 'class_milestone', 'student_milestone', 'journey'] as const
const STAFF_ONLY = ['comparison', 'class_milestone']

/** Saves a learning report (PDF, Excel for staff) made by the server. */
export async function downloadLearningReport(kind: string, params: Record<string, string | undefined>) {
  const query = new URLSearchParams(Object.entries(params).filter(([, v]) => v) as [string, string][]).toString()
  const file = await api.blob(`/learning/reports/${kind}?${query}`)
  const url = URL.createObjectURL(file)
  const a = document.createElement('a')
  a.href = url
  a.download = `learning-${kind}.${params.format === 'excel' ? 'xlsx' : params.format === 'json' ? 'json' : 'pdf'}`
  a.click()
  window.setTimeout(() => URL.revokeObjectURL(url), 30_000)
}

export function ReportsTab() {
  const { t, i18n } = useTranslation()
  const lang = i18n.language
  const message = useApiErrorMessage()
  const students = useStudentOptions()
  const rooms = useRoomOptions()
  const templates = useAssessmentTemplates()
  const [kind, setKind] = useState<(typeof KINDS)[number]>('progress')
  const [student, setStudent] = useState('')
  const [run, setRun] = useState('')
  const [room, setRoom] = useState('')
  const [template, setTemplate] = useState('')
  const [termA, setTermA] = useState('')
  const [termB, setTermB] = useState('')
  const [variant, setVariant] = useState('staff')
  const [format, setFormat] = useState('pdf')
  const [busy, setBusy] = useState(false)
  const [failure, setFailure] = useState<string | null>(null)
  const runs = useAssessmentRuns({ studentId: kind === 'snapshot' ? student : undefined })
  const staffOnly = STAFF_ONLY.includes(kind)
  const effectiveVariant = staffOnly ? 'staff' : variant

  const go = async () => {
    setFailure(null)
    setBusy(true)
    try {
      await downloadLearningReport(kind, {
        student_id: student, run_id: run, room_id: room, template_id: template, term_a: termA, term_b: termB,
        variant: effectiveVariant, language: lang === 'ar' ? 'ar' : 'en', format: effectiveVariant === 'parent' ? 'pdf' : format,
      })
      toast.success(t('learning.reports.done'))
    } catch (error) {
      setFailure(message(error))
    } finally {
      setBusy(false)
    }
  }

  const needsStudent = ['progress', 'snapshot', 'student_milestone', 'journey'].includes(kind)
  const needsRoom = ['class_milestone'].includes(kind)
  const needsTemplate = ['comparison', 'class_milestone'].includes(kind)
  const ready = (!needsStudent || !!student) && (kind !== 'snapshot' || !!run) && (!needsRoom || !!room) && (!needsTemplate || !!template) && (kind !== 'comparison' || (!!termA && !!termB))

  return (
    <Card className="max-w-3xl p-5">
      <p className="mb-4 text-sm text-muted-foreground">{t('learning.reports.intro')}</p>
      {failure && <Alert>{failure}</Alert>}
      <div className="grid gap-3 sm:grid-cols-2">
        <Field label={t('learning.reports.kind')} htmlFor="rp-kind">
          <Select id="rp-kind" value={kind} onChange={(e) => { setKind(e.target.value as (typeof KINDS)[number]); setRun('') }}>{KINDS.map((k) => <option key={k} value={k}>{t(`learning.reports.kinds.${k}`)}</option>)}</Select>
        </Field>
        <Field label={t('learning.reports.variant')} htmlFor="rp-var" hint={staffOnly ? t('learning.reports.staffOnly') : t('learning.reports.variantHint')}>
          <Select id="rp-var" value={effectiveVariant} disabled={staffOnly} onChange={(e) => setVariant(e.target.value)}><option value="staff">{t('learning.reports.staff')}</option><option value="parent">{t('learning.reports.parent')}</option></Select>
        </Field>
        {needsStudent && <Field label={t('learning.obs.child')} htmlFor="rp-st" required><Select id="rp-st" value={student} onChange={(e) => { setStudent(e.target.value); setRun('') }}><option value="">—</option>{(students.data ?? []).map((s) => <option key={s.id} value={s.id}>{s.first_name} {s.last_name}</option>)}</Select></Field>}
        {kind === 'snapshot' && student && <Field label={t('learning.reports.run')} htmlFor="rp-run" required><Select id="rp-run" value={run} onChange={(e) => setRun(e.target.value)}><option value="">—</option>{flatten(runs.data).map((r) => <option key={r.id} value={r.id}>{localized(r.template_name, lang)} · {r.term}</option>)}</Select></Field>}
        {(needsRoom || kind === 'comparison') && <Field label={t('learning.room')} htmlFor="rp-room" required={needsRoom}><Select id="rp-room" value={room} onChange={(e) => setRoom(e.target.value)}><option value="">{t('learning.allRooms')}</option>{(rooms.data ?? []).map((r) => <option key={r.id} value={r.id}>{localized(r.name, lang)}</option>)}</Select></Field>}
        {needsTemplate && <Field label={t('learning.assess.template')} htmlFor="rp-tpl" required><Select id="rp-tpl" value={template} onChange={(e) => setTemplate(e.target.value)}><option value="">—</option>{(templates.data ?? []).map((x) => <option key={x.id} value={x.id}>{localized(x.name, lang)}</option>)}</Select></Field>}
        {kind === 'comparison' && <Field label={t('learning.reports.termA')} htmlFor="rp-ta" required><Input id="rp-ta" value={termA} onChange={(e) => setTermA(e.target.value)} /></Field>}
        {kind === 'comparison' && <Field label={t('learning.reports.termB')} htmlFor="rp-tb" required><Input id="rp-tb" value={termB} onChange={(e) => setTermB(e.target.value)} /></Field>}
        {effectiveVariant === 'staff' && <Field label={t('learning.reports.format')} htmlFor="rp-fmt"><Select id="rp-fmt" value={format} onChange={(e) => setFormat(e.target.value)}><option value="pdf">PDF</option><option value="excel">Excel</option><option value="json">JSON</option></Select></Field>}
      </div>
      <div className="mt-5 flex justify-end"><Button onClick={() => void go()} disabled={!ready || busy}><Download className="h-4 w-4" aria-hidden="true" />{busy ? t('learning.reports.making') : t('learning.reports.download')}</Button></div>
    </Card>
  )
}

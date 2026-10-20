import { Plus, Trash2 } from 'lucide-react'
import { useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { useAssignToPlan, useFeePlan, useProgramOptions, useSaveFeePlan, useStudentOptions } from '@/api/hooks'
import { Button } from '@/components/ui/button'
import { Field, Input, Select } from '@/components/ui/form-controls'
import { Alert, Dialog, DialogContent, DialogHeader } from '@/components/ui/surfaces'
import { toast } from '@/components/ui/toaster'
import { useApiErrorMessage } from '@/hooks/use-api-error'
import { formatDate, todayIso } from '@/lib/format'
import { localized } from '@/lib/utils'
import { toFils } from './InvoiceDialog'

interface Row {
  en: string
  ar: string
  type: string
  amount: string
  tax: string
}

const TYPES = ['tuition', 'meal', 'transport', 'activity', 'discount', 'other']
const names = (en: string, ar: string): Record<string, string> => (ar.trim() ? { en: en.trim(), ar: ar.trim() } : { en: en.trim() })
const blank = (): Row => ({ en: '', ar: '', type: 'tuition', amount: '', tax: '5' })

/** Create or edit a fee plan (a price list) and put children on it. `planId` null means a new plan. */
export function FeePlanDialog({ open, planId, onClose }: { open: boolean; planId: string | null; onClose: () => void }) {
  const { t, i18n } = useTranslation()
  const detail = useFeePlan(planId)
  const programs = useProgramOptions(open)
  const students = useStudentOptions(open && !!planId, '')
  const save = useSaveFeePlan(planId ?? undefined)
  const assign = useAssignToPlan(planId ?? '')
  const message = useApiErrorMessage()
  const [nameEn, setNameEn] = useState('')
  const [nameAr, setNameAr] = useState('')
  const [period, setPeriod] = useState('monthly')
  const [program, setProgram] = useState('')
  const [status, setStatus] = useState('active')
  const [rows, setRows] = useState<Row[]>([blank()])
  const [picked, setPicked] = useState<string[]>([])
  const [from, setFrom] = useState(todayIso())
  const [failure, setFailure] = useState<string | null>(null)

  useEffect(() => {
    if (!open) return
    setFailure(null)
    setPicked([])
    if (!planId) {
      setNameEn(''); setNameAr(''); setPeriod('monthly'); setProgram(''); setStatus('active'); setRows([blank()])
    } else if (detail.data) {
      const p = detail.data.plan
      setNameEn(p.name.en ?? ''); setNameAr(p.name.ar ?? ''); setPeriod(p.period); setProgram(p.program_id ?? ''); setStatus(p.status)
      setRows(p.components.map((c) => ({ en: c.name.en ?? '', ar: c.name.ar ?? '', type: c.component_type, amount: (c.amount_fils / 100).toFixed(2), tax: c.tax_rate_percent === null ? '' : String(c.tax_rate_percent) })))
    }
  }, [open, planId, detail.data])

  const change = (i: number, patch: Partial<Row>) => setRows((all) => all.map((r, x) => (x === i ? { ...r, ...patch } : r)))

  const submit = async () => {
    const valid = rows.filter((r) => r.en.trim() && (toFils(r.amount) ?? (r.amount.trim() === '0' ? 0 : null)) !== null)
    if (!nameEn.trim()) return setFailure(t('validation.required'))
    if (!valid.some((r) => r.type !== 'discount' && toFils(r.amount))) return setFailure(t('billing.plan.form.needItem'))
    setFailure(null)
    try {
      await save.mutateAsync({
        name: names(nameEn, nameAr),
        period,
        program_id: program || null,
        status,
        components: valid.map((r) => ({
          name: names(r.en, r.ar),
          component_type: r.type,
          amount_fils: toFils(r.amount) ?? 0,
          tax_rate_percent: r.type === 'discount' || r.tax.trim() === '' ? null : Number(r.tax),
        })),
      })
      toast.success(t('billing.plan.form.saved'))
      onClose()
    } catch (error) {
      setFailure(message(error))
    }
  }

  const addChildren = async () => {
    if (picked.length === 0) return
    try {
      await assign.mutateAsync({ student_ids: picked, effective_from: from })
      toast.success(t('billing.plan.form.assigned'))
      setPicked([])
    } catch (error) {
      toast.error(message(error))
    }
  }

  const onPlan = new Set((detail.data?.students ?? []).filter((s) => !s.effective_to).map((s) => s.student_id))

  return (
    <Dialog open={open} onOpenChange={(o) => !o && onClose()}>
      <DialogContent closeLabel={t('common.close')} className="max-w-2xl">
        <DialogHeader title={t('billing.plan.form.title')} />
        <form onSubmit={(e) => { e.preventDefault(); void submit() }} className="space-y-4" noValidate>
          {failure && <Alert>{failure}</Alert>}
          <div className="grid gap-3 sm:grid-cols-2">
            <Field label={t('billing.plan.form.name')} htmlFor="fp-en" required><Input id="fp-en" value={nameEn} onChange={(e) => setNameEn(e.target.value)} /></Field>
            <Field label={t('billing.plan.form.nameAr')} htmlFor="fp-ar"><Input id="fp-ar" dir="rtl" lang="ar" value={nameAr} onChange={(e) => setNameAr(e.target.value)} /></Field>
            <Field label={t('billing.plan.form.period')} htmlFor="fp-period">
              <Select id="fp-period" value={period} onChange={(e) => setPeriod(e.target.value)}>
                {['monthly', 'termly', 'annual', 'one_time'].map((p) => <option key={p} value={p}>{t(`billing.plan.period.${p}`)}</option>)}
              </Select>
            </Field>
            <Field label={t('billing.plan.form.program')} htmlFor="fp-program">
              <Select id="fp-program" value={program} onChange={(e) => setProgram(e.target.value)}>
                <option value="">—</option>
                {(programs.data ?? []).map((p) => <option key={p.id} value={p.id}>{localized(p.name, i18n.language)}</option>)}
              </Select>
            </Field>
            {planId && (
              <Field label={t('billing.plan.form.status')} htmlFor="fp-status">
                <Select id="fp-status" value={status} onChange={(e) => setStatus(e.target.value)}>
                  <option value="active">{t('billing.plan.form.active')}</option>
                  <option value="inactive">{t('billing.plan.form.inactive')}</option>
                </Select>
              </Field>
            )}
          </div>
          <fieldset className="space-y-2">
            <legend className="mb-1 text-sm font-bold">{t('billing.plan.form.components')}</legend>
            <p className="text-xs text-muted-foreground">{t('billing.plan.form.discountNote')}</p>
            {rows.map((r, i) => (
              <div key={i} className="grid gap-2 rounded-md border p-3 sm:grid-cols-6">
                <Field label={t('billing.plan.form.componentName')} htmlFor={`fc-n-${i}`} className="sm:col-span-2"><Input id={`fc-n-${i}`} value={r.en} onChange={(e) => change(i, { en: e.target.value })} /></Field>
                <Field label={`${t('billing.plan.form.componentName')} (AR)`} htmlFor={`fc-a-${i}`} className="sm:col-span-2"><Input id={`fc-a-${i}`} dir="rtl" lang="ar" value={r.ar} onChange={(e) => change(i, { ar: e.target.value })} /></Field>
                <Field label={t('billing.plan.form.type')} htmlFor={`fc-t-${i}`} className="sm:col-span-2">
                  <Select id={`fc-t-${i}`} value={r.type} onChange={(e) => change(i, { type: e.target.value })}>{TYPES.map((x) => <option key={x} value={x}>{t(`billing.plan.types.${x}`)}</option>)}</Select>
                </Field>
                <Field label={t('billing.plan.form.amount')} htmlFor={`fc-m-${i}`} className="sm:col-span-2"><Input id={`fc-m-${i}`} inputMode="decimal" dir="ltr" className="text-start" value={r.amount} onChange={(e) => change(i, { amount: e.target.value })} /></Field>
                <Field label={t('billing.plan.form.tax')} htmlFor={`fc-x-${i}`}><Input id={`fc-x-${i}`} inputMode="decimal" dir="ltr" className="text-start" value={r.tax} onChange={(e) => change(i, { tax: e.target.value })} /></Field>
                <div className="flex items-end justify-end sm:col-span-3">
                  {rows.length > 1 && <Button type="button" variant="ghost" size="sm" onClick={() => setRows((all) => all.filter((_, x) => x !== i))} aria-label={t('common.delete')}><Trash2 className="h-4 w-4" aria-hidden="true" /></Button>}
                </div>
              </div>
            ))}
            <Button type="button" variant="secondary" size="sm" onClick={() => setRows((all) => [...all, blank()])}><Plus className="h-4 w-4" aria-hidden="true" />{t('billing.plan.form.addComponent')}</Button>
          </fieldset>
          <div className="flex justify-end gap-2 border-t pt-4">
            <Button type="button" variant="secondary" onClick={onClose}>{t('common.cancel')}</Button>
            <Button type="submit" disabled={save.isPending}>{t('billing.plan.form.save')}</Button>
          </div>
        </form>

        {planId && detail.data && (
          <section className="mt-4 space-y-3 border-t pt-4" aria-labelledby="fp-assign">
            <h3 id="fp-assign" className="text-sm font-bold">{t('billing.plan.form.assign')}</h3>
            <div className="grid gap-3 sm:grid-cols-[1fr_auto]">
              <Field label={t('billing.plan.form.assign')} htmlFor="fp-students">
                <Select id="fp-students" multiple size={5} value={picked} onChange={(e) => setPicked(Array.from(e.target.selectedOptions).map((o) => o.value))}>
                  {(students.data ?? []).filter((s) => !onPlan.has(s.id)).map((s) => <option key={s.id} value={s.id}>{s.first_name} {s.last_name}</option>)}
                </Select>
              </Field>
              <div className="space-y-2">
                <Field label={t('billing.plan.form.assignFrom')} htmlFor="fp-from"><Input id="fp-from" type="date" dir="ltr" className="text-start" value={from} onChange={(e) => setFrom(e.target.value)} /></Field>
                <Button type="button" onClick={() => void addChildren()} disabled={picked.length === 0 || assign.isPending}>{t('billing.plan.form.assignButton')}</Button>
              </div>
            </div>
            <div>
              <p className="mb-1 text-xs font-bold text-muted-foreground">{t('billing.plan.form.onPlan')}</p>
              <ul className="flex flex-wrap gap-2 text-xs">
                {detail.data.students.filter((s) => !s.effective_to).map((s) => <li key={s.student_id} className="rounded-full border px-2 py-0.5">{s.name} · {formatDate(s.effective_from, i18n.language)}</li>)}
              </ul>
            </div>
          </section>
        )}
      </DialogContent>
    </Dialog>
  )
}

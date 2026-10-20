import { Plus, Trash2 } from 'lucide-react'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { useCreateInvoice, useStudentOptions } from '@/api/hooks'
import { Button } from '@/components/ui/button'
import { Field, Input, Select } from '@/components/ui/form-controls'
import { Alert, Dialog, DialogContent, DialogHeader } from '@/components/ui/surfaces'
import { toast } from '@/components/ui/toaster'
import { useApiErrorMessage } from '@/hooks/use-api-error'
import { todayIso } from '@/lib/format'
import { toFils } from './InvoiceDialog'

interface Line {
  en: string
  ar: string
  qty: string
  price: string
  tax: string
}

const describe = (en: string, ar: string): Record<string, string> => (ar.trim() ? { en: en.trim(), ar: ar.trim() } : { en: en.trim() })
const blank = (): Line => ({ en: '', ar: '', qty: '1', price: '', tax: '5' })

/** A one-off invoice written by hand (for example a registration fee). Regular fees come from fee plans instead. */
export function NewInvoiceDialog({ open, onOpenChange }: { open: boolean; onOpenChange: (open: boolean) => void }) {
  const { t } = useTranslation()
  const students = useStudentOptions(open, '')
  const create = useCreateInvoice()
  const message = useApiErrorMessage()
  const [student, setStudent] = useState('')
  const [from, setFrom] = useState(todayIso())
  const [to, setTo] = useState(todayIso())
  const [lines, setLines] = useState<Line[]>([blank()])
  const [issue, setIssue] = useState(true)
  const [failure, setFailure] = useState<string | null>(null)

  const change = (index: number, patch: Partial<Line>) => setLines((all) => all.map((l, i) => (i === index ? { ...l, ...patch } : l)))

  const submit = async () => {
    const valid = lines.filter((l) => l.en.trim() && toFils(l.price))
    if (!student) return setFailure(t('billing.form.child'))
    if (valid.length === 0) return setFailure(t('billing.form.needLine'))
    setFailure(null)
    try {
      await create.mutateAsync({
        student_id: student,
        period_start: from,
        period_end: to < from ? from : to,
        issue,
        lines: valid.map((l) => ({
          description: describe(l.en, l.ar),
          quantity: Number(l.qty) > 0 ? Number(l.qty) : 1,
          unit_price_fils: toFils(l.price)!,
          tax_rate_percent: l.tax.trim() === '' ? null : Number(l.tax),
        })),
      })
      toast.success(t('billing.form.created'))
      setLines([blank()])
      setStudent('')
      onOpenChange(false)
    } catch (error) {
      setFailure(message(error))
    }
  }

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent closeLabel={t('common.close')} className="max-w-2xl">
        <DialogHeader title={t('billing.form.title')} />
        <form onSubmit={(e) => { e.preventDefault(); void submit() }} className="space-y-4" noValidate>
          {failure && <Alert>{failure}</Alert>}
          <div className="grid gap-3 sm:grid-cols-3">
            <Field label={t('billing.form.child')} htmlFor="ni-child" required className="sm:col-span-3">
              <Select id="ni-child" value={student} onChange={(e) => setStudent(e.target.value)}>
                <option value="">—</option>
                {(students.data ?? []).map((s) => <option key={s.id} value={s.id}>{s.first_name} {s.last_name}</option>)}
              </Select>
            </Field>
            <Field label={t('billing.form.from')} htmlFor="ni-from"><Input id="ni-from" type="date" dir="ltr" className="text-start" value={from} onChange={(e) => setFrom(e.target.value)} /></Field>
            <Field label={t('billing.form.to')} htmlFor="ni-to"><Input id="ni-to" type="date" dir="ltr" className="text-start" value={to} onChange={(e) => setTo(e.target.value)} /></Field>
          </div>
          <div className="space-y-3">
            {lines.map((line, i) => (
              <div key={i} className="grid gap-2 rounded-md border p-3 sm:grid-cols-6">
                <Field label={t('billing.form.description')} htmlFor={`ni-d-${i}`} className="sm:col-span-3"><Input id={`ni-d-${i}`} value={line.en} onChange={(e) => change(i, { en: e.target.value })} /></Field>
                <Field label={t('billing.form.descriptionAr')} htmlFor={`ni-a-${i}`} className="sm:col-span-3"><Input id={`ni-a-${i}`} dir="rtl" lang="ar" value={line.ar} onChange={(e) => change(i, { ar: e.target.value })} /></Field>
                <Field label={t('billing.form.qty')} htmlFor={`ni-q-${i}`}><Input id={`ni-q-${i}`} inputMode="decimal" dir="ltr" className="text-start" value={line.qty} onChange={(e) => change(i, { qty: e.target.value })} /></Field>
                <Field label={t('billing.form.price')} htmlFor={`ni-p-${i}`} className="sm:col-span-2"><Input id={`ni-p-${i}`} inputMode="decimal" dir="ltr" className="text-start" value={line.price} onChange={(e) => change(i, { price: e.target.value })} /></Field>
                <Field label={t('billing.form.tax')} htmlFor={`ni-t-${i}`}><Input id={`ni-t-${i}`} inputMode="decimal" dir="ltr" className="text-start" value={line.tax} onChange={(e) => change(i, { tax: e.target.value })} /></Field>
                <div className="flex items-end justify-end">
                  {lines.length > 1 && <Button type="button" variant="ghost" size="sm" onClick={() => setLines((all) => all.filter((_, x) => x !== i))} aria-label={t('common.delete')}><Trash2 className="h-4 w-4" aria-hidden="true" /></Button>}
                </div>
              </div>
            ))}
            <Button type="button" variant="secondary" size="sm" onClick={() => setLines((all) => [...all, blank()])}><Plus className="h-4 w-4" aria-hidden="true" />{t('billing.form.addLine')}</Button>
          </div>
          <label className="flex items-center gap-2 text-sm"><input type="checkbox" className="h-4 w-4 accent-primary" checked={issue} onChange={(e) => setIssue(e.target.checked)} />{t('billing.form.issueNow')}</label>
          <div className="flex justify-end gap-2 border-t pt-4">
            <Button type="button" variant="secondary" onClick={() => onOpenChange(false)}>{t('common.cancel')}</Button>
            <Button type="submit" disabled={create.isPending}>{t('billing.form.save')}</Button>
          </div>
        </form>
      </DialogContent>
    </Dialog>
  )
}

import { Ban, Download, Send, Check } from 'lucide-react'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { ApiError, api } from '@/api/client'
import { useInvoice, useInvoiceAction, useRecordPayment } from '@/api/hooks'
import { Button } from '@/components/ui/button'
import { Field, Input, Select } from '@/components/ui/form-controls'
import { Alert, Badge, Dialog, DialogContent, DialogHeader, Skeleton, type Tone } from '@/components/ui/surfaces'
import { toast } from '@/components/ui/toaster'
import { useApiErrorMessage } from '@/hooks/use-api-error'
import { formatAed, formatDate, todayIso } from '@/lib/format'
import { localized } from '@/lib/utils'
import { can, useAuth } from '@/stores/auth'

export const statusTone: Record<string, Tone> = { draft: 'neutral', issued: 'info', partially_paid: 'warning', paid: 'success', overdue: 'danger', cancelled: 'neutral' }

/** Turns what a person types ("1,250.50") into whole fils, or null when it is not a valid amount. */
export function toFils(text: string): number | null {
  const value = Number(text.replace(/,/g, '').trim())
  return Number.isFinite(value) && value > 0 ? Math.round(value * 100) : null
}

function PaymentForm({ id, balance, onDone }: { id: string; balance: number; onDone: () => void }) {
  const { t } = useTranslation()
  const record = useRecordPayment()
  const message = useApiErrorMessage()
  const [amount, setAmount] = useState((balance / 100).toFixed(2))
  const [date, setDate] = useState(todayIso())
  const [method, setMethod] = useState('cash')
  const [reference, setReference] = useState('')
  const [failure, setFailure] = useState<string | null>(null)

  const submit = async () => {
    const fils = toFils(amount)
    if (!fils) return setFailure(t('validation.positive'))
    setFailure(null)
    try {
      await record.mutateAsync({ id, amount_fils: fils, payment_date: date, method, reference: reference.trim() || null })
      toast.success(t('billing.payment.saved'))
      onDone()
    } catch (error) {
      setFailure(error instanceof ApiError && error.status === 400 ? t('billing.payment.tooMuch') : message(error))
    }
  }

  return (
    <form onSubmit={(e) => { e.preventDefault(); void submit() }} className="mt-3 space-y-3 rounded-md border bg-slate-50/60 p-3" noValidate>
      <h3 className="text-sm font-bold">{t('billing.payment.title')}</h3>
      {failure && <Alert>{failure}</Alert>}
      <div className="grid gap-3 sm:grid-cols-2">
        <Field label={t('billing.payment.amount')} htmlFor="pay-amount">
          <Input id="pay-amount" inputMode="decimal" dir="ltr" className="text-start" value={amount} onChange={(e) => setAmount(e.target.value)} />
        </Field>
        <Field label={t('billing.payment.date')} htmlFor="pay-date">
          <Input id="pay-date" type="date" max={todayIso()} dir="ltr" className="text-start" value={date} onChange={(e) => setDate(e.target.value)} />
        </Field>
        <Field label={t('billing.payment.method')} htmlFor="pay-method">
          <Select id="pay-method" value={method} onChange={(e) => setMethod(e.target.value)}>
            {['cash', 'cheque', 'bank_transfer', 'card', 'other'].map((m) => <option key={m} value={m}>{t(`billing.payment.methods.${m}`)}</option>)}
          </Select>
        </Field>
        <Field label={t('billing.payment.reference')} htmlFor="pay-ref">
          <Input id="pay-ref" dir="ltr" className="text-start" maxLength={100} value={reference} onChange={(e) => setReference(e.target.value)} />
        </Field>
      </div>
      <div className="flex justify-end"><Button type="submit" disabled={record.isPending}><Check className="h-4 w-4" aria-hidden="true" />{t('billing.payment.save')}</Button></div>
    </form>
  )
}

/** One invoice: what it is for, what has been paid, and the actions allowed for the person looking at it. */
export function InvoiceDialog({ id, onClose }: { id: string | null; onClose: () => void }) {
  const { t, i18n } = useTranslation()
  const me = useAuth((s) => s.me)
  const detail = useInvoice(id)
  const action = useInvoiceAction()
  const message = useApiErrorMessage()
  const canWrite = can(me, 'billing.write')
  const canPay = can(me, 'payments.record')
  const d = detail.data
  const inv = d?.invoice
  const open = !!inv && ['issued', 'partially_paid', 'overdue'].includes(inv.status)

  const run = async (kind: 'issue' | 'cancel' | 'send') => {
    if (kind === 'cancel' && !window.confirm(t('billing.invoice.confirmCancel'))) return
    try {
      await action.mutateAsync({ id: id!, action: kind })
      toast.success(t(`billing.invoice.${kind === 'issue' ? 'issued' : kind === 'cancel' ? 'cancelled' : 'sent'}`))
    } catch (error) {
      toast.error(message(error))
    }
  }

  const pdf = async () => {
    try {
      const blob = await api.blob(`/invoices/${id}/pdf?language=${i18n.language === 'ar' ? 'ar' : 'en'}`)
      const url = URL.createObjectURL(blob)
      window.open(url, '_blank', 'noopener')
      setTimeout(() => URL.revokeObjectURL(url), 60_000)
    } catch (error) {
      toast.error(message(error))
    }
  }

  return (
    <Dialog open={!!id} onOpenChange={(o) => !o && onClose()}>
      <DialogContent closeLabel={t('common.close')} className="max-w-2xl">
        <DialogHeader title={inv ? t('billing.invoice.title', { number: inv.invoice_number }) : t('common.loading')} description={inv ? `${inv.student_name ?? ''}` : undefined} />
        {!d || !inv ? <Skeleton className="h-48" /> : (
          <div className="space-y-4">
            <div className="flex flex-wrap items-center gap-2">
              <Badge tone={statusTone[inv.status]}>{t(`billing.status.${inv.status}`)}</Badge>
              <span className="text-xs text-muted-foreground">{t('billing.invoice.period')}: {formatDate(inv.period_start, i18n.language)} – {formatDate(inv.period_end, i18n.language)}</span>
              <span className="text-xs text-muted-foreground">{t('billing.invoice.dueOn', { date: formatDate(inv.due_date, i18n.language) })}</span>
            </div>
            <section aria-labelledby="inv-lines">
              <h3 id="inv-lines" className="mb-1 text-sm font-bold">{t('billing.invoice.lines')}</h3>
              <ul className="divide-y rounded-md border text-sm">
                {d.lines.map((l) => (
                  <li key={l.id} className="flex items-center justify-between gap-3 px-3 py-2">
                    <span>{localized(l.description, i18n.language)}{l.quantity !== 1 && <span className="text-muted-foreground"> × {l.quantity}</span>}</span>
                    <span dir="ltr">{formatAed(l.total_fils, i18n.language)}</span>
                  </li>
                ))}
              </ul>
            </section>
            <dl className="ms-auto w-full max-w-xs space-y-1 text-sm">
              <div className="flex justify-between"><dt>{t('billing.invoice.subtotal')}</dt><dd dir="ltr">{formatAed(d.subtotal_fils, i18n.language)}</dd></div>
              {d.discount_fils > 0 && <div className="flex justify-between"><dt>{t('billing.invoice.discount')}</dt><dd dir="ltr">− {formatAed(d.discount_fils, i18n.language)}</dd></div>}
              <div className="flex justify-between"><dt>{t('billing.invoice.tax')}</dt><dd dir="ltr">{formatAed(d.tax_fils, i18n.language)}</dd></div>
              <div className="flex justify-between border-t pt-1 font-bold"><dt>{t('billing.invoice.total')}</dt><dd dir="ltr">{formatAed(inv.total_fils, i18n.language)}</dd></div>
              <div className="flex justify-between"><dt>{t('billing.invoice.paid')}</dt><dd dir="ltr">{formatAed(inv.paid_fils, i18n.language)}</dd></div>
              <div className="flex justify-between font-bold"><dt>{t('billing.invoice.balance')}</dt><dd dir="ltr">{formatAed(inv.total_fils - inv.paid_fils, i18n.language)}</dd></div>
            </dl>
            <section aria-labelledby="inv-pay">
              <h3 id="inv-pay" className="mb-1 text-sm font-bold">{t('billing.invoice.payments')}</h3>
              {d.payments.length === 0 ? <p className="text-sm text-muted-foreground">{t('billing.invoice.noPayments')}</p> : (
                <ul className="divide-y rounded-md border text-sm">
                  {d.payments.map((p) => (
                    <li key={p.id} className="flex flex-wrap items-center justify-between gap-2 px-3 py-2">
                      <span>{formatDate(p.payment_date, i18n.language)} · {t(`billing.payment.methods.${p.method}`)}{p.reference && <span dir="ltr" className="text-muted-foreground"> · {p.reference}</span>}</span>
                      <span className="flex items-center gap-2"><span dir="ltr" className="font-semibold">{formatAed(p.amount_fils, i18n.language)}</span>{p.recorded_by_name && <span className="text-xs text-muted-foreground">{t('billing.invoice.by', { name: p.recorded_by_name })}</span>}</span>
                    </li>
                  ))}
                </ul>
              )}
            </section>
            {canPay && open && <PaymentForm id={inv.id} balance={inv.total_fils - inv.paid_fils} onDone={() => undefined} />}
            <div className="flex flex-wrap justify-end gap-2 border-t pt-3">
              <Button variant="secondary" onClick={() => void pdf()}><Download className="h-4 w-4" aria-hidden="true" />{t('billing.invoice.pdf')}</Button>
              {canWrite && inv.status === 'draft' && <Button onClick={() => void run('issue')}><Check className="h-4 w-4" aria-hidden="true" />{t('billing.invoice.issue')}</Button>}
              {canWrite && (open || inv.status === 'paid') && <Button variant="secondary" onClick={() => void run('send')}><Send className="h-4 w-4" aria-hidden="true" />{t('billing.invoice.send')}</Button>}
              {canWrite && inv.status !== 'cancelled' && inv.paid_fils === 0 && <Button variant="destructive" onClick={() => void run('cancel')}><Ban className="h-4 w-4" aria-hidden="true" />{t('billing.invoice.cancel')}</Button>}
            </div>
          </div>
        )}
      </DialogContent>
    </Dialog>
  )
}

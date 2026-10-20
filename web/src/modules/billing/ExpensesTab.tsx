import { Plus, Trash2, Wallet } from 'lucide-react'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { flatten, useDeleteExpense, useExpenseReport, useExpenses, useSaveExpense } from '@/api/hooks'
import type { Expense } from '@/api/types'
import { ListError, ListSkeleton, LoadMore } from '@/components/shared/ListState'
import { Button } from '@/components/ui/button'
import { Field, Input, Select } from '@/components/ui/form-controls'
import { Alert, Card, Dialog, DialogContent, DialogHeader, EmptyState, Table, Td, Th } from '@/components/ui/surfaces'
import { toast } from '@/components/ui/toaster'
import { useApiErrorMessage } from '@/hooks/use-api-error'
import { formatAed, formatDate, todayIso } from '@/lib/format'
import { toFils } from './InvoiceDialog'

export const EXPENSE_CATEGORIES = ['rent', 'utilities', 'salaries', 'supplies', 'food', 'other']

function ExpenseDialog({ expense, onClose }: { expense: Expense | null; onClose: () => void }) {
  const { t } = useTranslation()
  const save = useSaveExpense(expense?.id)
  const message = useApiErrorMessage()
  const [date, setDate] = useState(expense?.expense_date ?? todayIso())
  const [amount, setAmount] = useState(expense ? (expense.amount_fils / 100).toFixed(2) : '')
  const [category, setCategory] = useState(expense?.category ?? 'supplies')
  const [description, setDescription] = useState(expense?.description ?? '')
  const [failure, setFailure] = useState<string | null>(null)

  const submit = async () => {
    const fils = toFils(amount)
    if (!fils || !description.trim()) return setFailure(t('validation.required'))
    setFailure(null)
    try {
      await save.mutateAsync({ expense_date: date, amount_fils: fils, category, description: description.trim(), receipt_media_id: expense?.receipt_media_id ?? null })
      toast.success(t('billing.expenses.saved'))
      onClose()
    } catch (error) {
      setFailure(message(error))
    }
  }

  return (
    <Dialog open onOpenChange={(o) => !o && onClose()}>
      <DialogContent closeLabel={t('common.close')} className="max-w-lg">
        <DialogHeader title={t(expense ? 'billing.expenses.edit' : 'billing.expenses.add')} />
        <form onSubmit={(e) => { e.preventDefault(); void submit() }} className="space-y-4" noValidate>
          {failure && <Alert>{failure}</Alert>}
          <div className="grid gap-3 sm:grid-cols-2">
            <Field label={t('billing.expenses.date')} htmlFor="ex-date"><Input id="ex-date" type="date" dir="ltr" className="text-start" value={date} max={todayIso()} onChange={(e) => setDate(e.target.value)} /></Field>
            <Field label={t('billing.expenses.amount')} htmlFor="ex-amount" required><Input id="ex-amount" inputMode="decimal" dir="ltr" className="text-start" value={amount} onChange={(e) => setAmount(e.target.value)} /></Field>
            <Field label={t('billing.expenses.category')} htmlFor="ex-cat" className="sm:col-span-2">
              <Select id="ex-cat" value={category} onChange={(e) => setCategory(e.target.value)}>{EXPENSE_CATEGORIES.map((c) => <option key={c} value={c}>{t(`billing.expenses.categories.${c}`)}</option>)}</Select>
            </Field>
            <Field label={t('billing.expenses.description')} htmlFor="ex-desc" required className="sm:col-span-2"><Input id="ex-desc" value={description} maxLength={500} onChange={(e) => setDescription(e.target.value)} /></Field>
          </div>
          <div className="flex justify-end gap-2 border-t pt-4">
            <Button type="button" variant="secondary" onClick={onClose}>{t('common.cancel')}</Button>
            <Button type="submit" disabled={save.isPending}>{t('billing.expenses.save')}</Button>
          </div>
        </form>
      </DialogContent>
    </Dialog>
  )
}

/** Centre spending by month, with the monthly total and a split by category (spec 03 §8.5). */
export function ExpensesTab() {
  const { t, i18n } = useTranslation()
  const [month, setMonth] = useState(todayIso().slice(0, 7))
  const [editing, setEditing] = useState<{ expense: Expense | null } | null>(null)
  const list = useExpenses(month)
  const report = useExpenseReport(month)
  const remove = useDeleteExpense()
  const message = useApiErrorMessage()
  const rows = flatten(list.data)
  const money = (v: number) => formatAed(v, i18n.language)

  const del = async (id: string) => {
    if (!window.confirm(t('billing.expenses.confirmDelete'))) return
    try {
      await remove.mutateAsync(id)
    } catch (error) {
      toast.error(message(error))
    }
  }

  return (
    <>
      <div className="mb-4 flex flex-wrap items-end gap-3">
        <Field label={t('billing.expenses.month')} htmlFor="ex-month"><Input id="ex-month" type="month" dir="ltr" className="text-start" value={month} onChange={(e) => setMonth(e.target.value)} /></Field>
        <Button className="ms-auto" onClick={() => setEditing({ expense: null })}><Plus className="h-4 w-4" aria-hidden="true" />{t('billing.expenses.add')}</Button>
      </div>
      {report.data && (
        <Card className="mb-4 p-4">
          <p className="font-heading text-2xl font-bold" dir="ltr">{money(report.data.total_fils)}</p>
          <p className="mb-3 text-[11px] font-bold uppercase tracking-wide text-muted-foreground">{t('billing.expenses.total')}</p>
          <ul className="flex flex-wrap gap-2 text-xs">
            {report.data.by_category.map((c) => (
              <li key={c.category} className="rounded-full border px-2.5 py-1">{t(`billing.expenses.categories.${c.category}`)} · <span dir="ltr">{money(c.total_fils)}</span></li>
            ))}
          </ul>
        </Card>
      )}
      <Card className="overflow-hidden">
        {list.isLoading ? <ListSkeleton /> : list.isError ? <ListError error={list.error} onRetry={() => void list.refetch()} /> :
          rows.length === 0 ? <EmptyState icon={<Wallet className="h-7 w-7" aria-hidden="true" />} title={t('billing.expenses.empty')} description={t('billing.expenses.emptyBody')} /> : (
            <>
              <Table>
                <caption className="sr-only">{t('billing.tabs.expenses')}</caption>
                <thead><tr><Th>{t('billing.expenses.date')}</Th><Th>{t('billing.expenses.category')}</Th><Th>{t('billing.expenses.description')}</Th><Th>{t('billing.expenses.amount')}</Th><Th><span className="sr-only">{t('common.delete')}</span></Th></tr></thead>
                <tbody>
                  {rows.map((r) => (
                    <tr key={r.id}>
                      <Td>{formatDate(r.expense_date, i18n.language)}</Td>
                      <Td>{t(`billing.expenses.categories.${r.category}`)}</Td>
                      <Td>
                        <button type="button" className="text-start font-semibold text-primary-dark underline-offset-4 hover:underline focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring" onClick={() => setEditing({ expense: r })}>{r.description}</button>
                      </Td>
                      <Td dir="ltr" className="text-start">{money(r.amount_fils)}</Td>
                      <Td><Button size="sm" variant="ghost" aria-label={t('common.delete')} onClick={() => void del(r.id)}><Trash2 className="h-4 w-4" aria-hidden="true" /></Button></Td>
                    </tr>
                  ))}
                </tbody>
              </Table>
              <LoadMore visible={!!list.hasNextPage} loading={list.isFetchingNextPage} onClick={() => void list.fetchNextPage()} />
            </>
          )}
      </Card>
      {editing && <ExpenseDialog key={editing.expense?.id ?? 'new'} expense={editing.expense} onClose={() => setEditing(null)} />}
    </>
  )
}

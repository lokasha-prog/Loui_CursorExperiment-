import { CalendarDays, Plus, Trash2 } from 'lucide-react'
import { useMemo, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { useAddBlockOff, useBlockOffs, useDeleteBlockOff, useSetTourStatus, useStaffOptions, useTours } from '@/api/hooks'
import type { Tour } from '@/api/types'
import { ListError, ListSkeleton } from '@/components/shared/ListState'
import { Button } from '@/components/ui/button'
import { Field, Input, Select } from '@/components/ui/form-controls'
import { Alert, Badge, Card, EmptyState, type Tone } from '@/components/ui/surfaces'
import { toast } from '@/components/ui/toaster'
import { useApiErrorMessage } from '@/hooks/use-api-error'
import { todayIso } from '@/lib/format'
import { TourDialog, toInstant } from './TourDialog'

const tone: Record<string, Tone> = { scheduled: 'info', completed: 'success', cancelled: 'neutral', no_show: 'danger' }

function addDays(iso: string, days: number) {
  const d = new Date(`${iso}T00:00:00`)
  d.setDate(d.getDate() + days)
  const pad = (n: number) => String(n).padStart(2, '0')
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}`
}

function BlockOffForm({ onDone }: { onDone: () => void }) {
  const { t } = useTranslation()
  const staff = useStaffOptions()
  const add = useAddBlockOff()
  const message = useApiErrorMessage()
  const [staffId, setStaffId] = useState('')
  const [from, setFrom] = useState('')
  const [to, setTo] = useState('')
  const [reason, setReason] = useState('')
  const [failure, setFailure] = useState<string | null>(null)

  const submit = async () => {
    if (!from || !to) return setFailure(t('validation.required'))
    setFailure(null)
    try {
      await add.mutateAsync({ staff_id: staffId || null, starts_at: toInstant(from), ends_at: toInstant(to), reason: reason.trim() || null })
      toast.success(t('enquiries.tours.blockSaved'))
      onDone()
    } catch (error) {
      setFailure(message(error))
    }
  }

  return (
    <form onSubmit={(e) => { e.preventDefault(); void submit() }} className="mb-4 space-y-3 rounded-md border p-3" noValidate>
      {failure && <Alert>{failure}</Alert>}
      <div className="grid gap-3 sm:grid-cols-4">
        <Field label={t('enquiries.tours.blockStaff')} htmlFor="bo-staff">
          <Select id="bo-staff" value={staffId} onChange={(e) => setStaffId(e.target.value)}>
            <option value="">{t('enquiries.tours.wholeCentre')}</option>
            {(staff.data ?? []).map((s) => <option key={s.id} value={s.id}>{s.first_name} {s.last_name}</option>)}
          </Select>
        </Field>
        <Field label={t('enquiries.tours.blockFrom')} htmlFor="bo-from"><Input id="bo-from" type="datetime-local" dir="ltr" className="text-start" value={from} onChange={(e) => setFrom(e.target.value)} /></Field>
        <Field label={t('enquiries.tours.blockTo')} htmlFor="bo-to"><Input id="bo-to" type="datetime-local" dir="ltr" className="text-start" value={to} onChange={(e) => setTo(e.target.value)} /></Field>
        <Field label={t('enquiries.tours.blockReason')} htmlFor="bo-reason"><Input id="bo-reason" value={reason} maxLength={300} onChange={(e) => setReason(e.target.value)} /></Field>
      </div>
      <div className="flex justify-end gap-2">
        <Button type="button" variant="secondary" onClick={onDone}>{t('common.cancel')}</Button>
        <Button type="submit" disabled={add.isPending}>{t('enquiries.tours.blockSave')}</Button>
      </div>
    </form>
  )
}

/** The booked tours for a week, grouped by day, with the time slots staff have blocked off. */
export function ToursTab() {
  const { t, i18n } = useTranslation()
  const [weekStart, setWeekStart] = useState(todayIso())
  const [staffId, setStaffId] = useState('')
  const [blocking, setBlocking] = useState(false)
  const [editing, setEditing] = useState<Tour | null>(null)
  const staff = useStaffOptions()
  const from = new Date(`${weekStart}T00:00:00`).toISOString()
  const to = new Date(`${addDays(weekStart, 7)}T00:00:00`).toISOString()
  const tours = useTours(from, to, staffId || undefined)
  const blocks = useBlockOffs(from, to)
  const setStatus = useSetTourStatus()
  const removeBlock = useDeleteBlockOff()
  const message = useApiErrorMessage()

  const days = useMemo(() => Array.from({ length: 7 }, (_, i) => addDays(weekStart, i)), [weekStart])
  const time = (iso: string) => new Date(iso).toLocaleTimeString(i18n.language === 'ar' ? 'ar-AE-u-nu-latn' : 'en-GB', { hour: '2-digit', minute: '2-digit' })
  const dayLabel = (iso: string) => new Date(`${iso}T00:00:00`).toLocaleDateString(i18n.language === 'ar' ? 'ar-AE-u-nu-latn' : 'en-GB', { weekday: 'long', day: 'numeric', month: 'short' })
  const sameDay = (iso: string, day: string) => {
    const d = new Date(iso)
    const pad = (n: number) => String(n).padStart(2, '0')
    return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}` === day
  }

  const mark = async (id: string, status: string) => {
    try {
      await setStatus.mutateAsync({ id, status })
    } catch (error) {
      toast.error(message(error))
    }
  }

  return (
    <>
      <div className="mb-4 flex flex-wrap items-end gap-3">
        <Field label={t('enquiries.tours.weekOf')} htmlFor="tw-week"><Input id="tw-week" type="date" dir="ltr" className="text-start" value={weekStart} onChange={(e) => setWeekStart(e.target.value)} /></Field>
        <Field label={t('enquiries.tours.staffFilter')} htmlFor="tw-staff" className="min-w-44">
          <Select id="tw-staff" value={staffId} onChange={(e) => setStaffId(e.target.value)}>
            <option value="">{t('enquiries.tours.everyone')}</option>
            {(staff.data ?? []).map((s) => <option key={s.id} value={s.id}>{s.first_name} {s.last_name}</option>)}
          </Select>
        </Field>
        <Button variant="secondary" className="ms-auto" onClick={() => setBlocking((b) => !b)}><Plus className="h-4 w-4" aria-hidden="true" />{t('enquiries.tours.block')}</Button>
      </div>
      {blocking && <BlockOffForm onDone={() => setBlocking(false)} />}
      {tours.isLoading ? <ListSkeleton /> : tours.isError ? <ListError error={tours.error} onRetry={() => void tours.refetch()} /> : (
        (tours.data?.length ?? 0) === 0 && (blocks.data?.length ?? 0) === 0
          ? <Card><EmptyState icon={<CalendarDays className="h-7 w-7" aria-hidden="true" />} title={t('enquiries.tours.empty')} description={t('enquiries.tours.emptyBody')} /></Card>
          : (
            <div className="space-y-3">
              {days.map((day) => {
                const dayTours = (tours.data ?? []).filter((x) => sameDay(x.starts_at, day))
                const dayBlocks = (blocks.data ?? []).filter((b) => sameDay(b.starts_at, day))
                if (dayTours.length === 0 && dayBlocks.length === 0) return null
                return (
                  <Card key={day} className="p-4">
                    <h3 className="mb-2 font-heading text-base font-semibold">{dayLabel(day)}</h3>
                    <ul className="divide-y text-sm">
                      {dayTours.map((x) => (
                        <li key={x.id} className="flex flex-wrap items-center gap-2 py-2">
                          <span className="font-mono text-xs" dir="ltr">{time(x.starts_at)}</span>
                          <button type="button" className="font-bold text-primary-dark underline-offset-4 hover:underline focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring" onClick={() => setEditing(x)}>{x.family_name}</button>
                          <span className="text-muted-foreground">{x.child_name} · {t(`enquiries.tour.types.${x.tour_type}`)} · {x.staff_name ?? t('enquiries.tours.noStaff')}</span>
                          <Badge tone={tone[x.status]}>{t(`enquiries.tours.status.${x.status}`)}</Badge>
                          {x.status === 'scheduled' && (
                            <span className="ms-auto flex flex-wrap gap-1">
                              <Button size="sm" variant="secondary" onClick={() => void mark(x.id, 'completed')}>{t('enquiries.tours.markDone')}</Button>
                              <Button size="sm" variant="ghost" onClick={() => void mark(x.id, 'no_show')}>{t('enquiries.tours.markNoShow')}</Button>
                              <Button size="sm" variant="ghost" onClick={() => void mark(x.id, 'cancelled')}>{t('enquiries.tours.markCancelled')}</Button>
                            </span>
                          )}
                        </li>
                      ))}
                      {dayBlocks.map((b) => (
                        <li key={b.id} className="flex flex-wrap items-center gap-2 py-2 text-muted-foreground">
                          <span className="font-mono text-xs" dir="ltr">{time(b.starts_at)}–{time(b.ends_at)}</span>
                          <Badge>{t('enquiries.tours.blocked')}</Badge>
                          <span>{b.staff_name ?? t('enquiries.tours.wholeCentre')}{b.reason ? ` · ${b.reason}` : ''}</span>
                          <Button size="sm" variant="ghost" className="ms-auto" aria-label={t('common.delete')} onClick={() => removeBlock.mutate(b.id)}><Trash2 className="h-4 w-4" aria-hidden="true" /></Button>
                        </li>
                      ))}
                    </ul>
                  </Card>
                )
              })}
            </div>
          )
      )}
      {editing && <TourDialog enquiryId={editing.enquiry_id} tour={editing} onClose={() => setEditing(null)} />}
    </>
  )
}

import { Trash2 } from 'lucide-react'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { useCalendarEvent, useDeleteCalendarEvent, useRoomOptions, useSaveCalendarEvent } from '@/api/hooks'
import type { CalendarEventDetail } from '@/api/types'
import { Button } from '@/components/ui/button'
import { Field, Input, Select, Textarea } from '@/components/ui/form-controls'
import { Alert, Dialog, DialogContent, DialogHeader, Skeleton } from '@/components/ui/surfaces'
import { toast } from '@/components/ui/toaster'
import { useApiErrorMessage } from '@/hooks/use-api-error'
import { localized } from '@/lib/utils'

const DAY_CODES = ['MO', 'TU', 'WE', 'TH', 'FR', 'SA', 'SU']
const TYPES = ['regular', 'holiday', 'staff_only']

const pad = (n: number) => String(n).padStart(2, '0')
const datePart = (iso: string) => {
  const d = new Date(iso)
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}`
}
const dateTimePart = (iso: string) => `${datePart(iso)}T${pad(new Date(iso).getHours())}:${pad(new Date(iso).getMinutes())}`

/** Reads back the repeat choice from a stored rule such as FREQ=WEEKLY;BYDAY=TU,TH;UNTIL=20261231. */
export function parseRule(rule: string | null) {
  const parts = Object.fromEntries((rule ?? '').split(';').filter(Boolean).map((p) => p.split('=') as [string, string]))
  const until = parts.UNTIL ? `${parts.UNTIL.slice(0, 4)}-${parts.UNTIL.slice(4, 6)}-${parts.UNTIL.slice(6, 8)}` : ''
  return { freq: (parts.FREQ ?? '').toLowerCase(), days: parts.BYDAY ? parts.BYDAY.split(',') : [], until }
}

export function buildRule(freq: string, days: string[], until: string) {
  if (!freq) return null
  let rule = `FREQ=${freq.toUpperCase()}`
  if (freq === 'weekly' && days.length > 0) rule += `;BYDAY=${days.join(',')}`
  if (until) rule += `;UNTIL=${until.replaceAll('-', '')}`
  return rule
}

function Form({ event, startDate, onClose }: { event: CalendarEventDetail | null; startDate: string; onClose: () => void }) {
  const { t, i18n } = useTranslation()
  const rooms = useRoomOptions()
  const save = useSaveCalendarEvent(event?.id)
  const remove = useDeleteCalendarEvent()
  const message = useApiErrorMessage()
  const rule = parseRule(event?.recurrence ?? null)
  const [titleEn, setTitleEn] = useState(event?.title.en ?? '')
  const [titleAr, setTitleAr] = useState(event?.title.ar ?? '')
  const [descEn, setDescEn] = useState(event?.description?.en ?? '')
  const [descAr, setDescAr] = useState(event?.description?.ar ?? '')
  const [allDay, setAllDay] = useState(event?.is_all_day ?? true)
  const [start, setStart] = useState(event ? (event.is_all_day ? datePart(event.starts_at) : dateTimePart(event.starts_at)) : startDate)
  const [end, setEnd] = useState(event ? (event.is_all_day ? datePart(event.ends_at) : dateTimePart(event.ends_at)) : startDate)
  const [type, setType] = useState(event?.type ?? 'regular')
  const [room, setRoom] = useState(event?.room_id ?? '')
  const [visibility, setVisibility] = useState(event?.visibility ?? 'parents')
  const [freq, setFreq] = useState(rule.freq)
  const [days, setDays] = useState<string[]>(rule.days)
  const [until, setUntil] = useState(rule.until)
  const [failure, setFailure] = useState<string | null>(null)

  const toggleAllDay = (on: boolean) => {
    setAllDay(on)
    setStart((s) => (on ? s.slice(0, 10) : `${s.slice(0, 10)}T09:00`))
    setEnd((s) => (on ? s.slice(0, 10) : `${s.slice(0, 10)}T10:00`))
  }

  const submit = async () => {
    if (!titleEn.trim() || !start || !end) return setFailure(t('validation.required'))
    const startsAt = allDay ? new Date(`${start.slice(0, 10)}T00:00:00`) : new Date(start)
    const endsAt = allDay ? new Date(`${end.slice(0, 10)}T23:59:00`) : new Date(end)
    setFailure(null)
    try {
      await save.mutateAsync({
        title: titleAr.trim() ? { en: titleEn.trim(), ar: titleAr.trim() } : { en: titleEn.trim() },
        description: descEn.trim() || descAr.trim() ? { ...(descEn.trim() ? { en: descEn.trim() } : {}), ...(descAr.trim() ? { ar: descAr.trim() } : {}) } : null,
        starts_at: startsAt.toISOString(), ends_at: endsAt.toISOString(), is_all_day: allDay, type, room_id: room || null,
        visibility: type === 'staff_only' ? 'staff' : visibility, recurrence: buildRule(freq, days, until),
      })
      toast.success(t('calendar.saved'))
      onClose()
    } catch (error) {
      setFailure(message(error))
    }
  }

  const del = async () => {
    if (!event || !window.confirm(t('calendar.confirmDelete'))) return
    try {
      await remove.mutateAsync(event.id)
      onClose()
    } catch (error) {
      setFailure(message(error))
    }
  }

  return (
    <form onSubmit={(e) => { e.preventDefault(); void submit() }} className="space-y-4" noValidate>
      {failure && <Alert>{failure}</Alert>}
      <div className="grid gap-3 sm:grid-cols-2">
        <Field label={t('calendar.form.titleEn')} htmlFor="ev-en" required><Input id="ev-en" value={titleEn} maxLength={200} onChange={(e) => setTitleEn(e.target.value)} /></Field>
        <Field label={t('calendar.form.titleAr')} htmlFor="ev-ar"><Input id="ev-ar" dir="rtl" lang="ar" value={titleAr} maxLength={200} onChange={(e) => setTitleAr(e.target.value)} /></Field>
        <Field label={t('calendar.form.descEn')} htmlFor="ev-den"><Textarea id="ev-den" rows={2} value={descEn} onChange={(e) => setDescEn(e.target.value)} /></Field>
        <Field label={t('calendar.form.descAr')} htmlFor="ev-dar"><Textarea id="ev-dar" rows={2} dir="rtl" lang="ar" value={descAr} onChange={(e) => setDescAr(e.target.value)} /></Field>
        <label className="flex items-center gap-2 text-sm sm:col-span-2"><input type="checkbox" className="h-4 w-4 accent-primary" checked={allDay} onChange={(e) => toggleAllDay(e.target.checked)} />{t('calendar.form.allDay')}</label>
        <Field label={t('calendar.form.start')} htmlFor="ev-start" required><Input id="ev-start" type={allDay ? 'date' : 'datetime-local'} dir="ltr" className="text-start" value={start} onChange={(e) => setStart(e.target.value)} /></Field>
        <Field label={t('calendar.form.end')} htmlFor="ev-end" required><Input id="ev-end" type={allDay ? 'date' : 'datetime-local'} dir="ltr" className="text-start" value={end} onChange={(e) => setEnd(e.target.value)} /></Field>
        <Field label={t('calendar.form.type')} htmlFor="ev-type">
          <Select id="ev-type" value={type} onChange={(e) => setType(e.target.value)}>{TYPES.map((x) => <option key={x} value={x}>{t(`calendar.types.${x}`)}</option>)}</Select>
        </Field>
        <Field label={t('calendar.form.room')} htmlFor="ev-room">
          <Select id="ev-room" value={room} onChange={(e) => setRoom(e.target.value)}>
            <option value="">{t('calendar.form.allRooms')}</option>
            {(rooms.data ?? []).map((r) => <option key={r.id} value={r.id}>{localized(r.name, i18n.language)}</option>)}
          </Select>
        </Field>
        {type !== 'staff_only' && (
          <Field label={t('calendar.form.visibility')} htmlFor="ev-vis">
            <Select id="ev-vis" value={visibility} onChange={(e) => setVisibility(e.target.value)}>
              <option value="parents">{t('calendar.visibility.parents')}</option>
              <option value="staff">{t('calendar.visibility.staff')}</option>
            </Select>
          </Field>
        )}
        <Field label={t('calendar.form.repeat')} htmlFor="ev-repeat">
          <Select id="ev-repeat" value={freq} onChange={(e) => setFreq(e.target.value)}>
            <option value="">{t('calendar.repeat.none')}</option>
            {['daily', 'weekly', 'monthly'].map((f) => <option key={f} value={f}>{t(`calendar.repeat.${f}`)}</option>)}
          </Select>
        </Field>
        {freq && <Field label={t('calendar.form.until')} htmlFor="ev-until"><Input id="ev-until" type="date" dir="ltr" className="text-start" value={until} onChange={(e) => setUntil(e.target.value)} /></Field>}
        {freq === 'weekly' && (
          <fieldset className="sm:col-span-2">
            <legend className="mb-1 text-xs font-bold">{t('calendar.form.days')}</legend>
            <div className="flex flex-wrap gap-3">
              {DAY_CODES.map((d) => (
                <label key={d} className="flex items-center gap-1.5 text-sm">
                  <input type="checkbox" className="h-4 w-4 accent-primary" checked={days.includes(d)} onChange={(e) => setDays(e.target.checked ? [...days, d] : days.filter((x) => x !== d))} />
                  {t(`calendar.days.${d}`)}
                </label>
              ))}
            </div>
          </fieldset>
        )}
      </div>
      <div className="flex flex-wrap justify-end gap-2 border-t pt-4">
        {event && <Button type="button" variant="ghost" className="me-auto" onClick={() => void del()}><Trash2 className="h-4 w-4" aria-hidden="true" />{t('common.delete')}</Button>}
        <Button type="button" variant="secondary" onClick={onClose}>{t('common.cancel')}</Button>
        <Button type="submit" disabled={save.isPending}>{t('calendar.form.save')}</Button>
      </div>
    </form>
  )
}

/** Create or change an event. `id` is empty for a new one, which starts on `startDate`. */
export function EventDialog({ id, startDate, onClose }: { id: string | null; startDate: string; onClose: () => void }) {
  const { t } = useTranslation()
  const detail = useCalendarEvent(id)
  return (
    <Dialog open onOpenChange={(o) => !o && onClose()}>
      <DialogContent closeLabel={t('common.close')} className="max-w-2xl">
        <DialogHeader title={t(id ? 'calendar.edit' : 'calendar.add')} />
        {id && !detail.data ? <Skeleton className="h-64" /> : <Form event={detail.data ?? null} startDate={startDate} onClose={onClose} />}
      </DialogContent>
    </Dialog>
  )
}

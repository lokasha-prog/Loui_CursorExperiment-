import { CalendarPlus, Clock, DoorOpen, Eye, Pencil, Repeat, Trash2 } from 'lucide-react'
import { useTranslation } from 'react-i18next'
import { useDeleteCalendarEvent, useRoomOptions } from '@/api/hooks'
import type { CalendarOccurrence } from '@/api/types'
import { Button } from '@/components/ui/button'
import { Badge, Dialog, DialogContent, DialogHeader, type Tone } from '@/components/ui/surfaces'
import { toast } from '@/components/ui/toaster'
import { useApiErrorMessage } from '@/hooks/use-api-error'
import { localized } from '@/lib/utils'
import { confirmAction } from '@/components/shared/confirm'

const typeTone: Record<string, Tone> = { regular: 'info', holiday: 'danger', staff_only: 'neutral' }

const pad = (n: number) => String(n).padStart(2, '0')
const icsDate = (d: Date) => `${d.getFullYear()}${pad(d.getMonth() + 1)}${pad(d.getDate())}`
const icsUtc = (d: Date) => d.toISOString().replace(/[-:]/g, '').replace(/\.\d{3}/, '')
const icsText = (s: string) => s.replace(/\\/g, '\\\\').replace(/;/g, '\\;').replace(/,/g, '\\,').replace(/\r?\n/g, '\\n')

/** One occurrence as an .ics file that any phone or desktop calendar can open. All-day ends are exclusive, so the last day is included. */
export function buildIcs(event: CalendarOccurrence, title: string, description: string, now = new Date()) {
  const start = new Date(event.starts_at)
  const end = new Date(event.ends_at)
  const dayAfter = new Date(end.getFullYear(), end.getMonth(), end.getDate() + 1)
  const lines = [
    'BEGIN:VCALENDAR',
    'VERSION:2.0',
    'PRODID:-//Bright Futures//NMS//EN',
    'BEGIN:VEVENT',
    `UID:${event.event_id}-${icsDate(start)}@nms`,
    `DTSTAMP:${icsUtc(now)}`,
    ...(event.is_all_day ? [`DTSTART;VALUE=DATE:${icsDate(start)}`, `DTEND;VALUE=DATE:${icsDate(dayAfter)}`] : [`DTSTART:${icsUtc(start)}`, `DTEND:${icsUtc(end)}`]),
    `SUMMARY:${icsText(title)}`,
    ...(description ? [`DESCRIPTION:${icsText(description)}`] : []),
    'END:VEVENT',
    'END:VCALENDAR',
  ]
  return lines.join('\r\n')
}

function whenLabel(event: CalendarOccurrence, locale: string, allDay: string) {
  const start = new Date(event.starts_at)
  const end = new Date(event.ends_at)
  const day = (d: Date) => d.toLocaleDateString(locale, { weekday: 'long', day: 'numeric', month: 'long', year: 'numeric' })
  const time = (d: Date) => d.toLocaleTimeString(locale, { hour: '2-digit', minute: '2-digit' })
  const sameDay = start.toDateString() === end.toDateString()
  if (event.is_all_day) return sameDay ? `${day(start)} · ${allDay}` : `${day(start)} → ${day(end)} · ${allDay}`
  return sameDay ? `${day(start)} · ${time(start)}–${time(end)}` : `${day(start)} ${time(start)} → ${day(end)} ${time(end)}`
}

/** What an event is, with what the viewer can do about it: everyone can add it to their own calendar; managers can change or remove it. */
export function EventDetails({ event, canWrite, onEdit, onClose }: { event: CalendarOccurrence; canWrite: boolean; onEdit: () => void; onClose: () => void }) {
  const { t, i18n } = useTranslation()
  const rooms = useRoomOptions(!!event.room_id)
  const remove = useDeleteCalendarEvent()
  const message = useApiErrorMessage()
  const locale = i18n.language === 'ar' ? 'ar-AE-u-nu-latn' : 'en-GB'
  const title = localized(event.title, i18n.language)
  const description = event.description ? localized(event.description, i18n.language) : ''
  const room = event.room_id ? localized(rooms.data?.find((r) => r.id === event.room_id)?.name, i18n.language) : t('calendar.form.allRooms')

  const download = () => {
    const blob = new Blob([buildIcs(event, title, description)], { type: 'text/calendar;charset=utf-8' })
    const url = URL.createObjectURL(blob)
    const link = document.createElement('a')
    link.href = url
    link.download = `${title.replace(/[^\p{L}\p{N}]+/gu, '-').replace(/^-|-$/g, '') || 'event'}.ics`
    link.click()
    URL.revokeObjectURL(url)
  }

  const del = async () => {
    if (!(await confirmAction({ title: t(event.repeats ? 'calendar.details.confirmDeleteSeries' : 'calendar.confirmDelete'), confirmLabel: t('confirm.remove') }))) return
    try {
      await remove.mutateAsync(event.event_id)
      toast.success(t('calendar.details.deleted'))
      onClose()
    } catch (error) {
      toast.error(message(error))
    }
  }

  return (
    <Dialog open onOpenChange={(o) => !o && onClose()}>
      <DialogContent closeLabel={t('common.close')} className="max-w-lg">
        <DialogHeader title={title} />
        <div className="-mt-3 mb-4 flex flex-wrap gap-2">
          <Badge tone={typeTone[event.type] ?? 'neutral'}>{t(`calendar.types.${event.type}`)}</Badge>
          {event.repeats && <Badge tone="neutral"><Repeat className="me-1 h-3 w-3" aria-hidden="true" />{t('calendar.details.repeats')}</Badge>}
        </div>
        <dl className="space-y-3 text-sm">
          <div className="flex gap-3">
            <dt><Clock className="mt-0.5 h-4 w-4 text-muted-foreground" aria-hidden="true" /><span className="sr-only">{t('calendar.details.when')}</span></dt>
            <dd>{whenLabel(event, locale, t('calendar.form.allDay'))}</dd>
          </div>
          <div className="flex gap-3">
            <dt><DoorOpen className="mt-0.5 h-4 w-4 text-muted-foreground" aria-hidden="true" /><span className="sr-only">{t('calendar.form.room')}</span></dt>
            <dd>{room}</dd>
          </div>
          <div className="flex gap-3">
            <dt><Eye className="mt-0.5 h-4 w-4 text-muted-foreground" aria-hidden="true" /><span className="sr-only">{t('calendar.form.visibility')}</span></dt>
            <dd>{t(`calendar.visibility.${event.visibility}`)}</dd>
          </div>
        </dl>
        {description && <p className="mt-4 whitespace-pre-line rounded-md bg-slate-50 p-3 text-sm">{description}</p>}
        <div className="mt-6 flex flex-wrap justify-end gap-2 border-t pt-4">
          {canWrite && (
            <Button variant="ghost" className="me-auto" onClick={() => void del()} disabled={remove.isPending}>
              <Trash2 className="h-4 w-4" aria-hidden="true" />{t('common.delete')}
            </Button>
          )}
          <Button variant="secondary" onClick={download}><CalendarPlus className="h-4 w-4" aria-hidden="true" />{t('calendar.details.addToMine')}</Button>
          {canWrite && <Button onClick={onEdit}><Pencil className="h-4 w-4" aria-hidden="true" />{t('common.edit')}</Button>}
        </div>
      </DialogContent>
    </Dialog>
  )
}

/** Every event on one day, for when the month cell is too small to show them all. */
export function DayEvents({ date, events, onPick, onClose }: { date: string; events: CalendarOccurrence[]; onPick: (event: CalendarOccurrence) => void; onClose: () => void }) {
  const { t, i18n } = useTranslation()
  const locale = i18n.language === 'ar' ? 'ar-AE-u-nu-latn' : 'en-GB'
  return (
    <Dialog open onOpenChange={(o) => !o && onClose()}>
      <DialogContent closeLabel={t('common.close')} className="max-w-md">
        <DialogHeader title={new Date(`${date}T00:00:00`).toLocaleDateString(locale, { weekday: 'long', day: 'numeric', month: 'long' })} />
        <ul className="space-y-2">
          {events.map((e, k) => (
            <li key={`${e.event_id}-${k}`}>
              <Button variant="secondary" className="w-full justify-between" onClick={() => onPick(e)}>
                <span className="truncate">{localized(e.title, i18n.language)}</span>
                <span className="shrink-0 text-xs text-muted-foreground">
                  {e.is_all_day ? t('calendar.form.allDay') : new Date(e.starts_at).toLocaleTimeString(locale, { hour: '2-digit', minute: '2-digit' })}
                </span>
              </Button>
            </li>
          ))}
        </ul>
      </DialogContent>
    </Dialog>
  )
}

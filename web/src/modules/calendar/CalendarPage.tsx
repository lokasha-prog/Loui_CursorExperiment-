import * as TabsPrimitive from '@radix-ui/react-tabs'
import { CalendarDays, ChevronLeft, ChevronRight, Copy, Plus, RefreshCw, Rss } from 'lucide-react'
import { useMemo, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { useAgenda, useCalendarEvents, useFeedLink, useRotateFeedLink, useSchedules } from '@/api/hooks'
import type { AgendaItem } from '@/api/types'
import { PageHeader } from '@/components/layout/PageHeader'
import { ListError, ListSkeleton } from '@/components/shared/ListState'
import { Button } from '@/components/ui/button'
import { Badge, Card, EmptyState, Skeleton, type Tone } from '@/components/ui/surfaces'
import { toast } from '@/components/ui/toaster'
import { formatDate, todayIso } from '@/lib/format'
import { cn, localized } from '@/lib/utils'
import { can, isParentOnly, useAuth } from '@/stores/auth'
import { EventDialog } from './EventDialog'

const pad = (n: number) => String(n).padStart(2, '0')
const iso = (d: Date) => `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}`
const LAYERS = ['events', 'birthdays_students', 'birthdays_staff', 'leaves', 'tours']
const kindTone: Record<string, Tone> = { event: 'info', holiday: 'danger', birthdays_students: 'success', birthdays_staff: 'success', leave: 'warning', tour: 'info' }
const typeChip: Record<string, string> = { regular: 'bg-sky-100 text-sky-900', holiday: 'bg-red-100 text-red-900', staff_only: 'bg-slate-200 text-slate-800' }

/** Month navigation shared by the views. */
function useMonth() {
  const [cursor, setCursor] = useState(() => { const d = new Date(); return new Date(d.getFullYear(), d.getMonth(), 1) })
  const from = iso(cursor)
  const to = iso(new Date(cursor.getFullYear(), cursor.getMonth() + 1, 0))
  const move = (by: number) => setCursor((c) => new Date(c.getFullYear(), c.getMonth() + by, 1))
  return { cursor, from, to, move }
}

function MonthBar({ cursor, move, locale }: { cursor: Date; move: (by: number) => void; locale: string }) {
  const { t } = useTranslation()
  return (
    <div className="mb-4 flex items-center gap-2">
      <Button variant="secondary" size="sm" aria-label={t('calendar.previous')} onClick={() => move(-1)}><ChevronLeft className="h-4 w-4 rtl:rotate-180" aria-hidden="true" /></Button>
      <h2 className="min-w-40 text-center font-heading text-lg font-semibold">{cursor.toLocaleDateString(locale, { month: 'long', year: 'numeric' })}</h2>
      <Button variant="secondary" size="sm" aria-label={t('calendar.next')} onClick={() => move(1)}><ChevronRight className="h-4 w-4 rtl:rotate-180" aria-hidden="true" /></Button>
    </div>
  )
}

function SchoolMonth({ canWrite, onOpen }: { canWrite: boolean; onOpen: (target: { id: string | null; date: string }) => void }) {
  const { t, i18n } = useTranslation()
  const { cursor, from, to, move } = useMonth()
  const events = useCalendarEvents(from, to)
  const locale = i18n.language === 'ar' ? 'ar-AE-u-nu-latn' : 'en-GB'

  const cells = useMemo(() => {
    const first = new Date(cursor)
    const lead = (first.getDay() + 6) % 7
    const days = new Date(cursor.getFullYear(), cursor.getMonth() + 1, 0).getDate()
    return Array.from({ length: Math.ceil((lead + days) / 7) * 7 }, (_, i) => (i < lead || i >= lead + days ? null : new Date(cursor.getFullYear(), cursor.getMonth(), i - lead + 1)))
  }, [cursor])
  const weekdays = Array.from({ length: 7 }, (_, i) => new Date(2026, 0, 5 + i).toLocaleDateString(locale, { weekday: 'short' }))
  const byDay = (day: Date) => (events.data ?? []).filter((e) => iso(new Date(e.starts_at)) <= iso(day) && iso(new Date(e.ends_at)) >= iso(day))

  return (
    <>
      <MonthBar cursor={cursor} move={move} locale={locale} />
      {events.isError ? <ListError error={events.error} onRetry={() => void events.refetch()} /> : (
        <Card className="overflow-x-auto p-2">
          <div className="grid min-w-[44rem] grid-cols-7 gap-px text-xs">
            {weekdays.map((w) => <div key={w} className="px-1 pb-1 text-center font-bold uppercase text-muted-foreground">{w}</div>)}
            {cells.map((day, i) => (
              <div key={i} className={cn('min-h-24 rounded-md border bg-white p-1', !day && 'border-transparent bg-transparent', day && iso(day) === todayIso() && 'border-primary')}>
                {day && (
                  <>
                    <button
                      type="button"
                      disabled={!canWrite}
                      onClick={() => onOpen({ id: null, date: iso(day) })}
                      className="mb-1 block w-full text-start font-bold text-muted-foreground enabled:hover:text-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
                      aria-label={canWrite ? t('calendar.addOn', { date: iso(day) }) : undefined}
                    >
                      {day.getDate()}
                    </button>
                    <ul className="space-y-0.5">
                      {byDay(day).slice(0, 3).map((e, k) => (
                        <li key={`${e.event_id}-${k}`}>
                          <button
                            type="button"
                            onClick={() => canWrite && onOpen({ id: e.event_id, date: iso(day) })}
                            className={cn('block w-full truncate rounded px-1 py-0.5 text-start', typeChip[e.type], !canWrite && 'cursor-default')}
                            title={localized(e.title, i18n.language)}
                          >
                            {localized(e.title, i18n.language)}
                          </button>
                        </li>
                      ))}
                      {byDay(day).length > 3 && <li className="px-1 text-muted-foreground">{t('calendar.more', { count: byDay(day).length - 3 })}</li>}
                    </ul>
                  </>
                )}
              </div>
            ))}
          </div>
        </Card>
      )}
      {events.isLoading && <ListSkeleton />}
    </>
  )
}

function itemTitle(i: AgendaItem, language: string) {
  if (i.title) return localized(i.title, language)
  if (i.kind.startsWith('birthdays')) return `${i.label ?? ''} · ${i.detail ?? ''}`
  return i.label ?? ''
}

function Agenda({ mine }: { mine: boolean }) {
  const { t, i18n } = useTranslation()
  const { cursor, from, to, move } = useMonth()
  const [layers, setLayers] = useState<string[]>(LAYERS)
  const agenda = useAgenda(from, to, layers, mine)
  const locale = i18n.language === 'ar' ? 'ar-AE-u-nu-latn' : 'en-GB'
  const grouped = useMemo(() => {
    const map = new Map<string, AgendaItem[]>()
    for (const item of agenda.data ?? []) map.set(item.start, [...(map.get(item.start) ?? []), item])
    return [...map.entries()]
  }, [agenda.data])

  return (
    <>
      <MonthBar cursor={cursor} move={move} locale={locale} />
      <fieldset className="mb-4 flex flex-wrap gap-x-4 gap-y-2">
        <legend className="sr-only">{t('calendar.layers.title')}</legend>
        {LAYERS.map((l) => (
          <label key={l} className="flex items-center gap-1.5 text-sm">
            <input type="checkbox" className="h-4 w-4 accent-primary" checked={layers.includes(l)} onChange={(e) => setLayers(e.target.checked ? [...layers, l] : layers.filter((x) => x !== l))} />
            {t(`calendar.layers.${l}`)}
          </label>
        ))}
      </fieldset>
      <Card className="overflow-hidden">
        {agenda.isLoading ? <ListSkeleton /> : agenda.isError ? <ListError error={agenda.error} onRetry={() => void agenda.refetch()} /> :
          grouped.length === 0 ? <EmptyState icon={<CalendarDays className="h-7 w-7" aria-hidden="true" />} title={t('calendar.agendaEmpty')} description={t('calendar.agendaEmptyBody')} /> : (
            <ul className="divide-y">
              {grouped.map(([day, items]) => (
                <li key={day} className="flex flex-wrap gap-x-4 gap-y-1 px-4 py-3">
                  <span className="w-32 shrink-0 font-bold">{formatDate(day, i18n.language)}</span>
                  <ul className="flex flex-1 flex-wrap gap-2">
                    {items.map((i, k) => (
                      <li key={k}><Badge tone={kindTone[i.kind] ?? 'neutral'}>{t(`calendar.kinds.${i.kind}`)}: {itemTitle(i, i18n.language)}{i.end !== i.start ? ` → ${formatDate(i.end, i18n.language)}` : ''}</Badge></li>
                    ))}
                  </ul>
                </li>
              ))}
            </ul>
          )}
      </Card>
    </>
  )
}

function Schedules() {
  const { t, i18n } = useTranslation()
  const schedules = useSchedules()
  const dayNames = Array.from({ length: 7 }, (_, i) => new Date(2026, 0, 4 + i).toLocaleDateString(i18n.language === 'ar' ? 'ar-AE-u-nu-latn' : 'en-GB', { weekday: 'short' }))
  if (schedules.isLoading) return <Skeleton className="h-48" />
  if (schedules.isError || !schedules.data) return <ListError error={schedules.error} onRetry={() => void schedules.refetch()} />
  const { staff, programs } = schedules.data
  return (
    <div className="grid gap-4 lg:grid-cols-2">
      <Card className="p-4">
        <h2 className="mb-2 font-heading text-lg font-semibold">{t('calendar.schedules.staff')}</h2>
        {staff.length === 0 ? <p className="text-sm text-muted-foreground">{t('calendar.schedules.noStaff')}</p> : (
          <ul className="divide-y text-sm">
            {staff.map((s) => (
              <li key={s.staff_id} className="py-2">
                <p className="font-semibold">{s.name}</p>
                <p className="text-xs text-muted-foreground">{s.shifts.map((x) => `${dayNames[x.day_of_week]} · ${x.room_name ?? ''}`).join('  |  ')}</p>
              </li>
            ))}
          </ul>
        )}
      </Card>
      <Card className="p-4">
        <h2 className="mb-2 font-heading text-lg font-semibold">{t('calendar.schedules.programs')}</h2>
        {programs.length === 0 ? <p className="text-sm text-muted-foreground">{t('calendar.schedules.noPrograms')}</p> : (
          <ul className="divide-y text-sm">
            {programs.map((p) => (
              <li key={p.program_id} className="flex flex-wrap items-center gap-2 py-2">
                <span className="font-semibold">{localized(p.name, i18n.language)}</span>
                <span className="font-mono text-xs" dir="ltr">{p.start_time.slice(0, 5)}–{p.end_time.slice(0, 5)}</span>
                <span className="text-xs text-muted-foreground">{t('calendar.schedules.days', { count: p.days_per_week })} · {t('calendar.schedules.students', { count: p.students })}</span>
              </li>
            ))}
          </ul>
        )}
      </Card>
    </div>
  )
}

/** The link a family adds to their phone calendar. Managers can replace it, which switches the old one off. */
function FeedLink({ canRotate }: { canRotate: boolean }) {
  const { t } = useTranslation()
  const link = useFeedLink()
  const rotate = useRotateFeedLink()
  const url = link.data ? `${window.location.origin}${link.data.path}` : ''

  const copy = async () => {
    try {
      await navigator.clipboard.writeText(url)
      toast.success(t('calendar.feed.copied'))
    } catch {
      toast.error(t('calendar.feed.copyFailed'))
    }
  }

  return (
    <Card className="mt-6 space-y-2 p-4">
      <h2 className="flex items-center gap-2 font-heading text-lg font-semibold"><Rss className="h-4 w-4" aria-hidden="true" />{t('calendar.feed.title')}</h2>
      <p className="text-sm text-muted-foreground">{t('calendar.feed.body')}</p>
      {link.data && (
        <div className="flex flex-wrap items-center gap-2">
          <input readOnly value={url} dir="ltr" aria-label={t('calendar.feed.title')} className="min-w-0 flex-1 rounded-md border bg-slate-50 px-3 py-2 font-mono text-xs" onFocus={(e) => e.currentTarget.select()} />
          <Button variant="secondary" onClick={() => void copy()}><Copy className="h-4 w-4" aria-hidden="true" />{t('calendar.feed.copy')}</Button>
          {canRotate && <Button variant="ghost" onClick={() => { if (window.confirm(t('calendar.feed.confirmRotate'))) rotate.mutate() }}><RefreshCw className="h-4 w-4" aria-hidden="true" />{t('calendar.feed.rotate')}</Button>}
        </div>
      )}
    </Card>
  )
}

export default function CalendarPage() {
  const { t } = useTranslation()
  const me = useAuth((s) => s.me)
  const parent = isParentOnly(me)
  const canWrite = can(me, 'calendar.write')
  const [tab, setTab] = useState('school')
  const [open, setOpen] = useState<{ id: string | null; date: string } | null>(null)
  const tabs = ['school', ...(parent ? [] : ['admin', 'mine', 'schedules'])]

  return (
    <>
      <PageHeader
        title={t('calendar.title')}
        subtitle={t(parent ? 'calendar.parentSubtitle' : 'calendar.subtitle')}
        action={canWrite ? <Button onClick={() => setOpen({ id: null, date: todayIso() })}><Plus className="h-4 w-4" aria-hidden="true" />{t('calendar.add')}</Button> : undefined}
      />
      <TabsPrimitive.Root value={tab} onValueChange={setTab}>
        {tabs.length > 1 && (
          <TabsPrimitive.List className="mb-5 flex gap-1 overflow-x-auto border-b">
            {tabs.map((key) => (
              <TabsPrimitive.Trigger
                key={key}
                value={key}
                className="whitespace-nowrap border-b-2 border-transparent px-4 py-2.5 text-sm font-semibold text-muted-foreground transition-colors hover:text-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring data-[state=active]:border-primary data-[state=active]:font-bold data-[state=active]:text-primary-dark"
              >
                {t(`calendar.tabs.${key}`)}
              </TabsPrimitive.Trigger>
            ))}
          </TabsPrimitive.List>
        )}
        <TabsPrimitive.Content value="school"><SchoolMonth canWrite={canWrite} onOpen={setOpen} /></TabsPrimitive.Content>
        {!parent && <TabsPrimitive.Content value="admin"><Agenda mine={false} /></TabsPrimitive.Content>}
        {!parent && <TabsPrimitive.Content value="mine"><Agenda mine /></TabsPrimitive.Content>}
        {!parent && <TabsPrimitive.Content value="schedules"><Schedules /></TabsPrimitive.Content>}
      </TabsPrimitive.Root>
      <FeedLink canRotate={can(me, 'settings.write')} />
      {open && <EventDialog key={open.id ?? 'new'} id={open.id} startDate={open.date} onClose={() => setOpen(null)} />}
    </>
  )
}

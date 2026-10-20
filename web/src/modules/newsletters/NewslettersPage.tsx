import { BarChart3, Newspaper, Plus, Trash2 } from 'lucide-react'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { flatten, useDeleteNewsletter, useMyNewsletters, useNewsletterAction, useNewsletterStats, useNewsletters, useReadNewsletter } from '@/api/hooks'
import { PageHeader } from '@/components/layout/PageHeader'
import { ListError, ListSkeleton, LoadMore } from '@/components/shared/ListState'
import { SafeHtml } from '@/components/shared/SafeHtml'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/form-controls'
import { Badge, Card, Dialog, DialogContent, DialogHeader, EmptyState, Skeleton, type Tone } from '@/components/ui/surfaces'
import { toast } from '@/components/ui/toaster'
import { useApiErrorMessage } from '@/hooks/use-api-error'
import { formatDate } from '@/lib/format'
import { localized } from '@/lib/utils'
import { can, useAuth } from '@/stores/auth'
import { NewsletterDialog } from './NewsletterDialog'

const tone: Record<string, Tone> = { draft: 'warning', scheduled: 'info', published: 'success', archived: 'neutral' }

function StatsDialog({ id, onClose }: { id: string; onClose: () => void }) {
  const { t } = useTranslation()
  const stats = useNewsletterStats(id)
  return (
    <Dialog open onOpenChange={(o) => !o && onClose()}>
      <DialogContent closeLabel={t('common.close')} className="max-w-lg">
        <DialogHeader title={t('newsletters.stats.title')} />
        {!stats.data ? <Skeleton className="h-32" /> : (
          <div className="space-y-4">
            <div className="grid grid-cols-3 gap-3 text-center">
              <Card className="p-3"><p className="font-heading text-xl font-bold">{stats.data.recipients}</p><p className="text-[11px] font-bold uppercase text-muted-foreground">{t('newsletters.stats.sent')}</p></Card>
              <Card className="p-3"><p className="font-heading text-xl font-bold">{stats.data.open_rate}%</p><p className="text-[11px] font-bold uppercase text-muted-foreground">{t('newsletters.stats.opened', { count: stats.data.opened })}</p></Card>
              <Card className="p-3"><p className="font-heading text-xl font-bold">{stats.data.clicks}</p><p className="text-[11px] font-bold uppercase text-muted-foreground">{t('newsletters.stats.clicks')}</p></Card>
            </div>
            {stats.data.links.length > 0 && (
              <ul className="divide-y text-sm">
                {stats.data.links.map((l) => <li key={l.index} className="flex items-center justify-between gap-2 py-2"><span className="truncate font-mono text-xs" dir="ltr">{l.url}</span><span className="font-semibold">{l.clicks}</span></li>)}
              </ul>
            )}
          </div>
        )}
      </DialogContent>
    </Dialog>
  )
}

function ManagerView() {
  const { t, i18n } = useTranslation()
  const [status, setStatus] = useState('')
  const [search, setSearch] = useState('')
  const [open, setOpen] = useState<{ id: string | null } | null>(null)
  const [stats, setStats] = useState<string | null>(null)
  const list = useNewsletters(status, search)
  const action = useNewsletterAction()
  const remove = useDeleteNewsletter()
  const message = useApiErrorMessage()
  const rows = flatten(list.data)

  const run = async (work: () => Promise<unknown>) => {
    try {
      await work()
    } catch (error) {
      toast.error(message(error))
    }
  }

  return (
    <>
      <div className="mb-4 flex flex-wrap items-center gap-2">
        <div className="flex flex-wrap gap-2" role="group" aria-label={t('newsletters.title')}>
          {['', 'draft', 'scheduled', 'published', 'archived'].map((s) => (
            <Button key={s || 'all'} size="sm" variant={status === s ? 'default' : 'secondary'} aria-pressed={status === s} onClick={() => setStatus(s)}>{s ? t(`newsletters.status.${s}`) : t('newsletters.status.all')}</Button>
          ))}
        </div>
        <Input type="search" className="max-w-56" placeholder={t('common.search')} aria-label={t('common.search')} value={search} onChange={(e) => setSearch(e.target.value)} />
        <Button className="ms-auto" onClick={() => setOpen({ id: null })}><Plus className="h-4 w-4" aria-hidden="true" />{t('newsletters.new')}</Button>
      </div>
      <Card className="overflow-hidden">
        {list.isLoading ? <ListSkeleton /> : list.isError ? <ListError error={list.error} onRetry={() => void list.refetch()} /> :
          rows.length === 0 ? <EmptyState icon={<Newspaper className="h-7 w-7" aria-hidden="true" />} title={t('newsletters.empty')} description={t('newsletters.emptyBody')} /> : (
            <>
              <ul className="divide-y">
                {rows.map((n) => (
                  <li key={n.id} className="flex flex-wrap items-center gap-3 px-4 py-3 text-sm">
                    <button type="button" className="font-bold text-primary-dark underline-offset-4 hover:underline focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring" onClick={() => setOpen({ id: n.id })}>{localized(n.title, i18n.language)}</button>
                    <Badge tone={tone[n.status]}>{t(`newsletters.status.${n.status}`)}</Badge>
                    <span className="text-xs text-muted-foreground">{t(`newsletters.audiences.${n.audience_type}`)}</span>
                    {n.scheduled_for && <span className="text-xs text-muted-foreground">{new Date(n.scheduled_for).toLocaleString(i18n.language === 'ar' ? 'ar-AE-u-nu-latn' : 'en-GB')}</span>}
                    {n.published_at && <span className="text-xs text-muted-foreground">{t('newsletters.sentOn', { date: formatDate(n.published_at, i18n.language) })} · {t('newsletters.openedOf', { opened: n.opened, total: n.recipients })}</span>}
                    <span className="ms-auto flex gap-1">
                      {n.status === 'published' && <Button size="sm" variant="secondary" onClick={() => setStats(n.id)}><BarChart3 className="h-4 w-4" aria-hidden="true" />{t('newsletters.stats.title')}</Button>}
                      {n.status === 'published' && <Button size="sm" variant="ghost" onClick={() => void run(() => action.mutateAsync({ id: n.id, action: 'archive' }))}>{t('newsletters.archive')}</Button>}
                      {n.status === 'scheduled' && <Button size="sm" variant="ghost" onClick={() => void run(() => action.mutateAsync({ id: n.id, action: 'unschedule' }))}>{t('newsletters.unschedule')}</Button>}
                      {(n.status === 'draft' || n.status === 'scheduled') && <Button size="sm" variant="ghost" aria-label={t('common.delete')} onClick={() => { if (window.confirm(t('newsletters.confirmDelete'))) void run(() => remove.mutateAsync(n.id)) }}><Trash2 className="h-4 w-4" aria-hidden="true" /></Button>}
                    </span>
                  </li>
                ))}
              </ul>
              <LoadMore visible={!!list.hasNextPage} loading={list.isFetchingNextPage} onClick={() => void list.fetchNextPage()} />
            </>
          )}
      </Card>
      {open && <NewsletterDialog key={open.id ?? 'new'} id={open.id} onClose={() => setOpen(null)} />}
      {stats && <StatsDialog id={stats} onClose={() => setStats(null)} />}
    </>
  )
}

function Reader({ id, onClose }: { id: string; onClose: () => void }) {
  const { t, i18n } = useTranslation()
  const read = useReadNewsletter(id, i18n.language === 'ar' ? 'ar' : 'en')
  return (
    <Dialog open onOpenChange={(o) => !o && onClose()}>
      <DialogContent closeLabel={t('common.close')} className="max-w-2xl">
        <DialogHeader title={read.data ? localized(read.data.title, i18n.language) : t('newsletters.title')} />
        {!read.data ? <Skeleton className="h-48" /> : (
          <SafeHtml
            html={read.data.body}
            dir={read.data.language === 'ar' ? 'rtl' : 'ltr'}
            className="space-y-3 text-sm leading-relaxed [&_a]:text-primary-dark [&_a]:underline [&_h2]:text-lg [&_h2]:font-bold [&_h3]:font-bold [&_ol]:list-decimal [&_ol]:ps-6 [&_ul]:list-disc [&_ul]:ps-6"
          />
        )}
      </DialogContent>
    </Dialog>
  )
}

function ParentView() {
  const { t, i18n } = useTranslation()
  const list = useMyNewsletters()
  const [open, setOpen] = useState<string | null>(null)
  const rows = flatten(list.data)
  return (
    <>
      <Card className="overflow-hidden">
        {list.isLoading ? <ListSkeleton /> : list.isError ? <ListError error={list.error} onRetry={() => void list.refetch()} /> :
          rows.length === 0 ? <EmptyState icon={<Newspaper className="h-7 w-7" aria-hidden="true" />} title={t('newsletters.emptyParent')} description={t('newsletters.emptyParentBody')} /> : (
            <>
              <ul className="divide-y">
                {rows.map((n) => (
                  <li key={n.id}>
                    <button type="button" onClick={() => setOpen(n.id)} className="flex w-full flex-wrap items-center gap-3 px-4 py-3 text-start hover:bg-slate-50 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring">
                      <span className={n.opened ? 'font-semibold' : 'font-bold'}>{localized(n.title, i18n.language)}</span>
                      {!n.opened && <Badge tone="info">{t('newsletters.new_')}</Badge>}
                      {n.published_at && <span className="ms-auto text-xs text-muted-foreground">{formatDate(n.published_at, i18n.language)}</span>}
                    </button>
                  </li>
                ))}
              </ul>
              <LoadMore visible={!!list.hasNextPage} loading={list.isFetchingNextPage} onClick={() => void list.fetchNextPage()} />
            </>
          )}
      </Card>
      {open && <Reader id={open} onClose={() => setOpen(null)} />}
    </>
  )
}

export default function NewslettersPage() {
  const { t } = useTranslation()
  const me = useAuth((s) => s.me)
  const manage = can(me, 'messaging.broadcast')
  return (
    <>
      <PageHeader title={t('newsletters.title')} subtitle={t(manage ? 'newsletters.subtitle' : 'newsletters.parentSubtitle')} />
      {manage ? <ManagerView /> : <ParentView />}
    </>
  )
}

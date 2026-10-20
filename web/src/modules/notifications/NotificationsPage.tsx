import { Bell, Check } from 'lucide-react'
import { useNavigate } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { flatten, useMarkRead, useNotifications, usePreferences, useSavePreferences } from '@/api/hooks'
import type { NotificationPreference } from '@/api/types'
import { PageHeader } from '@/components/layout/PageHeader'
import { ListError, ListSkeleton, LoadMore } from '@/components/shared/ListState'
import { Button } from '@/components/ui/button'
import { Badge, Card, EmptyState, type Tone } from '@/components/ui/surfaces'
import { toast } from '@/components/ui/toaster'
import { useApiErrorMessage } from '@/hooks/use-api-error'
import { localized } from '@/lib/utils'
import { can, useAuth } from '@/stores/auth'

const tierTone: Record<string, Tone> = { critical: 'danger', operational: 'info', informational: 'neutral' }

function Preferences() {
  const { t } = useTranslation()
  const prefs = usePreferences()
  const save = useSavePreferences()
  const message = useApiErrorMessage()
  if (prefs.isLoading) return null
  if (prefs.isError) return <ListError error={prefs.error} onRetry={() => void prefs.refetch()} />

  const change = async (pref: NotificationPreference, patch: Partial<NotificationPreference>) => {
    try {
      await save.mutateAsync(prefs.data!.map((p) => (p.tier === pref.tier ? { ...p, ...patch } : p)))
      toast.success(t('notifications.saved'))
    } catch (error) {
      toast.error(message(error))
    }
  }

  return (
    <Card className="p-4">
      <h2 className="font-heading text-lg font-semibold">{t('notifications.preferences')}</h2>
      <p className="mb-3 text-sm text-muted-foreground">{t('notifications.preferencesHint')} {t('notifications.criticalNote')}</p>
      <ul className="space-y-3">
        {prefs.data!.map((p) => (
          <li key={p.tier} className="rounded-md border p-3">
            <p className="mb-2 text-sm font-bold">{t(`notifications.tier.${p.tier}`)}</p>
            <div className="flex flex-wrap gap-4 text-sm">
              <label className="flex items-center gap-2">
                <input type="checkbox" className="h-4 w-4 accent-primary" checked={p.in_app} disabled={p.tier === 'critical'} onChange={(e) => void change(p, { in_app: e.target.checked })} />
                {t('notifications.inApp')}
              </label>
              <label className="flex items-center gap-2">
                <input type="checkbox" className="h-4 w-4 accent-primary" checked={p.email} onChange={(e) => void change(p, { email: e.target.checked })} />
                {t('notifications.email')}
              </label>
            </div>
          </li>
        ))}
      </ul>
    </Card>
  )
}

export default function NotificationsPage() {
  const { t, i18n } = useTranslation()
  const me = useAuth((s) => s.me)
  const navigate = useNavigate()
  const list = useNotifications()
  const mark = useMarkRead()
  const items = flatten(list.data)
  const when = (v: string) => new Intl.DateTimeFormat(i18n.language === 'ar' ? 'ar-AE-u-nu-latn' : 'en-GB', { dateStyle: 'medium', timeStyle: 'short' }).format(new Date(v))

  return (
    <>
      <PageHeader
        title={t('notifications.title')}
        subtitle={t('notifications.subtitle')}
        action={<Button variant="secondary" onClick={() => mark.mutate(null)} disabled={items.every((n) => n.read)}><Check className="h-4 w-4" aria-hidden="true" />{t('notifications.markAll')}</Button>}
      />
      <div className="grid gap-6 lg:grid-cols-[1fr_22rem]">
        <Card className="overflow-hidden">
          {list.isLoading ? <ListSkeleton /> : list.isError ? <ListError error={list.error} onRetry={() => void list.refetch()} /> :
            items.length === 0 ? <EmptyState icon={<Bell className="h-7 w-7" aria-hidden="true" />} title={t('notifications.empty')} description={t('notifications.emptyBody')} /> : (
              <>
                <ul>
                  {items.map((n) => (
                    <li key={n.id} className={`border-b p-4 ${n.read ? '' : 'bg-accent/40'}`}>
                      <div className="mb-1 flex items-center gap-2">
                        <Badge tone={tierTone[n.tier]}>{t(`notifications.tierShort.${n.tier}`)}</Badge>
                        <span className={`text-sm ${n.read ? 'font-semibold' : 'font-bold'}`}>{localized(n.title, i18n.language)}</span>
                        <span className="ms-auto text-[11px] text-muted-foreground">{when(n.created_at)}</span>
                      </div>
                      <p className="text-sm text-muted-foreground">{localized(n.body, i18n.language)}</p>
                      <div className="mt-2 flex gap-2">
                        {n.link && (
                          <Button size="sm" variant="secondary" onClick={() => { mark.mutate(n.id); navigate(n.link!) }}>{t('notifications.open')}</Button>
                        )}
                        {!n.read && <Button size="sm" variant="ghost" onClick={() => mark.mutate(n.id)}>{t('notifications.markRead')}</Button>}
                      </div>
                    </li>
                  ))}
                </ul>
                <LoadMore visible={!!list.hasNextPage} loading={list.isFetchingNextPage} onClick={() => void list.fetchNextPage()} />
              </>
            )}
        </Card>
        {can(me, 'notifications.preferences') && <Preferences />}
      </div>
    </>
  )
}

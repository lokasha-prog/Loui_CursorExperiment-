import { MessageCircleHeart } from 'lucide-react'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { flatten, useFeedbackList, useSetFeedbackStatus } from '@/api/hooks'
import { PageHeader } from '@/components/layout/PageHeader'
import { ListError, ListSkeleton, LoadMore } from '@/components/shared/ListState'
import { Button } from '@/components/ui/button'
import { Badge, Card, EmptyState, Table, Td, Th, type Tone } from '@/components/ui/surfaces'
import { can, useAuth } from '@/stores/auth'

const kindTone: Record<string, Tone> = { problem: 'danger', idea: 'info', praise: 'success' }

/** What staff and families told the team through the feedback button. */
export default function FeedbackPage() {
  const { t, i18n } = useTranslation()
  const me = useAuth((s) => s.me)
  const [status, setStatus] = useState('')
  const list = useFeedbackList(status)
  const mark = useSetFeedbackStatus()
  const rows = flatten(list.data)
  const when = (v: string) => new Intl.DateTimeFormat(i18n.language === 'ar' ? 'ar-AE-u-nu-latn' : 'en-GB', { dateStyle: 'medium', timeStyle: 'short' }).format(new Date(v))

  return (
    <>
      <PageHeader title={t('feedback.pageTitle')} subtitle={t('feedback.pageSubtitle')} />
      <div className="mb-4 flex gap-2" role="group" aria-label={t('feedback.filter')}>
        {['', 'new', 'seen', 'done'].map((s) => (
          <Button key={s || 'all'} size="sm" variant={status === s ? 'default' : 'secondary'} aria-pressed={status === s} onClick={() => setStatus(s)}>
            {s ? t(`feedback.status.${s}`) : t('feedback.status.all')}
          </Button>
        ))}
      </div>
      <Card className="overflow-hidden">
        {list.isLoading ? <ListSkeleton /> : list.isError ? <ListError error={list.error} onRetry={() => void list.refetch()} /> :
          rows.length === 0 ? <EmptyState icon={<MessageCircleHeart className="h-7 w-7" aria-hidden="true" />} title={t('feedback.empty')} /> : (
            <>
              <Table>
                <caption className="sr-only">{t('feedback.pageTitle')}</caption>
                <thead>
                  <tr><Th>{t('feedback.columns.when')}</Th><Th>{t('feedback.columns.who')}</Th><Th>{t('feedback.columns.kind')}</Th><Th>{t('feedback.columns.message')}</Th><Th>{t('feedback.columns.status')}</Th></tr>
                </thead>
                <tbody>
                  {rows.map((r) => (
                    <tr key={r.id} className="align-top hover:bg-slate-50/70">
                      <Td className="whitespace-nowrap text-xs">{when(r.created_at)}</Td>
                      <Td className="text-sm font-semibold">{r.user_name}</Td>
                      <Td><Badge tone={kindTone[r.kind] ?? 'neutral'}>{t(`feedback.kinds.${r.kind}`)}</Badge>{r.rating ? <span className="ms-2 text-xs">{r.rating}/5</span> : null}</Td>
                      <Td className="max-w-md text-sm"><p className="whitespace-pre-line">{r.message}</p>{r.page && <p dir="ltr" className="mt-1 text-start font-mono text-[11px] text-muted-foreground">{r.page}</p>}</Td>
                      <Td>
                        {can(me, 'settings.write') ? (
                          <select
                            aria-label={t('feedback.columns.status')}
                            className="rounded-md border bg-white px-2 py-1 text-xs"
                            value={r.status}
                            onChange={(e) => mark.mutate({ id: r.id, status: e.target.value })}
                          >
                            {['new', 'seen', 'done'].map((s) => <option key={s} value={s}>{t(`feedback.status.${s}`)}</option>)}
                          </select>
                        ) : t(`feedback.status.${r.status}`)}
                      </Td>
                    </tr>
                  ))}
                </tbody>
              </Table>
              <LoadMore visible={!!list.hasNextPage} loading={list.isFetchingNextPage} onClick={() => void list.fetchNextPage()} />
            </>
          )}
      </Card>
    </>
  )
}

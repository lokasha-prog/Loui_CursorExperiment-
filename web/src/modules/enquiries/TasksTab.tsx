import { CheckCircle2 } from 'lucide-react'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { flatten, useCompleteTask, useTasks } from '@/api/hooks'
import { ListError, ListSkeleton, LoadMore } from '@/components/shared/ListState'
import { Button } from '@/components/ui/button'
import { Badge, Card, EmptyState } from '@/components/ui/surfaces'
import { toast } from '@/components/ui/toaster'
import { useApiErrorMessage } from '@/hooks/use-api-error'
import { formatDate, todayIso } from '@/lib/format'

/** Follow-up jobs for staff, such as calling a family two days after their tour. */
export function TasksTab() {
  const { t, i18n } = useTranslation()
  const [done, setDone] = useState(false)
  const list = useTasks(done)
  const complete = useCompleteTask()
  const message = useApiErrorMessage()
  const rows = flatten(list.data)

  const finish = async (id: string) => {
    try {
      await complete.mutateAsync(id)
    } catch (error) {
      toast.error(message(error))
    }
  }

  return (
    <>
      <div className="mb-4 flex gap-2" role="group" aria-label={t('enquiries.tabs.tasks')}>
        <Button size="sm" variant={!done ? 'default' : 'secondary'} aria-pressed={!done} onClick={() => setDone(false)}>{t('enquiries.tasks.open')}</Button>
        <Button size="sm" variant={done ? 'default' : 'secondary'} aria-pressed={done} onClick={() => setDone(true)}>{t('enquiries.tasks.done')}</Button>
      </div>
      <Card className="overflow-hidden">
        {list.isLoading ? <ListSkeleton /> : list.isError ? <ListError error={list.error} onRetry={() => void list.refetch()} /> :
          rows.length === 0 ? <EmptyState icon={<CheckCircle2 className="h-7 w-7" aria-hidden="true" />} title={t('enquiries.tasks.empty')} description={t('enquiries.tasks.emptyBody')} /> : (
            <>
              <ul className="divide-y">
                {rows.map((r) => (
                  <li key={r.id} className="flex flex-wrap items-center gap-3 px-4 py-3 text-sm">
                    <span className="font-semibold">{r.title}</span>
                    <span className="text-muted-foreground">{r.assigned_staff_name ?? t('enquiries.tours.noStaff')}</span>
                    <Badge tone={!r.is_done && r.due_date < todayIso() ? 'danger' : 'neutral'}>{t('enquiries.tasks.due', { date: formatDate(r.due_date, i18n.language) })}</Badge>
                    {!r.is_done && <Button size="sm" variant="secondary" className="ms-auto" onClick={() => void finish(r.id)}>{t('enquiries.tasks.markDone')}</Button>}
                  </li>
                ))}
              </ul>
              <LoadMore visible={!!list.hasNextPage} loading={list.isFetchingNextPage} onClick={() => void list.fetchNextPage()} />
            </>
          )}
      </Card>
    </>
  )
}

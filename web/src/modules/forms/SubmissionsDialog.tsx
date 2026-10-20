import { useTranslation } from 'react-i18next'
import { flatten, useForm, useFormAnalytics, useSubmissions } from '@/api/hooks'
import { ListError, ListSkeleton, LoadMore } from '@/components/shared/ListState'
import { Card, Dialog, DialogContent, DialogHeader } from '@/components/ui/surfaces'
import { formatDate } from '@/lib/format'
import { localized } from '@/lib/utils'

function Stat({ label, value }: { label: string; value: string }) {
  return (
    <Card className="p-3">
      <p className="font-heading text-xl font-bold" dir="ltr">{value}</p>
      <p className="text-[11px] font-bold uppercase tracking-wide text-muted-foreground">{label}</p>
    </Card>
  )
}

/** What people sent in, and how the form performs: starts, completion, time taken, and where people give up. */
export function SubmissionsDialog({ formId, onClose }: { formId: string; onClose: () => void }) {
  const { t, i18n } = useTranslation()
  const detail = useForm(formId)
  const list = useSubmissions(formId)
  const stats = useFormAnalytics(formId)
  const rows = flatten(list.data)
  const fields = detail.data?.published_fields.length ? detail.data.published_fields : detail.data?.fields ?? []
  const label = (id: string) => {
    const f = fields.find((x) => x.id === id)
    return f ? localized(f.label, i18n.language) : id
  }
  const show = (value: unknown) => (typeof value === 'boolean' ? (value ? t('common.yes') : t('common.no')) : typeof value === 'string' && value.startsWith('data:image') ? t('forms.fill.signed') : String(value))

  return (
    <Dialog open onOpenChange={(o) => !o && onClose()}>
      <DialogContent closeLabel={t('common.close')} className="max-w-4xl">
        <DialogHeader title={detail.data ? `${localized(detail.data.form.name, i18n.language)} · ${t('forms.submissions.title')}` : t('forms.submissions.title')} />
        <div className="space-y-5">
          {stats.data && (
            <>
              <div className="grid gap-3 sm:grid-cols-4">
                <Stat label={t('forms.submissions.starts')} value={String(stats.data.starts)} />
                <Stat label={t('forms.submissions.sent')} value={String(stats.data.submissions)} />
                <Stat label={t('forms.submissions.completion')} value={`${stats.data.completion_rate}%`} />
                <Stat label={t('forms.submissions.time')} value={`${Math.round(stats.data.average_seconds)} s`} />
              </div>
              {stats.data.drop_offs.length > 0 && (
                <p className="text-sm text-muted-foreground">
                  {t('forms.submissions.dropOff')}: {stats.data.drop_offs.map((d) => `${label(d.field_id)} (${d.count})`).join(', ')}
                </p>
              )}
            </>
          )}
          {list.isLoading ? <ListSkeleton /> : list.isError ? <ListError error={list.error} onRetry={() => void list.refetch()} /> :
            rows.length === 0 ? <p className="text-sm text-muted-foreground">{t('forms.submissions.empty')}</p> : (
              <ul className="space-y-3">
                {rows.map((r) => (
                  <li key={r.id} className="rounded-md border p-3 text-sm">
                    <p className="mb-2 flex flex-wrap items-center gap-2 text-xs text-muted-foreground">
                      <span>{formatDate(r.created_at, i18n.language)}</span>
                      {r.student_name && <span className="font-semibold text-foreground">{r.student_name}</span>}
                      <span>v{r.version}</span>
                    </p>
                    <dl className="grid gap-x-4 gap-y-1 sm:grid-cols-[minmax(8rem,12rem)_1fr]">
                      {Object.entries(r.answers).map(([k, v]) => (
                        <div key={k} className="contents"><dt className="text-muted-foreground">{label(k)}</dt><dd className="font-semibold">{show(v)}</dd></div>
                      ))}
                    </dl>
                  </li>
                ))}
              </ul>
            )}
          <LoadMore visible={!!list.hasNextPage} loading={list.isFetchingNextPage} onClick={() => void list.fetchNextPage()} />
        </div>
      </DialogContent>
    </Dialog>
  )
}

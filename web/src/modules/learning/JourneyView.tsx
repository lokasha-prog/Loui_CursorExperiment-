import { Route } from 'lucide-react'
import { useTranslation } from 'react-i18next'
import { useState } from 'react'
import { useJourney, useParentReports } from '@/api/hooks'
import { Button } from '@/components/ui/button'
import { toast } from '@/components/ui/toaster'
import { downloadLearningReport } from './ReportsTab'
import { Badge, Card, EmptyState, Skeleton } from '@/components/ui/surfaces'
import { formatDate } from '@/lib/format'
import { localized } from '@/lib/utils'

const levelTone = { secure: 'success', growing: 'info', starting: 'warning' } as const

/** A child's learning story: approved reports in plain words, then everything that was recorded, newest first. Families see only what was shared with them. */
export function JourneyView({ studentId }: { studentId: string }) {
  const { t, i18n } = useTranslation()
  const lang = i18n.language
  const journey = useJourney(studentId)
  const reports = useParentReports(studentId)
  const [busy, setBusy] = useState(false)
  const print = async (kind: string, extra: Record<string, string> = {}) => {
    setBusy(true)
    try { await downloadLearningReport(kind, { student_id: studentId, variant: 'parent', language: lang === 'ar' ? 'ar' : 'en', format: 'pdf', ...extra }); toast.success(t('learning.reports.done')) } catch { toast.error(t('errors.UNKNOWN')) } finally { setBusy(false) }
  }

  if (!studentId) return <EmptyState icon={<Route className="h-7 w-7" aria-hidden="true" />} title={t('learning.journey.pick')} description={t('learning.journey.pickHint')} />
  if (journey.isLoading) return <Skeleton className="h-48" />

  const items = journey.data?.items ?? []
  return (
    <div className="space-y-6">
      <div className="flex flex-wrap gap-2">
        <Button variant="secondary" disabled={busy} onClick={() => void print('journey')}>{t('learning.reports.journeyPdf')}</Button>
        <Button variant="secondary" disabled={busy} onClick={() => void print('progress')}>{t('learning.reports.progressPdf')}</Button>
        <Button variant="secondary" disabled={busy} onClick={() => void print('student_milestone')}>{t('learning.reports.milestonesPdf')}</Button>
      </div>
      {(reports.data ?? []).length > 0 && (
        <section aria-labelledby="jv-reports">
          <h2 id="jv-reports" className="mb-3 text-base font-bold">{t('learning.journey.reports')}</h2>
          <div className="grid gap-4 md:grid-cols-2">
            {(reports.data ?? []).map((r) => (
              <Card key={r.id} className="p-4">
                <h3 className="font-bold">{localized(r.template_name, lang)} · {r.term}</h3>
                <p className="text-xs text-muted-foreground">{formatDate(r.assessed_on, lang)}</p>
                <Button size="sm" variant="ghost" disabled={busy} onClick={() => void print('snapshot', { run_id: r.id })}>{t('learning.reports.reportPdf')}</Button>
                {r.summary && <p className="mt-2 text-sm">{localized(r.summary, lang)}</p>}
                <ul className="mt-3 space-y-2">
                  {r.areas.map((a, i) => (
                    <li key={i}>
                      <div className="flex items-center justify-between text-sm"><span>{localized(a.name, lang)}</span><Badge tone={levelTone[a.level]}>{t(`learning.journey.levels.${a.level}`)}</Badge></div>
                      <div className="mt-1 h-2 overflow-hidden rounded-full bg-muted" role="img" aria-label={`${a.percent}%`}><div className="h-full rounded-full bg-primary" style={{ width: `${a.percent}%` }} /></div>
                    </li>
                  ))}
                </ul>
              </Card>
            ))}
          </div>
        </section>
      )}
      <section aria-labelledby="jv-line">
        <h2 id="jv-line" className="mb-3 text-base font-bold">{t('learning.journey.timeline')}</h2>
        {items.length === 0 ? <EmptyState icon={<Route className="h-7 w-7" aria-hidden="true" />} title={t('learning.journey.empty')} description={t('learning.journey.emptyHint')} /> : (
          <ol className="relative space-y-4 border-s-2 border-primary/30 ps-5">
            {items.map((i) => (
              <li key={`${i.kind}-${i.id}`} className="relative">
                <span className="absolute -start-[1.65rem] top-1.5 h-3 w-3 rounded-full border-2 border-white bg-primary" aria-hidden="true" />
                <Card className="p-4">
                  <div className="flex flex-wrap items-center gap-2 text-xs text-muted-foreground">
                    <span>{formatDate(i.at, lang)}</span>
                    <Badge tone={i.kind === 'assessment' ? 'info' : 'neutral'}>{t(`learning.journey.kinds.${i.kind}`)}</Badge>
                    {i.type === 'concern' && <Badge tone="danger">{t('learning.obs.types.concern')}</Badge>}
                    {i.area_name && <Badge tone="success">{localized(i.area_name, lang)}</Badge>}
                    {i.percent != null && <Badge tone="neutral">{i.percent}%</Badge>}
                  </div>
                  {i.title && <h3 className="mt-1 font-bold">{localized(i.title, lang)}</h3>}
                  {i.text && <p className="mt-1 text-sm">{localized(i.text, lang)}</p>}
                </Card>
              </li>
            ))}
          </ol>
        )}
      </section>
    </div>
  )
}

import { BarChart3, Download } from 'lucide-react'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { flatten, useForecast, useMonthlyReport, useRatioHistory, useRoomOptions } from '@/api/hooks'
import { PageHeader } from '@/components/layout/PageHeader'
import { ListError, ListSkeleton, LoadMore } from '@/components/shared/ListState'
import { Button } from '@/components/ui/button'
import { Field, Select } from '@/components/ui/form-controls'
import { Badge, Card, EmptyState, Table, Td, Th } from '@/components/ui/surfaces'
import { formatDate } from '@/lib/format'
import { localized } from '@/lib/utils'

function currentMonth() {
  const now = new Date()
  return `${now.getFullYear()}-${String(now.getMonth() + 1).padStart(2, '0')}`
}

function lastMonths(count: number) {
  const now = new Date()
  return Array.from({ length: count }, (_, i) => {
    const d = new Date(now.getFullYear(), now.getMonth() - i, 1)
    return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}`
  })
}

/** Manager reports: the month's attendance, tomorrow's expected numbers, and when rooms were short of staff. */
export default function ReportsPage() {
  const { t, i18n } = useTranslation()
  const [month, setMonth] = useState(currentMonth())
  const [personType, setPersonType] = useState<'student' | 'staff'>('student')
  const report = useMonthlyReport(month, personType)
  const forecast = useForecast()
  const history = useRatioHistory()
  const rooms = useRoomOptions()
  const breaches = flatten(history.data)
  const roomName = (id: string) => localized(rooms.data?.find((r) => r.id === id)?.name, i18n.language)
  const time = (value: string) =>
    new Intl.DateTimeFormat(i18n.language === 'ar' ? 'ar-AE-u-nu-latn' : 'en-GB', { dateStyle: 'medium', timeStyle: 'short' }).format(new Date(value))

  return (
    <>
      <PageHeader title={t('reports.title')} subtitle={t('reports.subtitle')} />

      <section className="mb-8" aria-labelledby="monthly-h">
        <div className="mb-3 flex flex-wrap items-end gap-3">
          <h2 id="monthly-h" className="me-auto font-heading text-lg font-semibold">{t('reports.monthly')}</h2>
          <Field label={t('reports.month')} htmlFor="rp-month">
            <Select id="rp-month" value={month} onChange={(e) => setMonth(e.target.value)}>
              {lastMonths(12).map((m) => <option key={m} value={m}>{m}</option>)}
            </Select>
          </Field>
          <Field label={t('reports.who')} htmlFor="rp-who">
            <Select id="rp-who" value={personType} onChange={(e) => setPersonType(e.target.value as 'student' | 'staff')}>
              <option value="student">{t('attendance.type.student')}</option>
              <option value="staff">{t('attendance.type.staff')}</option>
            </Select>
          </Field>
          <Button asChild variant="secondary">
            <a href={`/api/v1/attendance/export?month=${month}&person_type=${personType}`} download>
              <Download className="h-4 w-4" aria-hidden="true" />{t('reports.export')}
            </a>
          </Button>
        </div>
        <Card className="overflow-hidden">
          {report.isLoading ? <ListSkeleton /> : report.isError ? <ListError error={report.error} onRetry={() => void report.refetch()} /> :
            report.data!.rows.length === 0 ? <EmptyState icon={<BarChart3 className="h-7 w-7" aria-hidden="true" />} title={t('reports.empty')} /> : (
              <Table>
                <caption className="sr-only">{t('reports.monthly')}</caption>
                <thead>
                  <tr>
                    <Th>{t('attendance.columns.name')}</Th>
                    <Th>{t('attendance.status.present')}</Th>
                    <Th>{t('attendance.status.late')}</Th>
                    <Th>{t('attendance.status.early_leave')}</Th>
                    <Th>{t('attendance.status.absent')}</Th>
                    <Th>{t('reports.days')}</Th>
                  </tr>
                </thead>
                <tbody>
                  {report.data!.rows.map((r) => (
                    <tr key={r.person_id} className="hover:bg-slate-50/70">
                      <Td className="font-bold">{r.name}</Td><Td>{r.present}</Td><Td>{r.late}</Td><Td>{r.early_leave}</Td><Td>{r.absent}</Td><Td>{r.days_recorded}</Td>
                    </tr>
                  ))}
                </tbody>
              </Table>
            )}
        </Card>
      </section>

      <section className="mb-8" aria-labelledby="forecast-h">
        <h2 id="forecast-h" className="mb-3 font-heading text-lg font-semibold">{t('reports.forecast')}</h2>
        <Card className="p-4">
          {forecast.isLoading ? <ListSkeleton rows={2} /> : forecast.isError ? <ListError error={forecast.error} onRetry={() => void forecast.refetch()} /> : (
            <>
              <p className="mb-3 text-sm text-muted-foreground">
                {t('reports.forecastTotal', { date: formatDate(forecast.data!.date, i18n.language), count: forecast.data!.total_expected })}
              </p>
              <ul className="grid gap-2 sm:grid-cols-2">
                {forecast.data!.by_program.map((l) => (
                  <li key={l.program_id ?? 'none'} className="flex items-center justify-between rounded-md border px-3 py-2 text-sm">
                    <span className="font-semibold">{l.program_name ? localized(l.program_name, i18n.language) : t('reports.noProgram')}</span>
                    <span>{t('reports.forecastLine', { expected: l.expected, away: l.on_leave })}</span>
                  </li>
                ))}
              </ul>
            </>
          )}
        </Card>
      </section>

      <section aria-labelledby="short-h">
        <h2 id="short-h" className="mb-3 font-heading text-lg font-semibold">{t('reports.shortages')}</h2>
        <Card className="overflow-hidden">
          {history.isLoading ? <ListSkeleton /> : history.isError ? <ListError error={history.error} onRetry={() => void history.refetch()} /> :
            breaches.length === 0 ? <EmptyState icon={<BarChart3 className="h-7 w-7" aria-hidden="true" />} title={t('reports.noShortages')} /> : (
              <>
                <Table>
                  <caption className="sr-only">{t('reports.shortages')}</caption>
                  <thead>
                    <tr>
                      <Th>{t('attendance.filters.room')}</Th><Th>{t('reports.from')}</Th><Th>{t('reports.until')}</Th><Th>{t('reports.gap')}</Th>
                    </tr>
                  </thead>
                  <tbody>
                    {breaches.map((b) => (
                      <tr key={b.id} className="hover:bg-slate-50/70">
                        <Td className="font-bold">{roomName(b.room_id)}</Td>
                        <Td>{time(b.detected_at)}</Td>
                        <Td>{b.resolved_at ? time(b.resolved_at) : <Badge tone="danger">{t('reports.ongoing')}</Badge>}</Td>
                        <Td>{t('reports.gapValue', { staff: b.staff_present, required: b.staff_required })}</Td>
                      </tr>
                    ))}
                  </tbody>
                </Table>
                <LoadMore visible={!!history.hasNextPage} loading={history.isFetchingNextPage} onClick={() => void history.fetchNextPage()} />
              </>
            )}
        </Card>
      </section>
    </>
  )
}

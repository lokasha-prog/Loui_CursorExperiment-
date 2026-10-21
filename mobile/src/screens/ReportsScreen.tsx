import { useCallback, useEffect, useState } from 'react'
import { api } from '../api'
import { cached } from '../cache'
import { pick, useI18n } from '../i18n'
import { Body, Card, Empty, Heading, Hero, Pill, Row, Screen, Title } from '../ui'

interface Report { id: string; student_name: string; date: string; activity_count: number; check_in_at: string | null; check_out_at: string | null; summary: Record<string, string> }
const time = (iso: string | null) => (iso ? new Date(iso).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' }) : '')

/** A parent's day reports, newest first: when the child arrived and left and what the nursery wrote about the day. */
export function ReportsScreen() {
  const { t, lang } = useI18n()
  const [rows, setRows] = useState<Report[]>([])
  const [busy, setBusy] = useState(false)

  const load = useCallback(async () => {
    setBusy(true)
    const r = await cached('daily-reports', () => api.page<Report>('/daily-reports?limit=30'))
    setRows(r.data ?? [])
    setBusy(false)
  }, [])
  useEffect(() => { void load() }, [load])

  return (
    <Screen refreshing={busy} onRefresh={() => void load()}>
      <Hero title={t('tabs.reports')} icon="document-text" />
      {rows.length === 0 ? <Empty icon="document-text-outline" text={t('noReports')} /> : rows.map((r) => (
        <Card key={r.id}>
          <Row><Heading>{r.student_name}</Heading><Pill tone="info" label={r.date} /></Row>
          <Body>{pick(r.summary, lang)}</Body>
          <Body muted>{[r.check_in_at && `${t('arrived')} ${time(r.check_in_at)}`, r.check_out_at && `${t('left')} ${time(r.check_out_at)}`, t('moments', { count: r.activity_count })].filter(Boolean).join(' · ')}</Body>
        </Card>
      ))}
    </Screen>
  )
}

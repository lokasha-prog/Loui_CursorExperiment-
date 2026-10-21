import { useCallback, useEffect, useState } from 'react'
import { View } from 'react-native'
import { api } from '../api'
import { cached } from '../cache'
import { pick, useI18n } from '../i18n'
import { Banner, Body, Btn, Card, Chip, Heading, Hero, Loading, Pill, Row, Screen, Title, color } from '../ui'

interface Child { id: string; first_name: string; last_name: string }
interface Level { name: Record<string, string>; level: 'secure' | 'growing' | 'starting'; percent: number }
interface Report { id: string; template_name: Record<string, string>; term: string; assessed_on: string; summary: Record<string, string> | null; areas: Level[] }
interface Item { kind: 'observation' | 'assessment'; id: string; at: string; area_name: Record<string, string> | null; title: Record<string, string> | null; text: Record<string, string> | null }

const tone = { secure: 'ok', growing: 'info', starting: 'warn' } as const

/** A family's view of their child's learning: approved reports in plain levels and the moments teachers shared. Works from the last saved copy with no signal. */
export function LearningScreen() {
  const { t, lang } = useI18n()
  const [children, setChildren] = useState<Child[]>([])
  const [child, setChild] = useState('')
  const [reports, setReports] = useState<Report[]>([])
  const [items, setItems] = useState<Item[]>([])
  const [offline, setOffline] = useState(false)
  const [busy, setBusy] = useState(true)

  const loadChildren = useCallback(async () => {
    const r = await cached('children', () => api.page<Child>('/students?limit=100'))
    setChildren(r.data ?? [])
    setOffline(r.offline)
    setChild((c) => c || r.data?.[0]?.id || '')
    setBusy(false)
  }, [])

  const loadLearning = useCallback(async (id: string) => {
    if (!id) return
    setBusy(true)
    const [a, b] = await Promise.all([
      cached(`reports.${id}`, () => api.get<Report[]>(`/learning/parent-reports/${id}`)),
      cached(`journey.${id}`, () => api.get<{ items: Item[] }>(`/learning/journey/${id}`)),
    ])
    setReports(a.data ?? [])
    setItems(b.data?.items ?? [])
    setOffline(a.offline || b.offline)
    setBusy(false)
  }, [])

  useEffect(() => { void loadChildren().catch(() => setBusy(false)) }, [loadChildren])
  useEffect(() => { void loadLearning(child).catch(() => setBusy(false)) }, [child, loadLearning])

  return (
    <Screen refreshing={busy} onRefresh={() => void loadLearning(child)}>
      <Hero title={t('learningTitle')} icon="leaf" />
      {offline && <Banner tone="info" text={t('updatedOffline')} />}
      {children.length > 1 && <Row>{children.map((c) => <Chip key={c.id} active={c.id === child} label={c.first_name} onPress={() => setChild(c.id)} />)}</Row>}
      {busy && reports.length === 0 && items.length === 0 ? <Loading /> : (
        <>
          {reports.length > 0 && <Heading>{t('learnReports')}</Heading>}
          {reports.map((r) => (
            <Card key={r.id}>
              <Body bold>{pick(r.template_name, lang)} · {r.term}</Body>
              {r.summary && <Body>{pick(r.summary, lang)}</Body>}
              {r.areas.map((a, i) => (
                <View key={i} style={{ gap: 4 }}>
                  <Row><Body>{pick(a.name, lang)}</Body><Pill tone={tone[a.level]} label={t(`levels.${a.level}`)} /></Row>
                  <View style={{ height: 8, borderRadius: 4, backgroundColor: color.line, overflow: 'hidden' }}><View style={{ width: `${a.percent}%`, height: 8, backgroundColor: color.primary }} /></View>
                </View>
              ))}
            </Card>
          ))}
          <Heading>{t('learnTimeline')}</Heading>
          {items.length === 0 ? <Body muted>{t('noLearning')}</Body> : items.map((i) => (
            <Card key={`${i.kind}-${i.id}`}>
              <Row><Pill tone={i.kind === 'assessment' ? 'info' : 'neutral'} label={t(i.kind === 'assessment' ? 'kindReport' : 'kindMoment')} />{i.area_name && <Pill tone="ok" label={pick(i.area_name, lang)} />}</Row>
              {i.title && <Body bold>{pick(i.title, lang)}</Body>}
              {i.text && <Body>{pick(i.text, lang)}</Body>}
              <Body muted>{new Date(i.at).toLocaleDateString()}</Body>
            </Card>
          ))}
        </>
      )}
    </Screen>
  )
}

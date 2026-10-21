import { useCallback, useEffect, useState } from 'react'
import { View } from 'react-native'
import { api, isOffline } from '../api'
import { useAuth } from '../auth'
import { useI18n, pick } from '../i18n'
import { store } from '../core'
import { Banner, Body, Btn, Card, Heading, Hero, Loading, Pill, ProgressBar, Row, Screen, Section, Tile } from '../ui'

interface Summary { students_present: number; students_absent: number; students_not_yet_arrived: number; students_on_leave: number; active_students: number; staff_present: number; active_staff: number; rooms_in_breach: number; pending_leave_requests: number }
interface Ratio { room_id: string; name: Record<string, string>; students_present: number; staff_present: number; staff_required: number; is_breach: boolean }

const greeting = {
  en: { morning: 'Good morning', afternoon: 'Good afternoon', evening: 'Good evening', of: 'of', here: 'children here today' },
  ar: { morning: 'صباح الخير', afternoon: 'مساء الخير', evening: 'مساء الخير', of: 'من', here: 'طفل حاضر اليوم' },
}

export function TodayScreen() {
  const { t, lang } = useI18n()
  const { can, me } = useAuth()
  const [brief, setBrief] = useState<string | null>(null)
  const [briefBusy, setBriefBusy] = useState(false)
  const [summary, setSummary] = useState<Summary | null>(null)
  const [rooms, setRooms] = useState<Ratio[]>([])
  const [offline, setOffline] = useState(false)
  const [busy, setBusy] = useState(true)

  const load = useCallback(async () => {
    setBusy(true)
    try {
      const [s, r] = await Promise.all([api.get<Summary>('/dashboard/attendance-summary'), api.get<Ratio[]>('/attendance/ratio/live')])
      setSummary(s)
      setRooms(r)
      setOffline(false)
      await store.set('nms.today', { s, r })
    } catch (error) {
      if (!isOffline(error)) throw error
      const saved = await store.get<{ s: Summary; r: Ratio[] }>('nms.today')
      if (saved) { setSummary(saved.s); setRooms(saved.r) }
      setOffline(true)
    } finally {
      setBusy(false)
    }
  }, [])

  const askBrain = async () => {
    setBriefBusy(true)
    try { setBrief((await api.get<{ summary: string }>(`/ai/briefing/overview?language=${lang}`)).summary) } catch { setBrief(t('briefingDown')) }
    setBriefBusy(false)
  }

  useEffect(() => { void load().catch(() => setBusy(false)) }, [load])

  const g = greeting[lang]
  const hour = new Date().getHours()
  const hello = hour < 12 ? g.morning : hour < 18 ? g.afternoon : g.evening
  const first = me?.first_name ?? ''
  const date = new Date().toLocaleDateString(lang === 'ar' ? 'ar-AE-u-nu-latn' : 'en-GB', { weekday: 'long', day: 'numeric', month: 'long' })

  return (
    <Screen refreshing={busy} onRefresh={() => void load()}>
      <Hero title={`${hello}${first ? `, ${first}` : ''}`} subtitle={date} icon="sunny" />
      {offline && <Banner tone="info" text={t('noConnection')} />}
      {!summary ? <Loading /> : (
        <>
          <Card style={{ gap: 10 }}>
            <Row><Heading>{summary.students_present}</Heading><Body muted>{g.of} {summary.active_students} {g.here}</Body></Row>
            <ProgressBar value={summary.students_present} max={summary.active_students} />
          </Card>
          <Row gap={12}>
            <Tile label={t('notArrived')} value={summary.students_not_yet_arrived} icon="time" tone="sun" />
            <Tile label={t('absent')} value={summary.students_absent} icon="close-circle" tone="accent" />
          </Row>
          <Row gap={12}>
            <Tile label={t('onLeave')} value={summary.students_on_leave} icon="airplane" tone="sky" />
            <Tile label={t('staffPresent')} value={`${summary.staff_present}/${summary.active_staff}`} icon="people" tone="mint" />
          </Row>
          {summary.pending_leave_requests > 0 && <Banner tone="info" text={`${t('pendingLeave')}: ${summary.pending_leave_requests}`} />}
          {summary.rooms_in_breach > 0 && <Banner tone="danger" text={`${t('roomsShort')}: ${summary.rooms_in_breach}`} />}
          {can('attendance.override') && (
            <Card>
              <Row><Heading>{t('briefing')}</Heading></Row>
              {brief ? <Body>{brief}</Body> : null}
              <Btn kind="secondary" icon="sparkles" label={briefBusy ? t('briefingBusy') : t('briefingRun')} onPress={() => void askBrain()} busy={briefBusy} />
            </Card>
          )}
          <Section>{t('ratio')}</Section>
          {rooms.map((r) => (
            <Card key={r.room_id}>
              <Row><Heading>{pick(r.name, lang)}</Heading><Pill tone={r.is_breach ? 'danger' : 'ok'} label={r.is_breach ? t('short') : t('ok')} /></Row>
              <ProgressBar value={r.staff_present} max={Math.max(1, r.staff_required)} tone={r.is_breach ? 'danger' : 'ok'} />
              <Body muted>{t('staffNeeded', { present: r.staff_present, required: r.staff_required })} · {t('childrenHere', { count: r.students_present })}</Body>
            </Card>
          ))}
        </>
      )}
    </Screen>
  )
}

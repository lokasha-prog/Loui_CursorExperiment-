import { useCallback, useEffect, useState } from 'react'
import { api, isOffline } from '../api'
import { useAuth } from '../auth'
import { useI18n, pick } from '../i18n'
import { store } from '../core'
import { Banner, Body, Btn, Card, Heading, Loading, Pill, Row, Screen, Title } from '../ui'

interface Summary { students_present: number; students_absent: number; students_not_yet_arrived: number; students_on_leave: number; active_students: number; staff_present: number; active_staff: number; rooms_in_breach: number; pending_leave_requests: number }
interface Ratio { room_id: string; name: Record<string, string>; students_present: number; staff_present: number; staff_required: number; is_breach: boolean }

export function TodayScreen() {
  const { t, lang } = useI18n()
  const { can } = useAuth()
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

  const tile = (label: string, value: number | string) => (
    <Card style={{ flexBasis: '47%', flexGrow: 1 }}>
      <Body muted>{label}</Body>
      <Title>{String(value)}</Title>
    </Card>
  )

  return (
    <Screen refreshing={busy} onRefresh={() => void load()}>
      <Title>{t('today')}</Title>
      {offline && <Banner tone="info" text={t('noConnection')} />}
      {!summary ? <Loading /> : (
        <>
          <Row>{tile(t('present'), summary.students_present)}{tile(t('notArrived'), summary.students_not_yet_arrived)}</Row>
          <Row>{tile(t('absent'), summary.students_absent)}{tile(t('onLeave'), summary.students_on_leave)}</Row>
          <Row>{tile(t('staffPresent'), `${summary.staff_present}/${summary.active_staff}`)}{tile(t('pendingLeave'), summary.pending_leave_requests)}</Row>
          {summary.rooms_in_breach > 0 && <Banner tone="danger" text={`${t('roomsShort')}: ${summary.rooms_in_breach}`} />}
          {can('attendance.override') && (
            <Card>
              <Heading>{t('briefing')}</Heading>
              {brief ? <Body>{brief}</Body> : null}
              <Btn kind="secondary" label={briefBusy ? t('briefingBusy') : t('briefingRun')} onPress={() => void askBrain()} busy={briefBusy} />
            </Card>
          )}
          <Heading>{t('ratio')}</Heading>
          {rooms.map((r) => (
            <Card key={r.room_id}>
              <Row><Body bold>{pick(r.name, lang)}</Body><Pill tone={r.is_breach ? 'danger' : 'ok'} label={r.is_breach ? t('short') : t('ok')} /></Row>
              <Body muted>{t('staffNeeded', { present: r.staff_present, required: r.staff_required })} · {t('childrenHere', { count: r.students_present })}</Body>
            </Card>
          ))}
        </>
      )}
    </Screen>
  )
}

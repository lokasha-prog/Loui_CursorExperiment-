import { CameraView, useCameraPermissions } from 'expo-camera'
import { useCallback, useEffect, useState } from 'react'
import { View } from 'react-native'
import { api, ApiError } from '../api'
import { useI18n } from '../i18n'
import { doOrQueue, queue } from '../queue'
import { Banner, Body, Btn, Card, Field, Heading, Pill, Row, Screen, Title } from '../ui'

interface Row_ { id: string; person_name: string | null; person_type: string; check_in_at: string | null; check_out_at: string | null; status: 'pending' | 'present' | 'absent' | 'late' | 'early_leave' }

const tone = (s: Row_['status']) => (s === 'present' ? 'ok' : s === 'late' || s === 'early_leave' ? 'warn' : s === 'absent' ? 'danger' : 'neutral')
const time = (iso: string | null) => (iso ? new Date(iso).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' }) : '')

export function AttendanceScreen() {
  const { t } = useI18n()
  const [rows, setRows] = useState<Row_[]>([])
  const [code, setCode] = useState('')
  const [camera, setCamera] = useState(false)
  const [permission, requestPermission] = useCameraPermissions()
  const [message, setMessage] = useState<{ text: string; tone: 'info' | 'danger' | 'warn' } | null>(null)
  const [waiting, setWaiting] = useState(0)
  const [busy, setBusy] = useState(false)

  const load = useCallback(async () => {
    try {
      setRows(await api.page<Row_>('/attendance?limit=100'))
    } catch {
      /* offline: keep the list we have */
    }
    setWaiting((await queue.list()).length)
  }, [])

  useEffect(() => { void load() }, [load])

  const act = async (kind: 'check-in' | 'check-out', barcode: string) => {
    if (!barcode.trim()) return
    setBusy(true)
    try {
      const result = await doOrQueue(`/attendance/actions/${kind}`, { barcode: barcode.trim() }, `${kind} ${barcode}`)
      setMessage({ text: result === 'done' ? t(kind === 'check-in' ? 'checkedIn' : 'checkedOut') : t('savedForLater'), tone: result === 'done' ? 'info' : 'warn' })
      setCode('')
      await load()
    } catch (error) {
      setMessage({ text: error instanceof ApiError ? error.message : t('retry'), tone: 'danger' })
    } finally {
      setBusy(false)
    }
  }

  const flush = async () => {
    setBusy(true)
    const r = await queue.flush()
    setMessage({ text: `${t('sent', { count: r.sent })}${r.dropped ? ` · ${t('dropped', { count: r.dropped })}` : ''}`, tone: 'info' })
    setBusy(false)
    await load()
  }

  const onScan = (data: string) => {
    setCamera(false)
    setCode(data)
    setMessage({ text: `${t('scanned')}: ${data}`, tone: 'info' })
  }

  return (
    <Screen refreshing={busy} onRefresh={() => void load()}>
      <Title>{t('tabs.attendance')}</Title>
      {message && <Banner tone={message.tone} text={message.text} />}
      <Card>
        <Heading>{t('scan')}</Heading>
        {camera ? (
          permission?.granted ? (
            <View style={{ height: 240, borderRadius: 12, overflow: 'hidden' }}>
              <CameraView style={{ flex: 1 }} barcodeScannerSettings={{ barcodeTypes: ['qr', 'code128'] }} onBarcodeScanned={({ data }) => onScan(data)} />
            </View>
          ) : <Banner text={t('cameraNeeded')} />
        ) : null}
        <Btn kind="secondary" label={camera ? t('closeCamera') : t('openCamera')} onPress={() => { if (!permission?.granted) void requestPermission(); setCamera((c) => !c) }} />
        {camera && <Body muted>{t('scanHint')}</Body>}
        <Field label={t('typeCode')} value={code} onChangeText={setCode} autoCapitalize="characters" />
        <Row>
          <View style={{ flex: 1 }}><Btn label={t('checkIn')} onPress={() => void act('check-in', code)} busy={busy} disabled={!code} /></View>
          <View style={{ flex: 1 }}><Btn kind="secondary" label={t('checkOut')} onPress={() => void act('check-out', code)} busy={busy} disabled={!code} /></View>
        </Row>
      </Card>
      {waiting > 0 && (
        <Card>
          <Body bold>{t('waiting', { count: waiting })}</Body>
          <Btn kind="secondary" label={t('sendNow')} onPress={() => void flush()} busy={busy} />
        </Card>
      )}
      <Heading>{t('list')}</Heading>
      {rows.length === 0 ? <Body muted>{t('nobody')}</Body> : rows.map((r) => (
        <Card key={r.id}>
          <Row><Body bold>{r.person_name ?? '—'}</Body><Pill tone={tone(r.status)} label={t(`status.${r.status}`)} /></Row>
          <Body muted>{[time(r.check_in_at), time(r.check_out_at)].filter(Boolean).join(' → ')}</Body>
        </Card>
      ))}
    </Screen>
  )
}


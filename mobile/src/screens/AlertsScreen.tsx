import { useCallback, useEffect, useState } from 'react'
import { Switch, View } from 'react-native'
import { api, ApiError } from '../api'
import { cached } from '../cache'
import { useAuth } from '../auth'
import { pick, useI18n } from '../i18n'
import { enablePush, pushStatus } from '../push'
import { Banner, Body, Btn, Card, Empty, Heading, Hero, Pill, Row, Screen, Title } from '../ui'

interface Alert { id: string; tier: string; title: Record<string, string>; body: Record<string, string>; read: boolean; created_at: string }
interface Pref { tier: 'critical' | 'operational' | 'informational'; in_app: boolean; email: boolean }

export function AlertsScreen() {
  const { t, lang } = useI18n()
  const { can } = useAuth()
  const [alerts, setAlerts] = useState<Alert[]>([])
  const [prefs, setPrefs] = useState<Pref[]>([])
  const [push, setPush] = useState<'on' | 'off' | 'unavailable'>('off')
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)

  const load = useCallback(async () => {
    setBusy(true)
    try {
      setAlerts((await cached('alerts', () => api.page<Alert>('/notifications?limit=30'))).data ?? [])
      if (can('notifications.preferences')) setPrefs(await api.get<Pref[]>('/notifications/preferences'))
    } catch { /* offline */ }
    setPush(await pushStatus())
    setBusy(false)
  }, [can])
  useEffect(() => { void load() }, [load])

  const save = async (next: Pref[]) => {
    setPrefs(next)
    try { await api.put('/notifications/preferences', { preferences: next }) } catch (e) { setError(e instanceof ApiError ? e.message : t('retry')) }
  }
  const markAll = async () => {
    await api.post('/notifications/actions/read-all')
    await load()
  }

  return (
    <Screen refreshing={busy} onRefresh={() => void load()}>
      <Hero title={t('tabs.alerts')} icon="notifications" />
      {error && <Banner tone="danger" text={error} />}
      <Card>
        <Row><Heading>{t('alerts')}</Heading>{alerts.some((a) => !a.read) && <Btn kind="secondary" label={t('markAllRead')} onPress={() => void markAll()} />}</Row>
        {alerts.length === 0 ? <Empty icon="notifications-off-outline" text={t('noAlerts')} /> : alerts.map((a) => (
          <View key={a.id} style={{ gap: 2, paddingVertical: 4, opacity: a.read ? 0.65 : 1 }}>
            <Row>{a.tier === 'critical' && <Pill tone="danger" label={t('critical')} />}{!a.read && <Pill tone="info" label={t('unread')} />}<Body bold>{pick(a.title, lang)}</Body></Row>
            <Body muted>{pick(a.body, lang)}</Body>
          </View>
        ))}
      </Card>
      {push !== 'unavailable' && (
        <Card>
          <Heading>{t('pushOn')}</Heading>
          {push === 'on' ? <Pill tone="ok" label={t('pushOn')} /> : <><Body muted>{t('pushOff')}</Body><Btn label={t('pushEnable')} onPress={() => void enablePush().then(async () => setPush(await pushStatus()))} /></>}
        </Card>
      )}
      {prefs.length > 0 && (
        <Card>
          <Heading>{t('preferences')}</Heading>
          {prefs.map((p) => (
            <View key={p.tier} style={{ gap: 4, paddingVertical: 4 }}>
              <Body bold>{t(`tiers.${p.tier}`)}</Body>
              <Row><Body>{t('inApp')}</Body><Switch accessibilityLabel={`${t(`tiers.${p.tier}`)} ${t('inApp')}`} disabled={p.tier === 'critical'} value={p.in_app} onValueChange={(v) => void save(prefs.map((x) => (x.tier === p.tier ? { ...x, in_app: v } : x)))} /></Row>
              <Row><Body>{t('byEmail')}</Body><Switch accessibilityLabel={`${t(`tiers.${p.tier}`)} ${t('byEmail')}`} disabled={p.tier === 'critical'} value={p.email} onValueChange={(v) => void save(prefs.map((x) => (x.tier === p.tier ? { ...x, email: v } : x)))} /></Row>
              {p.tier === 'critical' && <Body muted>{t('urgentLocked')}</Body>}
            </View>
          ))}
        </Card>
      )}
    </Screen>
  )
}

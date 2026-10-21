import { useCallback, useEffect, useState } from 'react'
import { Linking, Switch, View } from 'react-native'
import { api, isOffline } from '../api'
import { useAuth } from '../auth'
import { store } from '../core'
import { pick, useI18n } from '../i18n'
import { Banner, Body, Btn, Card, Field, Heading, Pill, Row, Screen, Title } from '../ui'
import { LanguageSwitch } from './LoginScreen'

interface Alert { id: string; tier: string; title: Record<string, string>; body: Record<string, string>; read: boolean; created_at: string }
interface Contact { name: string; relationship: string; phone: string | null; can_pickup: boolean; is_primary: boolean }
interface Child { name: string; arabic_name: string | null; room: string | null; allergies: string | null; medical: string | null; dietary: string | null; contacts: Contact[] }
interface Emergency { centre_name: string; generated_at: string; children: Child[] }

const CACHE = 'nms.emergency'

export function MoreScreen() {
  const { t, lang, setLang } = useI18n()
  const { me, signOut, biometric, canUseBiometric, setBiometric } = useAuth()
  const [alerts, setAlerts] = useState<Alert[]>([])
  const [emergency, setEmergency] = useState<Emergency | null>(null)
  const [search, setSearch] = useState('')
  const [busy, setBusy] = useState(false)
  const [offline, setOffline] = useState(false)

  const loadAlerts = useCallback(async () => {
    try { setAlerts(await api.page<Alert>('/notifications?limit=20')) } catch { /* offline */ }
  }, [])

  const refreshEmergency = useCallback(async () => {
    setBusy(true)
    try {
      const data = await api.get<Emergency>('/emergency/data')
      await store.set(CACHE, data)
      setEmergency(data)
      setOffline(false)
    } catch (error) {
      if (isOffline(error)) setOffline(true)
    } finally {
      setBusy(false)
    }
  }, [])

  useEffect(() => {
    void loadAlerts()
    void store.get<Emergency>(CACHE).then((cached) => cached && setEmergency(cached))
  }, [loadAlerts])

  const shown = (emergency?.children ?? []).filter((c) => `${c.name} ${c.arabic_name ?? ''}`.toLowerCase().includes(search.toLowerCase()))

  return (
    <Screen>
      <Title>{t('more')}</Title>
      <Card>
        <Heading>{t('profile')}</Heading>
        <Body>{[me?.first_name, me?.last_name].filter(Boolean).join(' ') || me?.email}</Body>
        <Body muted>{me?.email} · {pick(me?.tenant.name, lang)}</Body>
      </Card>

      <Card>
        <Heading>{t('alerts')}</Heading>
        {alerts.length === 0 ? <Body muted>{t('noAlerts')}</Body> : alerts.map((a) => (
          <View key={a.id} style={{ gap: 2, paddingVertical: 4 }}>
            <Row>{a.tier === 'critical' && <Pill tone="danger" label={t('critical')} />}<Body bold>{pick(a.title, lang)}</Body></Row>
            <Body muted>{pick(a.body, lang)}</Body>
          </View>
        ))}
      </Card>

      <Card>
        <Heading>{t('emergency')}</Heading>
        <Body muted>{t('emergencyBody')}</Body>
        {offline && <Banner tone="info" text={t('noConnection')} />}
        {emergency ? <Body muted>{t('updated', { when: new Date(emergency.generated_at).toLocaleString() })}</Body> : <Body muted>{t('noCache')}</Body>}
        <Btn kind="secondary" label={busy ? t('refreshing') : t('refresh')} onPress={() => void refreshEmergency()} busy={busy} />
        {emergency && <Field label={t('searchChild')} value={search} onChangeText={setSearch} />}
        {shown.map((c) => (
          <Card key={c.name} style={{ backgroundColor: '#F8FAFC' }}>
            <Body bold>{lang === 'ar' && c.arabic_name ? c.arabic_name : c.name}{c.room ? ` · ${c.room}` : ''}</Body>
            {c.allergies ? <Body>{t('allergies')}: {c.allergies}</Body> : null}
            {c.medical ? <Body>{t('medical')}: {c.medical}</Body> : null}
            {c.dietary ? <Body>{t('dietary')}: {c.dietary}</Body> : null}
            <Body muted>{t('contacts')}</Body>
            {c.contacts.map((p, i) => (
              <Row key={i}>
                <Body>{p.name} ({p.relationship}){p.is_primary ? ` · ${t('primary')}` : ''}{p.can_pickup ? ` · ${t('pickup')}` : ''}</Body>
                {p.phone ? <Btn kind="secondary" label={`${t('call')} ${p.phone}`} onPress={() => void Linking.openURL(`tel:${p.phone}`)} /> : null}
              </Row>
            ))}
          </Card>
        ))}
      </Card>

      <Card>
        <Heading>{t('language')}</Heading>
        <LanguageSwitch lang={lang} setLang={setLang} />
        {canUseBiometric && (
          <Row>
            <Body>{t('biometricOn')}</Body>
            <Switch accessibilityLabel={t('biometricOn')} value={biometric} onValueChange={(v) => void setBiometric(v)} />
          </Row>
        )}
      </Card>
      <Btn kind="danger" label={t('signOut')} onPress={() => void signOut()} />
    </Screen>
  )
}

import { useCallback, useEffect, useState } from 'react'
import { Linking, Pressable, Switch, Text, View } from 'react-native'
import { api, isOffline } from '../api'
import { useAuth } from '../auth'
import { store } from '../core'
import { pick, useI18n } from '../i18n'
import { Banner, Body, Btn, Card, Empty, Field, Heading, Hero, IconChip, Pill, Row, Screen, Section, type Tone, color } from '../ui'
import { LanguageSwitch } from './LoginScreen'
import { ApprovalsOps, CalendarOps, CentreOps, ChildrenOps, EnquiriesOps, InvoicesOps, LeaveOps, NewslettersOps, opsWords } from './ops'

interface Alert { id: string; tier: string; title: Record<string, string>; body: Record<string, string>; read: boolean; created_at: string }
interface Contact { name: string; relationship: string; phone: string | null; can_pickup: boolean; is_primary: boolean }
interface Child { name: string; arabic_name: string | null; room: string | null; allergies: string | null; medical: string | null; dietary: string | null; contacts: Contact[] }
interface Emergency { centre_name: string; generated_at: string; children: Child[] }

const CACHE = 'nms.emergency'

export function MoreScreen() {
  const { t, lang, setLang } = useI18n()
  const { me, signOut, biometric, canUseBiometric, setBiometric, can } = useAuth()
  const staff = can('attendance.write')
  const [alerts, setAlerts] = useState<Alert[]>([])
  const [emergency, setEmergency] = useState<Emergency | null>(null)
  const [search, setSearch] = useState('')
  const [busy, setBusy] = useState(false)
  const [offline, setOffline] = useState(false)
  const [view, setView] = useState<string | null>(null)
  const w = opsWords(lang)
  const parent = !!me?.roles.includes('parent')
  type Item = { id: string; icon: React.ComponentProps<typeof IconChip>['name']; tone: Tone; title: string; show: boolean }
  const menu = ([
    { id: 'centre', icon: 'stats-chart', tone: 'primary', title: w.centre, show: can('attendance.override') },
    { id: 'children', icon: 'people', tone: 'sun', title: w.children, show: !parent && can('students.read') },
    { id: 'leave', icon: 'airplane', tone: 'sky', title: w.leave, show: !parent && can('attendance.read') },
    { id: 'approvals', icon: 'checkmark-done', tone: 'mint', title: w.approvals, show: can('activities.approve') },
    { id: 'enquiries', icon: 'call', tone: 'accent', title: w.enquiries, show: can('enquiries.read') },
    { id: 'invoices', icon: 'wallet', tone: 'lilac', title: w.invoices, show: can('billing.read') },
    { id: 'calendar', icon: 'calendar', tone: 'sky', title: w.calendar, show: can('calendar.read') },
    { id: 'newsletters', icon: 'newspaper', tone: 'sun', title: w.newsletters, show: can('messaging.read') },
  ] satisfies Item[]).filter((m) => m.show)

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
    if (staff) void loadAlerts()
    void store.get<Emergency>(CACHE).then((cached) => cached && setEmergency(cached))
  }, [loadAlerts, staff])

  const shown = (emergency?.children ?? []).filter((c) => `${c.name} ${c.arabic_name ?? ''}`.toLowerCase().includes(search.toLowerCase()))

  const back = () => setView(null)
  if (view === 'centre') return <CentreOps onBack={back} />
  if (view === 'children') return <ChildrenOps onBack={back} />
  if (view === 'leave') return <LeaveOps onBack={back} />
  if (view === 'approvals') return <ApprovalsOps onBack={back} />
  if (view === 'enquiries') return <EnquiriesOps onBack={back} />
  if (view === 'invoices') return <InvoicesOps onBack={back} />
  if (view === 'calendar') return <CalendarOps onBack={back} />
  if (view === 'newsletters') return <NewslettersOps onBack={back} />

  return (
    <Screen>
      <Hero title={t('more')} subtitle={[me?.first_name, me?.last_name].filter(Boolean).join(' ') || me?.email} icon="grid" />
      <View style={{ flexDirection: 'row', flexWrap: 'wrap', gap: 12 }}>
        {menu.map((m) => (
          <Pressable key={m.id} accessibilityRole="button" accessibilityLabel={m.title} onPress={() => setView(m.id)} style={({ pressed }) => ({ flexBasis: '47%', flexGrow: 1, opacity: pressed ? 0.85 : 1 })}>
            <Card style={{ gap: 12, minHeight: 112 }}>
              <IconChip name={m.icon} tone={m.tone} size={46} />
              <Text style={{ fontSize: 15, fontWeight: '800', color: color.ink }}>{m.title}</Text>
            </Card>
          </Pressable>
        ))}
      </View>
      <Card>
        <Heading>{t('profile')}</Heading>
        <Body>{[me?.first_name, me?.last_name].filter(Boolean).join(' ') || me?.email}</Body>
        <Body muted>{me?.email} · {pick(me?.tenant.name, lang)}</Body>
      </Card>

      {staff && <Card>
        <Heading>{t('alerts')}</Heading>
        {alerts.length === 0 ? <Empty icon="notifications-off-outline" text={t('noAlerts')} /> : alerts.map((a) => (
          <View key={a.id} style={{ gap: 2, paddingVertical: 4 }}>
            <Row>{a.tier === 'critical' && <Pill tone="danger" label={t('critical')} />}<Body bold>{pick(a.title, lang)}</Body></Row>
            <Body muted>{pick(a.body, lang)}</Body>
          </View>
        ))}
      </Card>}

      {staff && <Card>
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
      </Card>}

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

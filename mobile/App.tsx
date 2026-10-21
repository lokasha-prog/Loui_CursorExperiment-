import { StatusBar } from 'expo-status-bar'
import { useEffect, useState } from 'react'
import { Pressable, SafeAreaView, Text, View } from 'react-native'
import { AuthProvider, useAuth } from './src/auth'
import { I18nProvider, useI18n, type Key } from './src/i18n'
import { AlertsScreen } from './src/screens/AlertsScreen'
import { LearningScreen } from './src/screens/LearningScreen'
import { ObserveScreen } from './src/screens/ObserveScreen'
import { AttendanceScreen } from './src/screens/AttendanceScreen'
import { FeedScreen } from './src/screens/FeedScreen'
import { LoginScreen } from './src/screens/LoginScreen'
import { MessagesScreen } from './src/screens/MessagesScreen'
import { MoreScreen } from './src/screens/MoreScreen'
import { ReportsScreen } from './src/screens/ReportsScreen'
import { TodayScreen } from './src/screens/TodayScreen'
import { queue } from './src/queue'
import { Banner, Body, Btn, Loading, color } from './src/ui'

type Tab = { id: string; label: Key; icon: string; View: () => JSX.Element }

const PARENT_TABS = [
  { id: 'feed', label: 'tabs.feed', icon: '✎', View: FeedScreen },
  { id: 'reports', label: 'tabs.reports', icon: '☰', View: ReportsScreen },
  { id: 'learning', label: 'tabs.learning', icon: '🌱', View: LearningScreen },
  { id: 'messages', label: 'tabs.messages', icon: '✉', View: MessagesScreen },
  { id: 'alerts', label: 'tabs.alerts', icon: '🔔', View: AlertsScreen },
  { id: 'more', label: 'tabs.more', icon: '≡', View: MoreScreen },
] as const satisfies readonly Tab[]

const STAFF_TABS = [
  { id: 'dashboard', label: 'tabs.dashboard', icon: '◉', View: TodayScreen },
  { id: 'attendance', label: 'tabs.attendance', icon: '✓', View: AttendanceScreen },
  { id: 'feed', label: 'tabs.feed', icon: '✎', View: FeedScreen },
  { id: 'observe', label: 'tabs.observe', icon: '🔍', View: ObserveScreen },
  { id: 'messages', label: 'tabs.messages', icon: '✉', View: MessagesScreen },
  { id: 'more', label: 'tabs.more', icon: '≡', View: MoreScreen },
] as const satisfies readonly Tab[]

function Shell() {
  const { t, row } = useI18n()
  const { me, ready, locked, unlock } = useAuth()
  const [tab, setTab] = useState<string>('')
  const [waiting, setWaiting] = useState(0)

  // Whatever was saved with no signal is sent as soon as there is some.
  useEffect(() => {
    if (!me) return undefined
    const run = async () => { await queue.flush(); setWaiting((await queue.list()).length) }
    void run()
    const id = setInterval(() => void run(), 20000)
    return () => clearInterval(id)
  }, [me])

  if (!ready) return <Loading />
  if (locked) {
    return (
      <SafeAreaView style={{ flex: 1, justifyContent: 'center', padding: 24, gap: 12 }}>
        <Body bold>{t('appName')}</Body>
        <Btn label={t('unlock')} onPress={() => void unlock()} />
      </SafeAreaView>
    )
  }
  if (!me) return <SafeAreaView style={{ flex: 1 }}><LoginScreen /></SafeAreaView>

  const tabs: readonly Tab[] = me.roles.includes('parent') ? PARENT_TABS : STAFF_TABS
  const current = tabs.find((x) => x.id === tab) ?? tabs[0]
  const Active = current.View
  return (
    <SafeAreaView style={{ flex: 1, backgroundColor: color.bg }}>
      {waiting > 0 && <Banner tone="info" text={t('unsent', { count: waiting })} />}
      <View style={{ flex: 1 }}><Active /></View>
      <View accessibilityRole="tablist" style={{ flexDirection: row, borderTopWidth: 1, borderColor: color.line, backgroundColor: color.white }}>
        {tabs.map((x) => (
          <Pressable key={x.id} accessibilityRole="tab" accessibilityState={{ selected: current.id === x.id }} onPress={() => setTab(x.id)} style={{ flex: 1, alignItems: 'center', paddingVertical: 10, minHeight: 56 }}>
            <Text style={{ fontSize: 18, color: current.id === x.id ? color.primary : color.muted }}>{x.icon}</Text>
            <Text style={{ fontSize: 11, fontWeight: '700', color: current.id === x.id ? color.primary : color.muted }}>{t(x.label)}</Text>
          </Pressable>
        ))}
      </View>
    </SafeAreaView>
  )
}

export default function App() {
  return (
    <I18nProvider>
      <AuthProvider>
        <StatusBar style="dark" />
        <Shell />
      </AuthProvider>
    </I18nProvider>
  )
}

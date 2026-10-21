import { Ionicons } from '@expo/vector-icons'
import { StatusBar } from 'expo-status-bar'
import { useEffect, useState } from 'react'
import { Platform, Pressable, SafeAreaView, StatusBar as RNStatusBar, Text, View } from 'react-native'
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
import { onAlertTapped } from './src/push'
import { queue } from './src/queue'
import { Banner, Body, Btn, Loading, color } from './src/ui'

type Icon = keyof typeof Ionicons.glyphMap
type Tab = { id: string; label: Key; icon: Icon; View: () => JSX.Element }

/** On Android the system bar sits on top of the screen, so the top of the app is pushed down to clear it. */
const topInset = Platform.OS === 'android' ? RNStatusBar.currentHeight ?? 24 : 0

const PARENT_TABS = [
  { id: 'feed', label: 'tabs.feed', icon: 'images', View: FeedScreen },
  { id: 'reports', label: 'tabs.reports', icon: 'document-text', View: ReportsScreen },
  { id: 'learning', label: 'tabs.learning', icon: 'leaf', View: LearningScreen },
  { id: 'messages', label: 'tabs.messages', icon: 'chatbubbles', View: MessagesScreen },
  { id: 'alerts', label: 'tabs.alerts', icon: 'notifications', View: AlertsScreen },
  { id: 'more', label: 'tabs.more', icon: 'grid', View: MoreScreen },
] as const satisfies readonly Tab[]

const STAFF_TABS = [
  { id: 'dashboard', label: 'tabs.dashboard', icon: 'home', View: TodayScreen },
  { id: 'attendance', label: 'tabs.attendance', icon: 'checkmark-circle', View: AttendanceScreen },
  { id: 'feed', label: 'tabs.feed', icon: 'images', View: FeedScreen },
  { id: 'observe', label: 'tabs.observe', icon: 'eye', View: ObserveScreen },
  { id: 'messages', label: 'tabs.messages', icon: 'chatbubbles', View: MessagesScreen },
  { id: 'more', label: 'tabs.more', icon: 'grid', View: MoreScreen },
] as const satisfies readonly Tab[]

function Shell() {
  const { t, row } = useI18n()
  const { me, ready, locked, unlock } = useAuth()
  const [tab, setTab] = useState<string>('')
  const [waiting, setWaiting] = useState(0)

  // Tapping an alert opens the screen it is about.
  useEffect(() => onAlertTapped((link) => {
    const map: Record<string, string> = { '/feed': 'feed', '/messages': 'messages', '/learning': me?.roles.includes('parent') ? 'learning' : 'feed', '/attendance': 'attendance', '/notifications': 'alerts' }
    const id = map[link]
    if (id) setTab(id)
  }), [me])

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
      <SafeAreaView style={{ flex: 1, justifyContent: 'center', padding: 24, gap: 12, paddingTop: topInset }}>
        <Body bold>{t('appName')}</Body>
        <Btn label={t('unlock')} onPress={() => void unlock()} />
      </SafeAreaView>
    )
  }
  if (!me) return <SafeAreaView style={{ flex: 1, paddingTop: topInset, backgroundColor: color.bg }}><LoginScreen /></SafeAreaView>

  const tabs: readonly Tab[] = me.roles.includes('parent') ? PARENT_TABS : STAFF_TABS
  const current = tabs.find((x) => x.id === tab) ?? tabs[0]
  const Active = current.View
  return (
    <SafeAreaView style={{ flex: 1, backgroundColor: color.bg, paddingTop: topInset }}>
      {waiting > 0 && <Banner tone="info" text={t('unsent', { count: waiting })} />}
      <View style={{ flex: 1 }}><Active /></View>
      <View accessibilityRole="tablist" style={{ flexDirection: row, backgroundColor: color.white, paddingHorizontal: 6, paddingTop: 8, paddingBottom: 10, borderTopLeftRadius: 26, borderTopRightRadius: 26, shadowColor: '#0F2A43', shadowOpacity: 0.1, shadowRadius: 16, shadowOffset: { width: 0, height: -4 }, elevation: 12 }}>
        {tabs.map((x) => {
          const active = current.id === x.id
          return (
            <Pressable key={x.id} accessibilityRole="tab" accessibilityLabel={t(x.label)} accessibilityState={{ selected: active }} onPress={() => setTab(x.id)} style={{ flex: 1, alignItems: 'center', gap: 3, minHeight: 56, justifyContent: 'center' }}>
              <View style={{ width: 52, height: 30, borderRadius: 15, alignItems: 'center', justifyContent: 'center', backgroundColor: active ? color.primarySoft : 'transparent' }}>
                <Ionicons name={x.icon} size={22} color={active ? color.primary : color.muted} />
              </View>
              <Text numberOfLines={1} style={{ fontSize: 11, fontWeight: active ? '800' : '600', color: active ? color.primary : color.muted }}>{t(x.label)}</Text>
            </Pressable>
          )
        })}
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

import { StatusBar } from 'expo-status-bar'
import { useState } from 'react'
import { Pressable, SafeAreaView, Text, View } from 'react-native'
import { AuthProvider, useAuth } from './src/auth'
import { I18nProvider, useI18n, type Key } from './src/i18n'
import { AttendanceScreen } from './src/screens/AttendanceScreen'
import { FeedScreen } from './src/screens/FeedScreen'
import { LoginScreen } from './src/screens/LoginScreen'
import { MessagesScreen } from './src/screens/MessagesScreen'
import { MoreScreen } from './src/screens/MoreScreen'
import { TodayScreen } from './src/screens/TodayScreen'
import { Body, Btn, Loading, color } from './src/ui'

const TABS = [
  { id: 'dashboard', label: 'tabs.dashboard', icon: '◉', View: TodayScreen },
  { id: 'attendance', label: 'tabs.attendance', icon: '✓', View: AttendanceScreen },
  { id: 'feed', label: 'tabs.feed', icon: '✎', View: FeedScreen },
  { id: 'messages', label: 'tabs.messages', icon: '✉', View: MessagesScreen },
  { id: 'more', label: 'tabs.more', icon: '≡', View: MoreScreen },
] as const satisfies readonly { id: string; label: Key; icon: string; View: () => JSX.Element }[]

function Shell() {
  const { t, row } = useI18n()
  const { me, ready, locked, unlock } = useAuth()
  const [tab, setTab] = useState<(typeof TABS)[number]['id']>('dashboard')

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

  const Active = TABS.find((x) => x.id === tab)!.View
  return (
    <SafeAreaView style={{ flex: 1, backgroundColor: color.bg }}>
      <View style={{ flex: 1 }}><Active /></View>
      <View accessibilityRole="tablist" style={{ flexDirection: row, borderTopWidth: 1, borderColor: color.line, backgroundColor: color.white }}>
        {TABS.map((x) => (
          <Pressable key={x.id} accessibilityRole="tab" accessibilityState={{ selected: tab === x.id }} onPress={() => setTab(x.id)} style={{ flex: 1, alignItems: 'center', paddingVertical: 10, minHeight: 56 }}>
            <Text style={{ fontSize: 18, color: tab === x.id ? color.primary : color.muted }}>{x.icon}</Text>
            <Text style={{ fontSize: 11, fontWeight: '700', color: tab === x.id ? color.primary : color.muted }}>{t(x.label)}</Text>
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

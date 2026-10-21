import Constants from 'expo-constants'
import * as Notifications from 'expo-notifications'
import { Platform } from 'react-native'
import { api } from './api'
import { store } from './core'

const KEY = 'nms.pushToken'

Notifications.setNotificationHandler({ handleNotification: async () => ({ shouldShowAlert: true, shouldPlaySound: true, shouldSetBadge: false }) })

/** 'unavailable' on the web preview, 'on' once this phone is registered for alerts, 'off' otherwise. */
export async function pushStatus(): Promise<'on' | 'off' | 'unavailable'> {
  if (Platform.OS === 'web') return 'unavailable'
  const granted = (await Notifications.getPermissionsAsync()).granted
  return granted && (await store.get<string>(KEY)) ? 'on' : 'off'
}

/** Asks the person for permission, gets this phone's alert address and tells the server. Quietly does nothing if the build has no store project yet. */
export async function enablePush(): Promise<void> {
  if (Platform.OS === 'web') return
  const asked = await Notifications.requestPermissionsAsync()
  if (!asked.granted) return
  const projectId = Constants.expoConfig?.extra?.eas?.projectId ?? Constants.easConfig?.projectId
  if (!projectId) return
  const token = (await Notifications.getExpoPushTokenAsync({ projectId })).data
  await api.post('/notifications/devices', { token, platform: Platform.OS })
  await store.set(KEY, token)
}

/** On sign-out the phone stops receiving this person's alerts. */
export async function disablePush(): Promise<void> {
  const token = await store.get<string>(KEY)
  if (!token) return
  try { await api.post('/notifications/devices/actions/remove', { token }) } catch { /* the token is dropped locally anyway */ }
  await store.remove(KEY)
}

import AsyncStorage from '@react-native-async-storage/async-storage'
import { isOffline } from './api'
import { store } from './core'

const PREFIX = 'cache.'

/**
 * Reads from the server and keeps a copy on the phone. With no signal the saved copy is returned instead, so screens still open.
 * Anything other than "no connection" (for example the server saying no) is passed on.
 */
export async function cached<T>(key: string, fetcher: () => Promise<T>): Promise<{ data: T | null; offline: boolean }> {
  try {
    const data = await fetcher()
    await store.set(PREFIX + key, data)
    return { data, offline: false }
  } catch (error) {
    if (!isOffline(error)) throw error
    return { data: await store.get<T>(PREFIX + key), offline: true }
  }
}

/** Forgets every saved copy, used at sign-out so the next person on a shared phone sees nothing of the last. */
export async function clearCache() {
  try {
    const keys = (await AsyncStorage.getAllKeys()).filter((k) => k.startsWith(PREFIX) || k.startsWith('nms.today') || k.startsWith('nms.emergency') || k === 'nms.queue')
    await AsyncStorage.multiRemove(keys)
  } catch {
    // Nothing to clear.
  }
}

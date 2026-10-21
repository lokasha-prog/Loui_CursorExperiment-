import AsyncStorage from '@react-native-async-storage/async-storage'
import * as SecureStore from 'expo-secure-store'
import { Platform } from 'react-native'

/** Where the centre's server is. On an Android emulator the computer is 10.0.2.2; a real phone needs the server's address (EXPO_PUBLIC_API_URL). */
export const API_URL = process.env.EXPO_PUBLIC_API_URL ?? (Platform.OS === 'android' ? 'http://10.0.2.2:5178' : 'http://localhost:5178')

/** The refresh token is a secret, so on a phone it lives in the secure store; on the web preview it falls back to local storage. */
export const secret = {
  async get(key: string): Promise<string | null> {
    try {
      return Platform.OS === 'web' ? await AsyncStorage.getItem(key) : await SecureStore.getItemAsync(key)
    } catch {
      return null
    }
  },
  async set(key: string, value: string) {
    try {
      if (Platform.OS === 'web') await AsyncStorage.setItem(key, value)
      else await SecureStore.setItemAsync(key, value)
    } catch {
      // Storage can be unavailable (private mode); the person simply signs in again next time.
    }
  },
  async remove(key: string) {
    try {
      if (Platform.OS === 'web') await AsyncStorage.removeItem(key)
      else await SecureStore.deleteItemAsync(key)
    } catch {
      // Nothing to clear.
    }
  },
}

export const store = {
  async get<T>(key: string): Promise<T | null> {
    try {
      const raw = await AsyncStorage.getItem(key)
      return raw ? (JSON.parse(raw) as T) : null
    } catch {
      return null
    }
  },
  async set(key: string, value: unknown) {
    try {
      await AsyncStorage.setItem(key, JSON.stringify(value))
    } catch {
      // Out of space or unavailable: the cache is only a convenience.
    }
  },
  async remove(key: string) {
    try {
      await AsyncStorage.removeItem(key)
    } catch {
      // Nothing to clear.
    }
  },
}

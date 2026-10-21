import * as LocalAuthentication from 'expo-local-authentication'
import { createContext, useCallback, useContext, useEffect, useMemo, useState, type ReactNode } from 'react'
import { Platform } from 'react-native'
import { api, devLogin as apiDevLogin, hasSavedSession, login as apiLogin, refreshSession, setSignedOutHandler, signOut as apiSignOut, type Me } from './api'
import { loadApiUrl, store } from './core'
import { clearCache } from './cache'
import { disablePush } from './push'

interface Auth {
  me: Me | null
  ready: boolean
  locked: boolean
  biometric: boolean
  canUseBiometric: boolean
  signIn: (email: string, password: string, tenant?: string) => Promise<{ choose: { slug: string; name: Record<string, string> }[] } | null>
  signOut: () => Promise<void>
  devSignIn: (role: string) => Promise<void>
  unlock: () => Promise<void>
  setBiometric: (on: boolean) => Promise<void>
  can: (permission: string) => boolean
}

const Context = createContext<Auth | null>(null)

/** Keeps who is signed in. If fingerprint/face unlock is on, the saved session is opened only after the phone confirms it is the owner. */
export function AuthProvider({ children }: { children: ReactNode }) {
  const [me, setMe] = useState<Me | null>(null)
  const [ready, setReady] = useState(false)
  const [locked, setLocked] = useState(false)
  const [biometric, setBiometricState] = useState(false)
  const [canUseBiometric, setCanUse] = useState(false)

  const restore = useCallback(async () => {
    const session = await refreshSession()
    if (session) setMe(session.me)
    else {
      // The server may simply be out of reach; keep the last known account so cached screens still open.
      const cached = await store.get<Me>('nms.me')
      if (cached && (await hasSavedSession())) setMe(cached)
    }
  }, [])

  useEffect(() => {
    setSignedOutHandler(() => setMe(null))
    void (async () => {
      await loadApiUrl()
      const hardware = Platform.OS !== 'web' && (await LocalAuthentication.hasHardwareAsync()) && (await LocalAuthentication.isEnrolledAsync())
      setCanUse(hardware)
      const on = (await store.get<boolean>('nms.biometric')) === true && hardware
      setBiometricState(on)
      if (on && (await hasSavedSession())) setLocked(true)
      else await restore()
      setReady(true)
    })()
  }, [restore])

  const value = useMemo<Auth>(() => ({
    me,
    ready,
    locked,
    biometric,
    canUseBiometric,
    signIn: async (email, password, tenant) => {
      const result = await apiLogin(email, password, tenant)
      if ('choose' in result) return result
      setMe(result.me)
      await store.set('nms.me', result.me)
      return null
    },
    devSignIn: async (role) => {
      const session = await apiDevLogin(role)
      setMe(session.me)
    },
    signOut: async () => {
      await disablePush()
      await apiSignOut()
      await store.remove('nms.me')
      await clearCache()
      setMe(null)
    },
    unlock: async () => {
      const check = await LocalAuthentication.authenticateAsync({ promptMessage: 'Bright Futures', cancelLabel: 'Cancel' })
      if (check.success) {
        setLocked(false)
        await restore()
      }
    },
    setBiometric: async (on) => {
      setBiometricState(on)
      await store.set('nms.biometric', on)
    },
    can: (permission) => !!me?.permissions.includes(permission),
  }), [me, ready, locked, biometric, canUseBiometric, restore])

  useEffect(() => {
    if (me) void store.set('nms.me', me)
  }, [me])

  return <Context.Provider value={value}>{children}</Context.Provider>
}

export function useAuth() {
  const ctx = useContext(Context)
  if (!ctx) throw new Error('useAuth must be used inside AuthProvider')
  return ctx
}

export { api }

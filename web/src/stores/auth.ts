import { create } from 'zustand'
import type { Me } from '@/api/types'

interface AuthState {
  /** The short-lived access token lives only in memory (never localStorage); the refresh token is an HttpOnly cookie. */
  accessToken: string | null
  me: Me | null
  /** True once the first attempt to restore a session has finished, so routes do not flash the login page. */
  ready: boolean
  setSession: (accessToken: string, me: Me) => void
  clear: () => void
  setReady: () => void
}

export const useAuth = create<AuthState>((set) => ({
  accessToken: null,
  me: null,
  ready: false,
  setSession: (accessToken, me) => set({ accessToken, me }),
  clear: () => set({ accessToken: null, me: null }),
  setReady: () => set({ ready: true }),
}))

export function can(me: Me | null, permission: string): boolean {
  return !!me && me.permissions.includes(permission)
}

export function isParentOnly(me: Me | null): boolean {
  return !!me && me.roles.includes('parent') && me.roles.length === 1
}

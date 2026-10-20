import { HubConnectionBuilder, HubConnectionState, LogLevel } from '@microsoft/signalr'
import { useQueryClient } from '@tanstack/react-query'
import { useEffect } from 'react'
import { can, useAuth } from '@/stores/auth'

const REFRESH_KEYS = ['attendance', 'live-ratios', 'dashboard-summary', 'leaves', 'activities', 'threads', 'thread', 'daily-reports', 'notifications', 'unread-count']

/**
 * Keeps screens fresh without reloading: the server pushes a short message whenever someone is checked in or out or a
 * room's staff cover changes, and the affected lists simply ask again. Only people who work with attendance connect.
 * If the connection drops, the screens keep refreshing on their own timer, so nothing depends on it.
 */
export function useLiveUpdates() {
  const queryClient = useQueryClient()
  const me = useAuth((s) => s.me)
  const allowed = can(me, 'attendance.write') || can(me, 'activities.read')
  const userId = me?.id

  useEffect(() => {
    if (!allowed) return
    const connection = new HubConnectionBuilder()
      .withUrl('/hubs/centre', { accessTokenFactory: () => useAuth.getState().accessToken ?? '' })
      .withAutomaticReconnect()
      .configureLogging(LogLevel.None)
      .build()

    const refresh = () => {
      for (const key of REFRESH_KEYS) void queryClient.invalidateQueries({ queryKey: [key] })
    }
    connection.on('attendance_changed', refresh)
    connection.on('ratio_breach', refresh)
    connection.on('ratio_restored', refresh)
    connection.on('activity_published', refresh)
    connection.on('message_received', refresh)
    connection.on('notification_created', refresh)
    connection.onreconnected(refresh)

    connection.start().catch(() => undefined)
    return () => {
      if (connection.state !== HubConnectionState.Disconnected) void connection.stop()
    }
  }, [allowed, userId, queryClient])
}

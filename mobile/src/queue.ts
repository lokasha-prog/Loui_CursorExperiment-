import { ApiError, api, isOffline } from './api'
import { store } from './core'

export interface Queued {
  id: string
  path: string
  body: unknown
  label: string
  at: string
}

const KEY = 'nms.queue'

export const queue = {
  async list(): Promise<Queued[]> {
    return (await store.get<Queued[]>(KEY)) ?? []
  },
  async add(item: Omit<Queued, 'id' | 'at'>) {
    const all = await queue.list()
    await store.set(KEY, [...all, { ...item, id: `${Date.now()}-${Math.random().toString(36).slice(2, 8)}`, at: new Date().toISOString() }])
  },
  /**
   * Sends what was saved while there was no signal. An item the server refuses (for example the child was already checked in)
   * is dropped so it cannot block the rest; one that fails only because the signal is still down stays for next time.
   */
  async flush(): Promise<{ sent: number; dropped: number; left: number }> {
    const all = await queue.list()
    const keep: Queued[] = []
    let sent = 0
    let dropped = 0
    for (const item of all) {
      try {
        await api.post(item.path, item.body)
        sent++
      } catch (error) {
        if (isOffline(error) || (error instanceof ApiError && error.status >= 500)) keep.push(item)
        else dropped++
      }
    }
    await store.set(KEY, keep)
    return { sent, dropped, left: keep.length }
  },
}

/** Do the action now; if there is no signal, save it and do it later. Returns whether it went through at once. */
export async function doOrQueue(path: string, body: unknown, label: string): Promise<'done' | 'queued'> {
  try {
    await api.post(path, body)
    return 'done'
  } catch (error) {
    if (!isOffline(error)) throw error
    await queue.add({ path, body, label })
    return 'queued'
  }
}

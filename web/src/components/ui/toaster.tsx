import { CheckCircle2, XCircle } from 'lucide-react'
import { create } from 'zustand'

interface ToastItem {
  id: number
  message: string
  tone: 'success' | 'error'
}

interface ToastState {
  items: ToastItem[]
  push: (message: string, tone: ToastItem['tone']) => void
  dismiss: (id: number) => void
}

let nextId = 1

const useToasts = create<ToastState>((set) => ({
  items: [],
  push: (message, tone) => {
    const id = nextId++
    set((state) => ({ items: [...state.items, { id, message, tone }] }))
    setTimeout(() => set((state) => ({ items: state.items.filter((i) => i.id !== id) })), 4500)
  },
  dismiss: (id) => set((state) => ({ items: state.items.filter((i) => i.id !== id) })),
}))

export const toast = {
  success: (message: string) => useToasts.getState().push(message, 'success'),
  error: (message: string) => useToasts.getState().push(message, 'error'),
}

/** Announces messages to screen readers and shows them at the end-bottom corner (mirrors automatically in Arabic). */
export function Toaster() {
  const items = useToasts((s) => s.items)
  return (
    <div aria-live="polite" className="pointer-events-none fixed bottom-4 end-4 z-[60] flex w-80 max-w-[calc(100vw-2rem)] flex-col gap-2">
      {items.map((item) => (
        <div
          key={item.id}
          role="status"
          className="pointer-events-auto flex items-start gap-2 rounded-md border bg-white px-3 py-2.5 text-sm shadow-lg animate-in fade-in slide-in-from-bottom-2"
        >
          {item.tone === 'success' ? (
            <CheckCircle2 className="mt-0.5 h-4 w-4 shrink-0 text-success" aria-hidden="true" />
          ) : (
            <XCircle className="mt-0.5 h-4 w-4 shrink-0 text-destructive" aria-hidden="true" />
          )}
          <span>{item.message}</span>
        </div>
      ))}
    </div>
  )
}

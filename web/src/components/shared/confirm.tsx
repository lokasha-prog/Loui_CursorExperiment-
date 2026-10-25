import { useEffect } from 'react'
import { create } from 'zustand'
import { ConfirmDialog } from './ConfirmDialog'

export interface ConfirmOptions {
  title: string
  description?: string
  /** The button that goes ahead, for example "Remove" or "Send now". */
  confirmLabel: string
  /** The button that backs out; "Cancel" when not given. */
  cancelLabel?: string
  tone?: 'danger' | 'neutral'
}

interface ConfirmState {
  request: (ConfirmOptions & { resolve: (ok: boolean) => void }) | null
  open: (request: ConfirmOptions & { resolve: (ok: boolean) => void }) => void
  close: () => void
}

const useConfirmStore = create<ConfirmState>((set) => ({
  request: null,
  open: (request) => set({ request }),
  close: () => set({ request: null }),
}))

/**
 * Asks "are you sure?" in the app's own confirmation box (never the browser's pop-up) and resolves to the answer.
 * Needs <ConfirmHost /> mounted once near the root.
 */
export function confirmAction(options: ConfirmOptions): Promise<boolean> {
  return new Promise((resolve) => {
    // A question still open is answered "no" before the new one replaces it.
    useConfirmStore.getState().request?.resolve(false)
    useConfirmStore.getState().open({ ...options, resolve })
  })
}

/** Shows whichever question confirmAction asked last. */
export function ConfirmHost() {
  const request = useConfirmStore((s) => s.request)
  const close = useConfirmStore((s) => s.close)
  const answer = (ok: boolean) => {
    request?.resolve(ok)
    close()
  }

  // When the host goes away (sign-out, a test ending) an unanswered question counts as "no".
  useEffect(() => () => {
    useConfirmStore.getState().request?.resolve(false)
    useConfirmStore.getState().close()
  }, [])

  return (
    <ConfirmDialog
      open={!!request}
      onOpenChange={(open) => !open && answer(false)}
      title={request?.title ?? ''}
      description={request?.description}
      confirmLabel={request?.confirmLabel ?? ''}
      cancelLabel={request?.cancelLabel}
      tone={request?.tone}
      onConfirm={() => answer(true)}
    />
  )
}

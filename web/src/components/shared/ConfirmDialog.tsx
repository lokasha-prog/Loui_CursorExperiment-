import * as DialogPrimitive from '@radix-ui/react-dialog'
import { AlertTriangle } from 'lucide-react'
import { useTranslation } from 'react-i18next'
import { Button } from '@/components/ui/button'
import { cn } from '@/lib/utils'

/**
 * The app's own "are you sure?" box, used instead of the browser's confirm pop-up.
 * It sits above any open dialog, focuses Cancel first, and keeps the button busy while the action runs.
 */
export function ConfirmDialog({
  open,
  onOpenChange,
  title,
  description,
  confirmLabel,
  onConfirm,
  busy = false,
  busyLabel,
  cancelLabel,
  tone = 'danger',
}: {
  open: boolean
  onOpenChange: (open: boolean) => void
  title: string
  description?: string
  confirmLabel: string
  onConfirm: () => void
  busy?: boolean
  busyLabel?: string
  cancelLabel?: string
  tone?: 'danger' | 'neutral'
}) {
  const { t } = useTranslation()
  return (
    <DialogPrimitive.Root open={open} onOpenChange={(next) => !busy && onOpenChange(next)}>
      <DialogPrimitive.Portal>
        <DialogPrimitive.Overlay className="fixed inset-0 z-[60] bg-slate-900/50 data-[state=open]:animate-in data-[state=open]:fade-in-0" />
        <DialogPrimitive.Content
          role="alertdialog"
          onOpenAutoFocus={(e) => {
            e.preventDefault()
            ;(e.currentTarget as HTMLElement).querySelector<HTMLButtonElement>('[data-cancel]')?.focus()
          }}
          className="fixed start-1/2 top-1/2 z-[60] w-[calc(100%-2rem)] max-w-md -translate-y-1/2 rounded-lg border bg-card p-6 shadow-2xl ltr:-translate-x-1/2 rtl:translate-x-1/2 data-[state=open]:animate-in data-[state=open]:fade-in-0 data-[state=open]:zoom-in-95"
        >
          <div className="flex gap-4">
            <span
              className={cn(
                'flex h-10 w-10 shrink-0 items-center justify-center rounded-full',
                tone === 'danger' ? 'bg-red-50 text-red-600' : 'bg-slate-100 text-slate-600',
              )}
              aria-hidden="true"
            >
              <AlertTriangle className="h-5 w-5" />
            </span>
            <div className="min-w-0 space-y-1">
              <DialogPrimitive.Title className="font-heading text-lg font-semibold">{title}</DialogPrimitive.Title>
              <DialogPrimitive.Description className="text-sm text-muted-foreground">{description ?? ''}</DialogPrimitive.Description>
            </div>
          </div>
          <div className="mt-6 flex justify-end gap-2">
            <Button type="button" variant="secondary" data-cancel onClick={() => onOpenChange(false)} disabled={busy}>
              {cancelLabel ?? t('common.cancel')}
            </Button>
            <Button type="button" variant={tone === 'danger' ? 'destructive' : 'default'} onClick={onConfirm} disabled={busy}>
              {busy ? (busyLabel ?? t('common.saving')) : confirmLabel}
            </Button>
          </div>
        </DialogPrimitive.Content>
      </DialogPrimitive.Portal>
    </DialogPrimitive.Root>
  )
}

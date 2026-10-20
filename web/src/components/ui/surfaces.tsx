import * as DialogPrimitive from '@radix-ui/react-dialog'
import { X } from 'lucide-react'
import * as React from 'react'
import { cn } from '@/lib/utils'

export function Card({ className, ...props }: React.HTMLAttributes<HTMLDivElement>) {
  return <div className={cn('nursery-card', className)} {...props} />
}

const tones = {
  neutral: 'border-slate-200 bg-slate-100 text-slate-600',
  success: 'border-emerald-200 bg-emerald-50 text-emerald-800',
  warning: 'border-amber-300 bg-amber-50 text-amber-800',
  danger: 'border-red-200 bg-red-50 text-red-700',
  info: 'border-blue-200 bg-blue-50 text-blue-800',
  purple: 'border-violet-200 bg-violet-50 text-violet-800',
} as const

export type Tone = keyof typeof tones

export function Badge({ tone = 'neutral', className, ...props }: React.HTMLAttributes<HTMLSpanElement> & { tone?: Tone }) {
  return <span className={cn('pill', tones[tone], className)} {...props} />
}

export function Skeleton({ className, ...props }: React.HTMLAttributes<HTMLDivElement>) {
  return <div className={cn('animate-pulse rounded-md bg-slate-200/70', className)} {...props} />
}

export function Avatar({ text, className }: { text: string; className?: string }) {
  return (
    <span
      aria-hidden="true"
      className={cn(
        'inline-flex h-9 w-9 shrink-0 items-center justify-center rounded-full bg-gradient-to-br from-primary to-amber font-heading text-xs font-semibold text-white',
        className,
      )}
    >
      {text}
    </span>
  )
}

export function Alert({
  tone = 'danger',
  children,
  className,
}: {
  tone?: 'danger' | 'info' | 'success'
  children: React.ReactNode
  className?: string
}) {
  const styles = {
    danger: 'border-red-200 bg-red-50 text-red-800',
    info: 'border-blue-200 bg-blue-50 text-blue-800',
    success: 'border-emerald-200 bg-emerald-50 text-emerald-800',
  }
  return (
    <div role={tone === 'danger' ? 'alert' : 'status'} className={cn('rounded-md border px-3 py-2 text-sm', styles[tone], className)}>
      {children}
    </div>
  )
}

export const Dialog = DialogPrimitive.Root
export const DialogTrigger = DialogPrimitive.Trigger
export const DialogClose = DialogPrimitive.Close

export function DialogContent({
  className,
  children,
  closeLabel,
  ...props
}: React.ComponentPropsWithoutRef<typeof DialogPrimitive.Content> & { closeLabel: string }) {
  return (
    <DialogPrimitive.Portal>
      <DialogPrimitive.Overlay className="fixed inset-0 z-50 bg-slate-900/40 backdrop-blur-[1px] data-[state=open]:animate-in data-[state=closed]:animate-out data-[state=closed]:fade-out-0 data-[state=open]:fade-in-0" />
      <DialogPrimitive.Content
        className={cn(
          'fixed start-1/2 top-1/2 z-50 max-h-[90vh] w-[calc(100%-2rem)] max-w-2xl -translate-y-1/2 overflow-y-auto rounded-lg border bg-card p-6 shadow-xl ltr:-translate-x-1/2 rtl:translate-x-1/2 data-[state=open]:animate-in data-[state=closed]:animate-out data-[state=closed]:fade-out-0 data-[state=open]:fade-in-0',
          className,
        )}
        {...props}
      >
        {children}
        <DialogPrimitive.Close
          aria-label={closeLabel}
          className="absolute end-4 top-4 rounded-md p-1 text-muted-foreground hover:bg-secondary hover:text-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
        >
          <X className="h-4 w-4" />
        </DialogPrimitive.Close>
      </DialogPrimitive.Content>
    </DialogPrimitive.Portal>
  )
}

export function DialogHeader({ title, description }: { title: string; description?: string }) {
  return (
    <div className="mb-5 space-y-1 pe-8">
      <DialogPrimitive.Title className="font-heading text-xl font-semibold">{title}</DialogPrimitive.Title>
      <DialogPrimitive.Description className="text-sm text-muted-foreground">
        {description ?? ''}
      </DialogPrimitive.Description>
    </div>
  )
}

export function Table({ className, ...props }: React.TableHTMLAttributes<HTMLTableElement>) {
  return (
    // contain-paint stops a wide table from making the whole phone screen wider; the table scrolls inside this box instead.
    <div className="w-full overflow-x-auto [contain:paint]">
      <table className={cn('w-full text-sm', className)} {...props} />
    </div>
  )
}

export function Th({ className, ...props }: React.ThHTMLAttributes<HTMLTableCellElement>) {
  return (
    <th
      scope="col"
      className={cn('whitespace-nowrap border-b px-4 py-3 text-start text-[11px] font-bold uppercase tracking-wide text-muted-foreground', className)}
      {...props}
    />
  )
}

export function Td({ className, ...props }: React.TdHTMLAttributes<HTMLTableCellElement>) {
  return <td className={cn('border-b border-slate-100 px-4 py-3 align-middle', className)} {...props} />
}

export function EmptyState({ icon, title, description, action }: {
  icon: React.ReactNode
  title: string
  description?: string
  action?: React.ReactNode
}) {
  return (
    <div className="flex flex-col items-center gap-3 px-6 py-14 text-center">
      <div className="flex h-14 w-14 items-center justify-center rounded-2xl bg-amber/30 text-amber-800">{icon}</div>
      <h3 className="font-heading text-lg font-semibold">{title}</h3>
      {description && <p className="max-w-sm text-sm text-muted-foreground">{description}</p>}
      {action}
    </div>
  )
}

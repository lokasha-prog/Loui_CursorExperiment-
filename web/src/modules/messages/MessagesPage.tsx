import { zodResolver } from '@hookform/resolvers/zod'
import { MessageSquare, Plus, Send } from 'lucide-react'
import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { useTranslation } from 'react-i18next'
import { z } from 'zod'
import { ApiError } from '@/api/client'
import { flatten, useCreateThread, useReply, useRoomOptions, useStudentOptions, useThread, useThreads } from '@/api/hooks'
import { PageHeader } from '@/components/layout/PageHeader'
import { ListError, ListSkeleton, LoadMore } from '@/components/shared/ListState'
import { Button } from '@/components/ui/button'
import { Field, Input, Select, Textarea } from '@/components/ui/form-controls'
import { Alert, Badge, Card, Dialog, DialogContent, DialogHeader, EmptyState } from '@/components/ui/surfaces'
import { toast } from '@/components/ui/toaster'
import { useApiErrorMessage } from '@/hooks/use-api-error'
import { cn, localized } from '@/lib/utils'
import { can, isParentOnly, useAuth } from '@/stores/auth'

const schema = z.object({
  subject: z.string().trim().min(1, 'validation.required'),
  body: z.string().trim().min(1, 'validation.required'),
  studentId: z.string().optional(),
  broadcast: z.boolean().optional(),
  roomId: z.string().optional(),
})

function NewMessageDialog({ open, onOpenChange, onCreated }: { open: boolean; onOpenChange: (open: boolean) => void; onCreated: (id: string) => void }) {
  const { t, i18n } = useTranslation()
  const me = useAuth((s) => s.me)
  const family = isParentOnly(me)
  const canBroadcast = can(me, 'messaging.broadcast')
  const students = useStudentOptions(open, '')
  const rooms = useRoomOptions(open && canBroadcast)
  const create = useCreateThread()
  const [failure, setFailure] = useState<string | null>(null)
  const message = useApiErrorMessage()
  const {
    register,
    handleSubmit,
    reset,
    watch,
    formState: { errors, isSubmitting },
  } = useForm<z.input<typeof schema>>({ resolver: zodResolver(schema) })
  const broadcast = !!watch('broadcast')
  const err = (key?: string) => (key ? t(key) : undefined)

  const submit = handleSubmit(async (values) => {
    setFailure(null)
    if (!broadcast && !values.studentId) return setFailure(t('messages.form.child'))
    try {
      const thread = await create.mutateAsync({
        subject: values.subject.trim(),
        body: values.body.trim(),
        student_id: broadcast ? null : values.studentId,
        broadcast,
        room_id: broadcast && values.roomId ? values.roomId : null,
      })
      toast.success(t('messages.sent'))
      reset()
      onOpenChange(false)
      onCreated(thread.id)
    } catch (error) {
      setFailure(error instanceof ApiError && error.status === 422 ? t('messages.errors.noFamily') : message(error))
    }
  })

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent closeLabel={t('common.close')} className="max-w-lg">
        <DialogHeader title={t('messages.form.title')} />
        <form onSubmit={(e) => { e.preventDefault(); void submit() }} noValidate className="space-y-4">
          {failure && <Alert>{failure}</Alert>}
          {!family && canBroadcast && (
            <label className="flex items-center gap-2 text-sm font-semibold">
              <input type="checkbox" className="h-4 w-4 accent-primary" {...register('broadcast')} />
              {t('messages.form.broadcast')}
            </label>
          )}
          {broadcast ? (
            <Field label={t('messages.form.room')} htmlFor="nm-room">
              <Select id="nm-room" {...register('roomId')}>
                <option value="">{t('messages.broadcast')}</option>
                {(rooms.data ?? []).map((r) => <option key={r.id} value={r.id}>{localized(r.name, i18n.language)}</option>)}
              </Select>
            </Field>
          ) : (
            <Field label={t('messages.form.child')} htmlFor="nm-child" required>
              <Select id="nm-child" {...register('studentId')}>
                <option value="">—</option>
                {(students.data ?? []).map((s) => <option key={s.id} value={s.id}>{s.first_name} {s.last_name}</option>)}
              </Select>
            </Field>
          )}
          <Field label={t('messages.form.subject')} htmlFor="nm-subject" required error={err(errors.subject?.message)}>
            <Input id="nm-subject" maxLength={200} aria-invalid={!!errors.subject} {...register('subject')} />
          </Field>
          <Field label={t('messages.form.body')} htmlFor="nm-body" required error={err(errors.body?.message)}>
            <Textarea id="nm-body" rows={5} maxLength={4000} aria-invalid={!!errors.body} {...register('body')} />
          </Field>
          <div className="flex justify-end gap-2 border-t pt-4">
            <Button type="button" variant="secondary" onClick={() => onOpenChange(false)}>{t('common.cancel')}</Button>
            <Button type="submit" disabled={isSubmitting}>{t('messages.form.send')}</Button>
          </div>
        </form>
      </DialogContent>
    </Dialog>
  )
}

function Conversation({ id }: { id: string }) {
  const { t, i18n } = useTranslation()
  const thread = useThread(id)
  const reply = useReply(id)
  const message = useApiErrorMessage()
  const [text, setText] = useState('')
  const time = (v: string) => new Intl.DateTimeFormat(i18n.language === 'ar' ? 'ar-AE-u-nu-latn' : 'en-GB', { dateStyle: 'medium', timeStyle: 'short' }).format(new Date(v))

  if (thread.isLoading) return <ListSkeleton rows={3} />
  if (thread.isError) return <ListError error={thread.error} onRetry={() => void thread.refetch()} />
  const detail = thread.data!

  const send = async () => {
    const body = text.trim()
    if (!body) return
    try {
      await reply.mutateAsync(body)
      setText('')
    } catch (error) {
      toast.error(message(error))
    }
  }

  return (
    <div className="flex h-full flex-col">
      <h2 className="mb-3 font-heading text-lg font-semibold">{detail.subject}</h2>
      <ul className="mb-4 flex-1 space-y-3 overflow-y-auto" aria-live="polite">
        {detail.messages.map((m) => (
          <li key={m.id} className={cn('rounded-md border p-3 text-sm', m.from_staff ? 'bg-accent/50' : 'bg-white')}>
            <p className="mb-1 flex items-center gap-2 text-xs font-bold text-muted-foreground">
              {m.sender_name}
              {m.from_staff && <Badge tone="info">{t('messages.staff')}</Badge>}
              <span className="ms-auto font-normal">{time(m.created_at)}</span>
            </p>
            <p className="whitespace-pre-line">{m.body}</p>
          </li>
        ))}
      </ul>
      {detail.can_reply ? (
        <form onSubmit={(e) => { e.preventDefault(); void send() }} className="flex gap-2">
          <Textarea value={text} onChange={(e) => setText(e.target.value)} rows={2} maxLength={4000} aria-label={t('messages.reply')} placeholder={t('messages.reply')} />
          <Button type="submit" size="lg" disabled={!text.trim() || reply.isPending}><Send className="h-4 w-4 rtl:-scale-x-100" aria-hidden="true" />{t('messages.send')}</Button>
        </form>
      ) : (
        <Alert tone="info">{t('messages.broadcastNoReply')}</Alert>
      )}
    </div>
  )
}

export default function MessagesPage() {
  const { t, i18n } = useTranslation()
  const me = useAuth((s) => s.me)
  const family = isParentOnly(me)
  const canWrite = can(me, 'messaging.write')
  const [selected, setSelected] = useState<string | null>(null)
  const [open, setOpen] = useState(false)
  const list = useThreads()
  const threads = flatten(list.data)
  const when = (v: string) => new Intl.DateTimeFormat(i18n.language === 'ar' ? 'ar-AE-u-nu-latn' : 'en-GB', { dateStyle: 'medium' }).format(new Date(v))

  return (
    <>
      <PageHeader
        title={t('messages.title')}
        subtitle={family ? t('messages.parentSubtitle') : t('messages.subtitle')}
        action={canWrite && <Button onClick={() => setOpen(true)}><Plus className="h-4 w-4" aria-hidden="true" />{t('messages.new')}</Button>}
      />
      <div className="grid gap-4 lg:grid-cols-[20rem_1fr]">
        <Card className="overflow-hidden">
          {list.isLoading ? <ListSkeleton /> : list.isError ? <ListError error={list.error} onRetry={() => void list.refetch()} /> :
            threads.length === 0 ? <EmptyState icon={<MessageSquare className="h-7 w-7" aria-hidden="true" />} title={t('messages.empty')} /> : (
              <>
                <ul>
                  {threads.map((th) => (
                    <li key={th.id}>
                      <button
                        type="button"
                        onClick={() => setSelected(th.id)}
                        aria-current={selected === th.id}
                        className={cn('block w-full border-b px-4 py-3 text-start hover:bg-slate-50 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring', selected === th.id && 'bg-accent/60')}
                      >
                        <span className="flex items-center gap-2">
                          <span className={cn('truncate text-sm', th.unread ? 'font-bold' : 'font-semibold')}>{th.subject}</span>
                          {th.unread && <Badge tone="danger">{t('messages.unread')}</Badge>}
                          <span className="ms-auto shrink-0 text-[11px] text-muted-foreground">{when(th.last_message_at)}</span>
                        </span>
                        <span className="mt-0.5 block truncate text-xs text-muted-foreground">{th.last_sender_name}: {th.last_message_preview}</span>
                      </button>
                    </li>
                  ))}
                </ul>
                <LoadMore visible={!!list.hasNextPage} loading={list.isFetchingNextPage} onClick={() => void list.fetchNextPage()} />
              </>
            )}
        </Card>
        <Card className="min-h-[24rem] p-4">
          {selected ? <Conversation id={selected} /> : <EmptyState icon={<MessageSquare className="h-7 w-7" aria-hidden="true" />} title={t('messages.pick')} />}
        </Card>
      </div>
      {canWrite && <NewMessageDialog open={open} onOpenChange={setOpen} onCreated={setSelected} />}
    </>
  )
}

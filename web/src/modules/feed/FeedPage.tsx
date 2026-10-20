import { zodResolver } from '@hookform/resolvers/zod'
import { Check, Images, Plus, X } from 'lucide-react'
import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { useTranslation } from 'react-i18next'
import { z } from 'zod'
import {
  flatten,
  useActivities,
  useCreateActivity,
  useDailyReports,
  useDecideActivity,
  useStudentOptions,
  useUploadMedia,
} from '@/api/hooks'
import type { ActivityPost, ActivityType } from '@/api/types'
import { PageHeader } from '@/components/layout/PageHeader'
import { ListError, ListSkeleton, LoadMore } from '@/components/shared/ListState'
import { ProtectedMedia } from '@/components/shared/ProtectedMedia'
import { Button } from '@/components/ui/button'
import { Field, Input, Select, Textarea } from '@/components/ui/form-controls'
import { Alert, Badge, Card, Dialog, DialogContent, DialogHeader, EmptyState, type Tone } from '@/components/ui/surfaces'
import { toast } from '@/components/ui/toaster'
import { useApiErrorMessage } from '@/hooks/use-api-error'
import { formatDate } from '@/lib/format'
import { localized } from '@/lib/utils'
import { can, isParentOnly, useAuth } from '@/stores/auth'

const TYPES: ActivityType[] = ['diary', 'photo', 'video', 'food', 'toilet', 'mood', 'medical', 'announcement']
const NEEDS_APPROVAL: ActivityType[] = ['announcement', 'medical']
const statusTone: Record<ActivityPost['status'], Tone> = { pending: 'warning', approved: 'success', rejected: 'danger' }

const schema = z.object({
  type: z.enum(['announcement', 'photo', 'video', 'diary', 'food', 'toilet', 'medical', 'mood']),
  childIds: z.array(z.string()),
  text: z.string().optional(),
  textAr: z.string().optional(),
})

function NewPostDialog({ open, onOpenChange, canApprove }: { open: boolean; onOpenChange: (open: boolean) => void; canApprove: boolean }) {
  const { t } = useTranslation()
  const students = useStudentOptions(open)
  const upload = useUploadMedia()
  const create = useCreateActivity()
  const message = useApiErrorMessage()
  const [files, setFiles] = useState<File[]>([])
  const [failure, setFailure] = useState<string | null>(null)
  const {
    register,
    handleSubmit,
    reset,
    watch,
    formState: { isSubmitting },
  } = useForm<z.input<typeof schema>>({ resolver: zodResolver(schema), defaultValues: { type: 'diary', childIds: [] } })
  const type = watch('type')
  const needsFile = type === 'photo' || type === 'video'

  const submit = handleSubmit(async (values) => {
    setFailure(null)
    const text = values.text?.trim()
    const textAr = values.textAr?.trim()
    if (!text && !textAr && files.length === 0) return setFailure(t('validation.required'))
    if (needsFile && files.length === 0) return setFailure(t('feed.form.filesHint'))
    try {
      const media = await Promise.all(files.map((f) => upload.mutateAsync(f)))
      const post = await create.mutateAsync({
        type: values.type,
        caption: text || textAr ? { ...(text ? { en: text } : {}), ...(textAr ? { ar: textAr } : {}) } : null,
        student_ids: values.childIds ?? [],
        media_ids: media.map((m) => m.id),
      })
      toast.success(post.status === 'pending' ? t('feed.createdPending') : t('feed.created'))
      reset({ type: 'diary', childIds: [] })
      setFiles([])
      onOpenChange(false)
    } catch (error) {
      setFailure(message(error))
    }
  })

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent closeLabel={t('common.close')} className="max-w-lg">
        <DialogHeader title={t('feed.form.title')} description={t('feed.form.description')} />
        <form onSubmit={(e) => { e.preventDefault(); void submit() }} noValidate className="space-y-4">
          {failure && <Alert>{failure}</Alert>}
          <Field label={t('feed.form.type')} htmlFor="fp-type">
            <Select id="fp-type" {...register('type')}>
              {TYPES.map((x) => <option key={x} value={x}>{t(`feed.type.${x}`)}</option>)}
            </Select>
          </Field>
          {!canApprove && NEEDS_APPROVAL.includes(type as ActivityType) && <Alert tone="info">{t('feed.form.needApproval')}</Alert>}
          <Field label={t('feed.form.children')} htmlFor="fp-children" hint={t('feed.form.childrenHint')}>
            <Select id="fp-children" multiple size={5} {...register('childIds')}>
              {(students.data ?? []).map((s) => <option key={s.id} value={s.id}>{s.first_name} {s.last_name}</option>)}
            </Select>
          </Field>
          <Field label={t('feed.form.text')} htmlFor="fp-text">
            <Textarea id="fp-text" rows={3} maxLength={2000} {...register('text')} />
          </Field>
          <Field label={t('feed.form.textAr')} htmlFor="fp-text-ar">
            <Textarea id="fp-text-ar" rows={3} dir="rtl" lang="ar" maxLength={2000} {...register('textAr')} />
          </Field>
          <Field label={t('feed.form.files')} htmlFor="fp-files" hint={t('feed.form.filesHint')}>
            <Input id="fp-files" type="file" multiple accept="image/jpeg,image/png,image/gif,image/webp,video/mp4" onChange={(e) => setFiles(Array.from(e.target.files ?? []))} />
          </Field>
          <div className="flex justify-end gap-2 border-t pt-4">
            <Button type="button" variant="secondary" onClick={() => onOpenChange(false)}>{t('common.cancel')}</Button>
            <Button type="submit" disabled={isSubmitting}>{isSubmitting ? t('common.saving') : t('feed.form.submit')}</Button>
          </div>
        </form>
      </DialogContent>
    </Dialog>
  )
}

function PostCard({ post, names, canApprove }: { post: ActivityPost; names: Map<string, string>; canApprove: boolean }) {
  const { t, i18n } = useTranslation()
  const decide = useDecideActivity()
  const message = useApiErrorMessage()
  const text = localized(post.caption, i18n.language)
  const children = post.student_ids.map((id) => names.get(id)).filter(Boolean).join(i18n.language === 'ar' ? '، ' : ', ')

  const act = async (action: 'approve' | 'reject') => {
    try {
      await decide.mutateAsync({ id: post.id, action, reason: action === 'reject' ? (window.prompt(t('feed.rejectReason')) ?? undefined) : undefined })
      toast.success(t(action === 'approve' ? 'feed.approved' : 'feed.rejected'))
    } catch (error) {
      toast.error(message(error))
    }
  }

  return (
    <Card className="p-4">
      <div className="mb-2 flex flex-wrap items-center gap-2">
        <Badge tone="info">{t(`feed.type.${post.type}`)}</Badge>
        {post.status !== 'approved' && <Badge tone={statusTone[post.status]}>{t(`feed.status.${post.status}`)}</Badge>}
        <span className="ms-auto text-xs text-muted-foreground">{formatDate(post.created_at, i18n.language)}</span>
      </div>
      {post.media.length > 0 && (
        <div className="mb-3 grid gap-2 sm:grid-cols-2">
          {post.media.map((m) => <ProtectedMedia key={m.id} id={m.id} contentType={m.content_type} alt={text || t(`feed.type.${post.type}`)} />)}
        </div>
      )}
      {text && <p className="whitespace-pre-line text-sm">{text}</p>}
      {children && <p className="mt-2 text-xs font-semibold text-primary-dark">{children}</p>}
      {post.posted_by_name && <p className="mt-1 text-xs text-muted-foreground">{t('feed.by', { name: post.posted_by_name })}</p>}
      {canApprove && post.status === 'pending' && (
        <div className="mt-3 flex gap-2 border-t pt-3">
          <Button size="sm" onClick={() => void act('approve')}><Check className="h-3.5 w-3.5" aria-hidden="true" />{t('feed.approve')}</Button>
          <Button size="sm" variant="secondary" onClick={() => void act('reject')}><X className="h-3.5 w-3.5" aria-hidden="true" />{t('feed.reject')}</Button>
        </div>
      )}
    </Card>
  )
}

export default function FeedPage() {
  const { t, i18n } = useTranslation()
  const me = useAuth((s) => s.me)
  const parent = isParentOnly(me)
  const canWrite = can(me, 'activities.write')
  const canApprove = can(me, 'activities.approve')
  const [waiting, setWaiting] = useState(false)
  const [open, setOpen] = useState(false)
  const list = useActivities({ status: waiting ? 'pending' : undefined })
  const reports = useDailyReports()
  const students = useStudentOptions(can(me, 'students.read'), '')
  const names = new Map((students.data ?? []).map((s) => [s.id, `${s.first_name} ${s.last_name}`]))
  const posts = flatten(list.data)
  const dailyReports = flatten(reports.data)

  return (
    <>
      <PageHeader
        title={t('feed.title')}
        subtitle={parent ? t('feed.parentSubtitle') : t('feed.subtitle')}
        action={canWrite && <Button onClick={() => setOpen(true)}><Plus className="h-4 w-4" aria-hidden="true" />{t('feed.new')}</Button>}
      />

      {canApprove && (
        <div className="mb-4 flex gap-2" role="group" aria-label={t('feed.waiting')}>
          <Button size="sm" variant={!waiting ? 'default' : 'secondary'} aria-pressed={!waiting} onClick={() => setWaiting(false)}>{t('feed.all')}</Button>
          <Button size="sm" variant={waiting ? 'default' : 'secondary'} aria-pressed={waiting} onClick={() => setWaiting(true)}>{t('feed.waiting')}</Button>
        </div>
      )}

      <div className="grid gap-6 lg:grid-cols-[1fr_20rem]">
        <section className="space-y-4">
          {list.isLoading ? <ListSkeleton /> : list.isError ? <ListError error={list.error} onRetry={() => void list.refetch()} /> :
            posts.length === 0 ? <EmptyState icon={<Images className="h-7 w-7" aria-hidden="true" />} title={t('feed.empty')} description={t('feed.emptyBody')} /> : (
              <>
                {posts.map((p) => <PostCard key={p.id} post={p} names={names} canApprove={canApprove} />)}
                <LoadMore visible={!!list.hasNextPage} loading={list.isFetchingNextPage} onClick={() => void list.fetchNextPage()} />
              </>
            )}
        </section>
        <aside aria-labelledby="reports-h">
          <h2 id="reports-h" className="mb-3 font-heading text-lg font-semibold">{t('feed.reportsTitle')}</h2>
          {dailyReports.length === 0 ? <p className="text-sm text-muted-foreground">{t('feed.reportsEmpty')}</p> : (
            <ul className="space-y-3">
              {dailyReports.slice(0, 5).map((r) => (
                <li key={r.id}>
                  <Card className="p-3 text-sm">
                    <p className="text-xs font-bold text-muted-foreground">{formatDate(r.date, i18n.language)}</p>
                    <p className="mt-1">{localized(r.summary, i18n.language)}</p>
                  </Card>
                </li>
              ))}
            </ul>
          )}
        </aside>
      </div>
      {canWrite && <NewPostDialog open={open} onOpenChange={setOpen} canApprove={canApprove} />}
    </>
  )
}

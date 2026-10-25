import { Images, Play, Trash2 } from 'lucide-react'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import {
  flatten,
  useCreateActivity,
  useDeleteActivity,
  useGroupCentres,
  useGroups,
  useRoomOptions,
  useShareWithCentres,
  useStudentOptions,
  useUploadMedia,
} from '@/api/hooks'
import type { Page } from '@/api/client'
import type { ActivityPost } from '@/api/types'
import { ConfirmDialog } from '@/components/shared/ConfirmDialog'
import { ListError, LoadMore } from '@/components/shared/ListState'
import { ProtectedMedia } from '@/components/shared/ProtectedMedia'
import { Button } from '@/components/ui/button'
import { Field, Input, Select, Textarea } from '@/components/ui/form-controls'
import { Alert, Badge, Dialog, DialogContent, DialogHeader, EmptyState, Skeleton } from '@/components/ui/surfaces'
import { toast } from '@/components/ui/toaster'
import { useApiErrorMessage } from '@/hooks/use-api-error'
import { formatDate } from '@/lib/format'
import { localized } from '@/lib/utils'
import { can, isParentOnly, useAuth } from '@/stores/auth'

const ACCEPT = 'image/jpeg,image/png,image/gif,image/webp,video/mp4'

/** One photo or video in the grid, with the post it came from. */
export interface GalleryItem {
  post: ActivityPost
  mediaId: string
  contentType: string
}

/** A post naming no child is shared with everyone in the centre (or in its room). */
const isShared = (post: ActivityPost) => post.student_ids.length === 0

/** Who a gallery upload is for. A child page always uploads for that child; administrators choose on the gallery page. */
type Audience = 'centre' | 'room' | 'group' | 'children' | 'branches' | 'all_branches'

/**
 * Adds photos or videos as one photo post: about one child when a child is given, otherwise for the audience an
 * administrator picks (this centre, a room, a group, some children, or other centres of the group).
 * Families see it straight away, like the daily feed.
 */
export function UploadDialog({ studentId, open, onOpenChange }: { studentId?: string; open: boolean; onOpenChange: (open: boolean) => void }) {
  const { t, i18n } = useTranslation()
  const me = useAuth((s) => s.me)
  const choosing = !studentId && open
  const groupManager = can(me, 'group.manage')
  const upload = useUploadMedia()
  const create = useCreateActivity()
  const share = useShareWithCentres()
  const rooms = useRoomOptions(choosing)
  const groups = useGroups(choosing)
  const children = useStudentOptions(choosing)
  const centres = useGroupCentres(choosing && groupManager)
  const message = useApiErrorMessage()
  const [files, setFiles] = useState<File[]>([])
  const [text, setText] = useState('')
  const [textAr, setTextAr] = useState('')
  const [audience, setAudience] = useState<Audience>('centre')
  const [target, setTarget] = useState('')
  const [targets, setTargets] = useState<string[]>([])
  const [failure, setFailure] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)

  const close = (next: boolean) => {
    if (!next) {
      setFiles([])
      setText('')
      setTextAr('')
      setAudience('centre')
      setTarget('')
      setTargets([])
      setFailure(null)
    }
    onOpenChange(next)
  }

  const pickAudience = (next: Audience) => {
    setAudience(next)
    setTarget('')
    setTargets([])
  }

  const submit = async () => {
    setFailure(null)
    if (files.length === 0) return setFailure(t('students.gallery.chooseFile'))
    if (((audience === 'room' || audience === 'group') && !target) || ((audience === 'children' || audience === 'branches') && targets.length === 0)) {
      return setFailure(t('gallery.audience.chooseTarget'))
    }
    setBusy(true)
    try {
      const media = await Promise.all(files.map((f) => upload.mutateAsync(f)))
      const text_ = { ...(text.trim() ? { en: text.trim() } : {}), ...(textAr.trim() ? { ar: textAr.trim() } : {}) }
      const caption = Object.keys(text_).length > 0 ? text_ : null
      const mediaIds = media.map((m) => m.id)
      if (audience === 'branches' || audience === 'all_branches') {
        await share.mutateAsync({ caption, media_ids: mediaIds, centre_ids: audience === 'branches' ? targets : null })
      } else {
        await create.mutateAsync({
          type: media.every((m) => m.content_type.startsWith('video/')) ? 'video' : 'photo',
          caption,
          student_ids: studentId ? [studentId] : audience === 'children' ? targets : [],
          room_id: audience === 'room' ? target : null,
          group_id: audience === 'group' ? target : null,
          media_ids: mediaIds,
        })
      }
      toast.success(t('students.gallery.uploaded'))
      close(false)
    } catch (error) {
      setFailure(message(error))
    } finally {
      setBusy(false)
    }
  }

  return (
    <Dialog open={open} onOpenChange={close}>
      <DialogContent closeLabel={t('common.close')} className="max-w-lg">
        <DialogHeader
          title={t(studentId ? 'students.gallery.uploadTitle' : 'gallery.uploadTitle')}
          description={t(studentId ? 'students.gallery.uploadDescription' : 'gallery.uploadDescription')}
        />
        <form onSubmit={(e) => { e.preventDefault(); void submit() }} noValidate className="space-y-4">
          {failure && <Alert>{failure}</Alert>}
          {!studentId && (
            <>
              <Field label={t('gallery.audience.label')} htmlFor="sg-audience">
                <Select id="sg-audience" value={audience} onChange={(e) => pickAudience(e.target.value as Audience)}>
                  <option value="centre">{t('gallery.audience.centre')}</option>
                  <option value="room">{t('gallery.audience.room')}</option>
                  <option value="group">{t('gallery.audience.group')}</option>
                  <option value="children">{t('gallery.audience.children')}</option>
                  {groupManager && <option value="branches">{t('gallery.audience.branches')}</option>}
                  {groupManager && <option value="all_branches">{t('gallery.audience.allBranches')}</option>}
                </Select>
              </Field>
              {audience === 'room' && (
                <Field label={t('gallery.audience.whichRoom')} htmlFor="sg-room">
                  <Select id="sg-room" value={target} onChange={(e) => setTarget(e.target.value)}>
                    <option value="">{t('gallery.audience.pick')}</option>
                    {(rooms.data ?? []).map((r) => <option key={r.id} value={r.id}>{localized(r.name, i18n.language)}</option>)}
                  </Select>
                </Field>
              )}
              {audience === 'group' && (
                <Field label={t('gallery.audience.whichGroup')} htmlFor="sg-group" hint={t('gallery.audience.groupHint')}>
                  <Select id="sg-group" value={target} onChange={(e) => setTarget(e.target.value)}>
                    <option value="">{t('gallery.audience.pick')}</option>
                    {flatten(groups.data).map((g) => <option key={g.id} value={g.id}>{localized(g.name, i18n.language)}</option>)}
                  </Select>
                </Field>
              )}
              {audience === 'children' && (
                <Field label={t('gallery.audience.whichChildren')} htmlFor="sg-children">
                  <Select id="sg-children" multiple size={6} value={targets} onChange={(e) => setTargets(Array.from(e.target.selectedOptions, (o) => o.value))}>
                    {(children.data ?? []).map((c) => <option key={c.id} value={c.id}>{c.first_name} {c.last_name}</option>)}
                  </Select>
                </Field>
              )}
              {audience === 'branches' && (
                <Field label={t('gallery.audience.whichBranches')} htmlFor="sg-branches" hint={t('gallery.audience.branchesHint')}>
                  <Select id="sg-branches" multiple size={5} value={targets} onChange={(e) => setTargets(Array.from(e.target.selectedOptions, (o) => o.value))}>
                    {(centres.data ?? []).map((c) => <option key={c.id} value={c.id}>{localized(c.name, i18n.language)}</option>)}
                  </Select>
                </Field>
              )}
              {audience === 'all_branches' && <Alert tone="info">{t('gallery.audience.allBranchesNote')}</Alert>}
            </>
          )}
          <Field label={t('students.gallery.files')} htmlFor="sg-files" hint={t('students.gallery.filesHint')}>
            <Input id="sg-files" type="file" multiple accept={ACCEPT} onChange={(e) => setFiles(Array.from(e.target.files ?? []))} />
          </Field>
          <Field label={t('students.gallery.caption')} htmlFor="sg-text">
            <Textarea id="sg-text" rows={2} maxLength={2000} value={text} onChange={(e) => setText(e.target.value)} />
          </Field>
          <Field label={t('students.gallery.captionAr')} htmlFor="sg-text-ar">
            <Textarea id="sg-text-ar" rows={2} dir="rtl" lang="ar" maxLength={2000} value={textAr} onChange={(e) => setTextAr(e.target.value)} />
          </Field>
          <div className="flex justify-end gap-2 border-t pt-4">
            <Button type="button" variant="secondary" onClick={() => close(false)}>{t('common.cancel')}</Button>
            <Button type="submit" disabled={busy}>{busy ? t('students.gallery.uploading') : t('students.gallery.submit')}</Button>
          </div>
        </form>
      </DialogContent>
    </Dialog>
  )
}

function Viewer({ item, onClose }: { item: GalleryItem | null; onClose: () => void }) {
  const { t, i18n } = useTranslation()
  const me = useAuth((s) => s.me)
  const remove = useDeleteActivity()
  const message = useApiErrorMessage()
  const text = item ? localized(item.post.caption, i18n.language) : ''
  const canRemove = !!item && !isParentOnly(me) && (can(me, 'activities.approve') || item.post.posted_by_user_id === me?.id)
  const [confirming, setConfirming] = useState(false)
  const shared = !!item && isShared(item.post)

  const takeDown = async () => {
    if (!item) return
    try {
      await remove.mutateAsync(item.post.id)
      toast.success(t('students.gallery.removed'))
      setConfirming(false)
      onClose()
    } catch (error) {
      toast.error(message(error))
    }
  }

  return (
    <Dialog open={!!item} onOpenChange={(open) => !open && onClose()}>
      <DialogContent closeLabel={t('common.close')} className="max-w-3xl">
        {item && (
          <>
            <DialogHeader title={text || t(`feed.type.${item.post.type}`)} description={t('students.gallery.by', { name: item.post.posted_by_name ?? '', date: formatDate(item.post.created_at, i18n.language) })} />
            {/* Actions sit above the photo and the photo is sized to the window, so nothing needs scrolling to reach. */}
            {(isShared(item.post) || canRemove) && (
              <div className="-mt-2 mb-3 flex flex-wrap items-center gap-2">
                {isShared(item.post) && <Badge tone="info">{t('gallery.wholeNursery')}</Badge>}
                {canRemove && (
                  <Button variant="secondary" size="sm" className="ms-auto" onClick={() => setConfirming(true)} disabled={remove.isPending}>
                    <Trash2 className="h-3.5 w-3.5" aria-hidden="true" />
                    {t('students.gallery.remove')}
                  </Button>
                )}
              </div>
            )}
            <ProtectedMedia
              id={item.mediaId}
              contentType={item.contentType}
              alt={text || t(`feed.type.${item.post.type}`)}
              className="max-h-[calc(90vh-12rem)] w-full object-contain"
            />
          </>
        )}
      </DialogContent>
      <ConfirmDialog
        open={confirming}
        onOpenChange={setConfirming}
        title={t(shared ? 'students.gallery.removeSharedTitle' : 'students.gallery.removeTitle')}
        description={t(shared ? 'students.gallery.removeSharedConfirm' : 'students.gallery.removeConfirm')}
        confirmLabel={t('students.gallery.remove')}
        busyLabel={t('students.gallery.removing')}
        busy={remove.isPending}
        onConfirm={() => void takeDown()}
      />
    </Dialog>
  )
}

interface GalleryList {
  data?: { pages: Page<ActivityPost>[] }
  isLoading: boolean
  isError: boolean
  error: unknown
  hasNextPage: boolean
  isFetchingNextPage: boolean
  refetch: () => unknown
  fetchNextPage: () => unknown
}

/** The photo grid with loading, error and empty states, paging, and a viewer that opens on click. */
export function PhotoGrid({ list, emptyDescription, columns = 'grid-cols-2 sm:grid-cols-3' }: { list: GalleryList; emptyDescription: string; columns?: string }) {
  const { t, i18n } = useTranslation()
  const [viewing, setViewing] = useState<GalleryItem | null>(null)
  const items: GalleryItem[] = flatten(list.data).flatMap((post) => post.media.map((m) => ({ post, mediaId: m.id, contentType: m.content_type })))

  return (
    <>
      {list.isLoading ? (
        <div className={`grid gap-2 ${columns}`}><Skeleton className="h-32" /><Skeleton className="h-32" /><Skeleton className="h-32" /></div>
      ) : list.isError ? (
        <ListError error={list.error} onRetry={() => void list.refetch()} />
      ) : items.length === 0 ? (
        <EmptyState icon={<Images className="h-7 w-7" aria-hidden="true" />} title={t('students.gallery.empty')} description={emptyDescription} />
      ) : (
        <ul className={`grid gap-2 ${columns}`}>
          {items.map((item) => (
            <li key={`${item.post.id}-${item.mediaId}`}>
              <button
                type="button"
                onClick={() => setViewing(item)}
                aria-label={`${t('students.gallery.open')}: ${localized(item.post.caption, i18n.language) || formatDate(item.post.created_at, i18n.language)}`}
                className="block w-full overflow-hidden rounded-md focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary"
              >
                {item.contentType.startsWith('video/') ? (
                  <span className="flex h-32 w-full items-center justify-center rounded-md bg-slate-800 text-white">
                    <Play className="h-8 w-8" aria-hidden="true" />
                  </span>
                ) : (
                  <ProtectedMedia id={item.mediaId} contentType={item.contentType} alt="" className="h-32 w-full object-cover" />
                )}
              </button>
            </li>
          ))}
        </ul>
      )}
      <LoadMore visible={!!list.hasNextPage} loading={list.isFetchingNextPage} onClick={() => void list.fetchNextPage()} />
      <Viewer item={viewing} onClose={() => setViewing(null)} />
    </>
  )
}

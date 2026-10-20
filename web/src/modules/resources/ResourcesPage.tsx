import { Download, FileText, FolderPlus, History, Search, Trash2, Upload } from 'lucide-react'
import { useRef, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { api } from '@/api/client'
import { flatten, useDeleteDocument, useDeleteFolder, useResourceDocuments, useResourceDownloads, useResourceFolders, useResourceVersions, useSaveFolder, useUploadDocument, useUploadVersion } from '@/api/hooks'
import type { ResourceDocument } from '@/api/types'
import { PageHeader } from '@/components/layout/PageHeader'
import { ListError, ListSkeleton, LoadMore } from '@/components/shared/ListState'
import { Button } from '@/components/ui/button'
import { Field, Input, Select, Textarea } from '@/components/ui/form-controls'
import { Alert, Badge, Card, Dialog, DialogContent, DialogHeader, EmptyState, Skeleton } from '@/components/ui/surfaces'
import { toast } from '@/components/ui/toaster'
import { useApiErrorMessage } from '@/hooks/use-api-error'
import { formatDate } from '@/lib/format'
import { cn, localized } from '@/lib/utils'
import { can, useAuth } from '@/stores/auth'

const size = (bytes: number) => (bytes >= 1_048_576 ? `${(bytes / 1_048_576).toFixed(1)} MB` : `${Math.max(1, Math.round(bytes / 1024))} KB`)

function FolderDialog({ onClose }: { onClose: () => void }) {
  const { t } = useTranslation()
  const folders = useResourceFolders()
  const save = useSaveFolder()
  const message = useApiErrorMessage()
  const [en, setEn] = useState('')
  const [ar, setAr] = useState('')
  const [audience, setAudience] = useState('parents')
  const [parent, setParent] = useState('')
  const [failure, setFailure] = useState<string | null>(null)
  const submit = async () => {
    if (!en.trim()) return setFailure(t('validation.required'))
    setFailure(null)
    try {
      await save.mutateAsync({ name: ar.trim() ? { en: en.trim(), ar: ar.trim() } : { en: en.trim() }, audience, parent_folder_id: parent || null })
      toast.success(t('resources.folderSaved'))
      onClose()
    } catch (error) {
      setFailure(message(error))
    }
  }
  return (
    <Dialog open onOpenChange={(o) => !o && onClose()}>
      <DialogContent closeLabel={t('common.close')} className="max-w-lg">
        <DialogHeader title={t('resources.newFolder')} />
        <form onSubmit={(e) => { e.preventDefault(); void submit() }} className="space-y-4" noValidate>
          {failure && <Alert>{failure}</Alert>}
          <div className="grid gap-3 sm:grid-cols-2">
            <Field label={t('resources.nameEn')} htmlFor="rf-en" required><Input id="rf-en" value={en} maxLength={100} onChange={(e) => setEn(e.target.value)} /></Field>
            <Field label={t('resources.nameAr')} htmlFor="rf-ar"><Input id="rf-ar" dir="rtl" lang="ar" value={ar} maxLength={100} onChange={(e) => setAr(e.target.value)} /></Field>
            <Field label={t('resources.audience')} htmlFor="rf-aud"><Select id="rf-aud" value={audience} onChange={(e) => setAudience(e.target.value)}><option value="parents">{t('resources.audiences.parents')}</option><option value="staff">{t('resources.audiences.staff')}</option></Select></Field>
            <Field label={t('resources.inside')} htmlFor="rf-parent"><Select id="rf-parent" value={parent} onChange={(e) => setParent(e.target.value)}><option value="">—</option>{(folders.data ?? []).map((f) => <option key={f.id} value={f.id}>{localized(f.name, 'en')}</option>)}</Select></Field>
          </div>
          <div className="flex justify-end gap-2 border-t pt-4"><Button type="button" variant="secondary" onClick={onClose}>{t('common.cancel')}</Button><Button type="submit" disabled={save.isPending}>{t('resources.saveFolder')}</Button></div>
        </form>
      </DialogContent>
    </Dialog>
  )
}

function UploadDialog({ folderId, onClose }: { folderId: string; onClose: () => void }) {
  const { t, i18n } = useTranslation()
  const folders = useResourceFolders()
  const upload = useUploadDocument()
  const message = useApiErrorMessage()
  const [folder, setFolder] = useState(folderId)
  const [title, setTitle] = useState('')
  const [description, setDescription] = useState('')
  const [tags, setTags] = useState('')
  const [file, setFile] = useState<File | null>(null)
  const [failure, setFailure] = useState<string | null>(null)
  const submit = async () => {
    if (!folder || !title.trim() || !file) return setFailure(t('validation.required'))
    setFailure(null)
    try {
      await upload.mutateAsync({ file, folder_id: folder, title: title.trim(), description: description.trim(), tags: tags.trim() })
      toast.success(t('resources.uploaded'))
      onClose()
    } catch (error) {
      setFailure(message(error))
    }
  }
  return (
    <Dialog open onOpenChange={(o) => !o && onClose()}>
      <DialogContent closeLabel={t('common.close')} className="max-w-lg">
        <DialogHeader title={t('resources.upload')} />
        <form onSubmit={(e) => { e.preventDefault(); void submit() }} className="space-y-4" noValidate>
          {failure && <Alert>{failure}</Alert>}
          <Field label={t('resources.folder')} htmlFor="rd-folder" required><Select id="rd-folder" value={folder} onChange={(e) => setFolder(e.target.value)}><option value="">—</option>{(folders.data ?? []).map((f) => <option key={f.id} value={f.id}>{localized(f.name, i18n.language)}</option>)}</Select></Field>
          <Field label={t('resources.docTitle')} htmlFor="rd-title" required><Input id="rd-title" value={title} maxLength={200} onChange={(e) => setTitle(e.target.value)} /></Field>
          <Field label={t('resources.description')} htmlFor="rd-desc"><Textarea id="rd-desc" rows={2} maxLength={1000} value={description} onChange={(e) => setDescription(e.target.value)} /></Field>
          <Field label={t('resources.tags')} htmlFor="rd-tags" hint={t('resources.tagsHint')}><Input id="rd-tags" value={tags} maxLength={300} onChange={(e) => setTags(e.target.value)} /></Field>
          <Field label={t('resources.file')} htmlFor="rd-file" required hint={t('resources.fileHint')}><input id="rd-file" type="file" accept=".pdf,.docx,.xlsx,.jpg,.jpeg,.png,.gif,.webp,.mp4" onChange={(e) => setFile(e.target.files?.[0] ?? null)} /></Field>
          <div className="flex justify-end gap-2 border-t pt-4"><Button type="button" variant="secondary" onClick={onClose}>{t('common.cancel')}</Button><Button type="submit" disabled={upload.isPending}>{t('resources.upload')}</Button></div>
        </form>
      </DialogContent>
    </Dialog>
  )
}

function History_({ doc, onClose }: { doc: ResourceDocument; onClose: () => void }) {
  const { t, i18n } = useTranslation()
  const versions = useResourceVersions(doc.id)
  const downloads = useResourceDownloads(doc.id)
  const save = async (version: number, name: string) => {
    const blob = await api.blob(`/resources/documents/${doc.id}/download?version=${version}`)
    const url = URL.createObjectURL(blob)
    const a = document.createElement('a')
    a.href = url
    a.download = name
    a.click()
    window.setTimeout(() => URL.revokeObjectURL(url), 30_000)
  }
  return (
    <Dialog open onOpenChange={(o) => !o && onClose()}>
      <DialogContent closeLabel={t('common.close')} className="max-w-xl">
        <DialogHeader title={doc.title} />
        <div className="space-y-5">
          <section aria-labelledby="rh-v"><h3 id="rh-v" className="mb-2 text-sm font-bold">{t('resources.versions')}</h3>
            {!versions.data ? <Skeleton className="h-16" /> : (
              <ul className="divide-y text-sm">
                {versions.data.map((v) => (
                  <li key={v.id} className="flex flex-wrap items-center gap-2 py-2">
                    <Badge tone={v.version === doc.current_version ? 'success' : 'neutral'}>v{v.version}</Badge>
                    <span>{v.file_name}</span><span className="text-xs text-muted-foreground">{size(v.size_bytes)} · {v.uploaded_by} · {formatDate(v.created_at, i18n.language)}</span>
                    <Button size="sm" variant="ghost" className="ms-auto" onClick={() => void save(v.version, v.file_name)}><Download className="h-4 w-4" aria-hidden="true" />{t('resources.download')}</Button>
                  </li>
                ))}
              </ul>
            )}
          </section>
          <section aria-labelledby="rh-d"><h3 id="rh-d" className="mb-2 text-sm font-bold">{t('resources.downloadLog')}</h3>
            {!downloads.data ? <Skeleton className="h-16" /> : downloads.data.length === 0 ? <p className="text-sm text-muted-foreground">{t('resources.noDownloads')}</p> : (
              <ul className="max-h-56 divide-y overflow-y-auto text-sm">
                {downloads.data.map((d, i) => <li key={i} className="flex flex-wrap gap-2 py-1.5"><span className="font-semibold">{d.user}</span><span className="text-muted-foreground">v{d.version} · {new Date(d.at).toLocaleString(i18n.language === 'ar' ? 'ar-AE-u-nu-latn' : 'en-GB')}</span></li>)}
              </ul>
            )}
          </section>
        </div>
      </DialogContent>
    </Dialog>
  )
}

/** The document library: folders for parents or staff, files with versions, and who downloaded what. */
export default function ResourcesPage() {
  const { t, i18n } = useTranslation()
  const me = useAuth((s) => s.me)
  const canWrite = can(me, 'resources.write')
  const message = useApiErrorMessage()
  const folders = useResourceFolders()
  const [folder, setFolder] = useState('')
  const [search, setSearch] = useState('')
  const [newFolder, setNewFolder] = useState(false)
  const [uploading, setUploading] = useState(false)
  const [history, setHistory] = useState<ResourceDocument | null>(null)
  const docs = useResourceDocuments(folder, search)
  const removeDoc = useDeleteDocument()
  const removeFolder = useDeleteFolder()
  const version = useUploadVersion()
  const picker = useRef<HTMLInputElement>(null)
  const [target, setTarget] = useState<string | null>(null)
  const rows = flatten(docs.data)

  const get = async (doc: ResourceDocument) => {
    try {
      const blob = await api.blob(`/resources/documents/${doc.id}/download`)
      const url = URL.createObjectURL(blob)
      const a = document.createElement('a')
      a.href = url
      a.download = doc.file_name
      a.click()
      window.setTimeout(() => URL.revokeObjectURL(url), 30_000)
    } catch (error) {
      toast.error(message(error))
    }
  }
  const run = async (work: () => Promise<unknown>, done?: string) => {
    try {
      await work()
      if (done) toast.success(done)
    } catch (error) {
      toast.error(message(error))
    }
  }

  return (
    <>
      <PageHeader title={t('resources.title')} subtitle={t(canWrite ? 'resources.subtitle' : 'resources.parentSubtitle')} action={canWrite ? <Button onClick={() => setUploading(true)}><Upload className="h-4 w-4" aria-hidden="true" />{t('resources.upload')}</Button> : undefined} />
      <div className="grid grid-cols-1 gap-4 lg:grid-cols-[16rem_1fr] [&>*]:min-w-0">
        <Card className="h-fit p-2">
          <ul className="space-y-1" aria-label={t('resources.folders')}>
            <li><button type="button" aria-pressed={folder === ''} onClick={() => setFolder('')} className={cn('w-full rounded-md px-3 py-2 text-start text-sm font-semibold hover:bg-slate-50 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring', folder === '' && 'bg-accent')}>{t('resources.allFolders')}</button></li>
            {(folders.data ?? []).map((f) => (
              <li key={f.id} className="flex items-center">
                <button type="button" aria-pressed={folder === f.id} onClick={() => setFolder(f.id)} className={cn('flex-1 rounded-md px-3 py-2 text-start text-sm hover:bg-slate-50 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring', f.parent_folder_id && 'ps-6', folder === f.id && 'bg-accent font-semibold')}>
                  {localized(f.name, i18n.language)} <span className="text-xs text-muted-foreground">({f.documents})</span>{f.audience === 'staff' && <Badge className="ms-1">{t('resources.audiences.staff')}</Badge>}
                </button>
                {canWrite && f.documents === 0 && <Button size="sm" variant="ghost" aria-label={t('common.delete')} onClick={() => { if (window.confirm(t('resources.confirmFolder'))) void run(() => removeFolder.mutateAsync(f.id)) }}><Trash2 className="h-3.5 w-3.5" aria-hidden="true" /></Button>}
              </li>
            ))}
          </ul>
          {canWrite && <Button size="sm" variant="ghost" className="mt-2 w-full" onClick={() => setNewFolder(true)}><FolderPlus className="h-4 w-4" aria-hidden="true" />{t('resources.newFolder')}</Button>}
        </Card>
        <div>
          <div className="relative mb-3">
            <Search className="pointer-events-none absolute start-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground" aria-hidden="true" />
            <Input type="search" value={search} onChange={(e) => setSearch(e.target.value)} placeholder={t('resources.search')} aria-label={t('common.search')} className="ps-9" />
          </div>
          <Card className="overflow-hidden">
            {docs.isLoading ? <ListSkeleton /> : docs.isError ? <ListError error={docs.error} onRetry={() => void docs.refetch()} /> :
              rows.length === 0 ? <EmptyState icon={<FileText className="h-7 w-7" aria-hidden="true" />} title={t('resources.empty')} description={t(canWrite ? 'resources.emptyBody' : 'resources.emptyParent')} /> : (
                <>
                  <ul className="divide-y">
                    {rows.map((d) => (
                      <li key={d.id} className="flex flex-wrap items-center gap-3 px-4 py-3 text-sm">
                        <div className="min-w-0">
                          <p className="font-bold">{d.title}</p>
                          <p className="truncate text-xs text-muted-foreground">{d.file_name} · {size(d.size_bytes)} · v{d.current_version} · {formatDate(d.updated_at, i18n.language)}{d.tags ? ` · ${d.tags}` : ''}</p>
                          {d.description && <p className="text-xs text-muted-foreground">{d.description}</p>}
                        </div>
                        <span className="ms-auto flex flex-wrap gap-1">
                          <Button size="sm" onClick={() => void get(d)}><Download className="h-4 w-4" aria-hidden="true" />{t('resources.download')}</Button>
                          {canWrite && <Button size="sm" variant="secondary" onClick={() => { setTarget(d.id); picker.current?.click() }}>{t('resources.newVersion')}</Button>}
                          {canWrite && <Button size="sm" variant="ghost" aria-label={t('resources.history')} onClick={() => setHistory(d)}><History className="h-4 w-4" aria-hidden="true" /></Button>}
                          {canWrite && <Button size="sm" variant="ghost" aria-label={t('common.delete')} onClick={() => { if (window.confirm(t('resources.confirmDelete'))) void run(() => removeDoc.mutateAsync(d.id)) }}><Trash2 className="h-4 w-4" aria-hidden="true" /></Button>}
                        </span>
                      </li>
                    ))}
                  </ul>
                  <LoadMore visible={!!docs.hasNextPage} loading={docs.isFetchingNextPage} onClick={() => void docs.fetchNextPage()} />
                </>
              )}
          </Card>
        </div>
      </div>
      <input
        ref={picker}
        type="file"
        className="sr-only"
        aria-label={t('resources.newVersion')}
        accept=".pdf,.docx,.xlsx,.jpg,.jpeg,.png,.gif,.webp,.mp4"
        onChange={(e) => {
          const file = e.target.files?.[0]
          e.target.value = ''
          if (file && target) void run(() => version.mutateAsync({ id: target, file }), t('resources.versionAdded'))
        }}
      />
      {newFolder && <FolderDialog onClose={() => setNewFolder(false)} />}
      {uploading && <UploadDialog folderId={folder} onClose={() => setUploading(false)} />}
      {history && <History_ doc={history} onClose={() => setHistory(null)} />}
    </>
  )
}

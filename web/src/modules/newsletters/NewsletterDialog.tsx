import { Sparkles } from 'lucide-react'
import { useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { useDraftNewsletter, useNewsletter, useNewsletterAction, useProgramOptions, useRoomOptions, useSaveNewsletter } from '@/api/hooks'
import { RichTextEditor } from '@/components/shared/RichTextEditor'
import { Button } from '@/components/ui/button'
import { Field, Input, Select } from '@/components/ui/form-controls'
import { Alert, Dialog, DialogContent, DialogHeader, Skeleton } from '@/components/ui/surfaces'
import { toast } from '@/components/ui/toaster'
import { useApiErrorMessage } from '@/hooks/use-api-error'
import { localized } from '@/lib/utils'
import { confirmAction } from '@/components/shared/confirm'

const AUDIENCES = ['all', 'room', 'program']

const localInput = (iso: string | null) => {
  if (!iso) return ''
  const d = new Date(iso)
  const pad = (n: number) => String(n).padStart(2, '0')
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`
}

function Editor({ id, onClose }: { id: string | null; onClose: () => void }) {
  const { t, i18n } = useTranslation()
  const detail = useNewsletter(id)
  const rooms = useRoomOptions()
  const programs = useProgramOptions()
  const save = useSaveNewsletter(id ?? undefined)
  const action = useNewsletterAction()
  const draft = useDraftNewsletter()
  const message = useApiErrorMessage()
  const [titleEn, setTitleEn] = useState('')
  const [titleAr, setTitleAr] = useState('')
  const [bodyEn, setBodyEn] = useState('')
  const [bodyAr, setBodyAr] = useState('')
  const [lang, setLang] = useState<'en' | 'ar'>('en')
  const [audience, setAudience] = useState('all')
  const [picked, setPicked] = useState<string[]>([])
  const [at, setAt] = useState('')
  const [version, setVersion] = useState(0)
  const [failure, setFailure] = useState<string | null>(null)
  const n = detail.data?.newsletter
  const locked = n?.status === 'published' || n?.status === 'archived'

  useEffect(() => {
    if (!detail.data) return
    setTitleEn(detail.data.newsletter.title.en ?? '')
    setTitleAr(detail.data.newsletter.title.ar ?? '')
    setBodyEn(detail.data.body.en ?? '')
    setBodyAr(detail.data.body.ar ?? '')
    setAudience(detail.data.newsletter.audience_type)
    setPicked(detail.data.newsletter.audience_ids)
    setAt(localInput(detail.data.newsletter.scheduled_for))
    setVersion((v) => v + 1)
  }, [detail.data])

  const options = audience === 'room' ? rooms.data ?? [] : audience === 'program' ? programs.data ?? [] : []

  const input = () => ({
    title: (titleAr.trim() ? { en: titleEn.trim(), ar: titleAr.trim() } : { en: titleEn.trim() }) as Record<string, string>,
    body: { en: bodyEn, ...(bodyAr.trim() ? { ar: bodyAr } : {}) } as Record<string, string>,
    audience_type: audience,
    audience_ids: audience === 'all' ? [] : picked,
  })

  const run = async (work: () => Promise<unknown>, done: string, close = false) => {
    if (!titleEn.trim()) return setFailure(t('validation.required'))
    setFailure(null)
    try {
      await work()
      toast.success(done)
      if (close) onClose()
    } catch (error) {
      setFailure(message(error))
    }
  }

  /** Saves what is on screen and returns the newsletter's id (new or existing). */
  const persist = async () => {
    const result = (await save.mutateAsync(input())) as { newsletter: { id: string } }
    return id ?? result.newsletter.id
  }

  const generate = async () => {
    try {
      const d = await draft.mutateAsync(lang)
      if (lang === 'en') { setBodyEn(d.body); if (!titleEn.trim()) setTitleEn(d.title) } else { setBodyAr(d.body); if (!titleAr.trim()) setTitleAr(d.title) }
      setVersion((v) => v + 1)
      toast.success(t(d.used_ai ? 'newsletters.draftAi' : 'newsletters.draftTemplate'))
    } catch (error) {
      toast.error(message(error))
    }
  }

  if (id && !detail.data) return <Skeleton className="h-64" />

  return (
    <div className="space-y-4">
      {failure && <Alert>{failure}</Alert>}
      <fieldset disabled={locked} className="space-y-4">
        <div className="grid gap-3 sm:grid-cols-2">
          <Field label={t('newsletters.titleEn')} htmlFor="nl-en" required><Input id="nl-en" value={titleEn} maxLength={200} onChange={(e) => setTitleEn(e.target.value)} /></Field>
          <Field label={t('newsletters.titleAr')} htmlFor="nl-ar"><Input id="nl-ar" dir="rtl" lang="ar" value={titleAr} maxLength={200} onChange={(e) => setTitleAr(e.target.value)} /></Field>
        </div>
        <div>
          <div className="mb-1 flex flex-wrap items-center gap-2" role="group" aria-label={t('newsletters.language')}>
            <Button type="button" size="sm" variant={lang === 'en' ? 'default' : 'secondary'} aria-pressed={lang === 'en'} onClick={() => setLang('en')}>English</Button>
            <Button type="button" size="sm" variant={lang === 'ar' ? 'default' : 'secondary'} aria-pressed={lang === 'ar'} onClick={() => setLang('ar')}>العربية</Button>
            <Button type="button" size="sm" variant="ghost" className="ms-auto" onClick={() => void generate()} disabled={draft.isPending}><Sparkles className="h-4 w-4" aria-hidden="true" />{t('newsletters.draft')}</Button>
          </div>
          {lang === 'en'
            ? <RichTextEditor key={`en-${version}`} id="nl-body-en" label={t('newsletters.bodyEn')} value={bodyEn} onChange={setBodyEn} />
            : <RichTextEditor key={`ar-${version}`} id="nl-body-ar" label={t('newsletters.bodyAr')} value={bodyAr} onChange={setBodyAr} dir="rtl" />}
        </div>
        <div className="grid gap-3 sm:grid-cols-2">
          <Field label={t('newsletters.audience')} htmlFor="nl-aud">
            <Select id="nl-aud" value={audience} onChange={(e) => { setAudience(e.target.value); setPicked([]) }}>{AUDIENCES.map((a) => <option key={a} value={a}>{t(`newsletters.audiences.${a}`)}</option>)}</Select>
          </Field>
          {audience !== 'all' && (
            <fieldset>
              <legend className="mb-1.5 text-xs font-bold">{t(`newsletters.audiences.${audience}`)}</legend>
              <div className="flex flex-wrap gap-x-4 gap-y-1.5">
                {options.map((o) => (
                  <label key={o.id} className="flex items-center gap-1.5 text-sm">
                    <input type="checkbox" className="h-4 w-4 accent-primary" checked={picked.includes(o.id)} onChange={(e) => setPicked(e.target.checked ? [...picked, o.id] : picked.filter((x) => x !== o.id))} />
                    {localized(o.name, i18n.language)}
                  </label>
                ))}
              </div>
            </fieldset>
          )}
        </div>
        <Field label={t('newsletters.sendAt')} htmlFor="nl-at" hint={t('newsletters.sendAtHint')}><Input id="nl-at" type="datetime-local" dir="ltr" className="text-start" value={at} onChange={(e) => setAt(e.target.value)} /></Field>
      </fieldset>
      <div className="flex flex-wrap justify-end gap-2 border-t pt-4">
        <Button type="button" variant="secondary" onClick={onClose}>{t('common.close')}</Button>
        {!locked && (
          <>
            <Button type="button" variant="secondary" disabled={save.isPending} onClick={() => void run(async () => { await persist() }, t('newsletters.saved'))}>{t('newsletters.saveDraft')}</Button>
            <Button type="button" variant="secondary" disabled={!at || action.isPending} onClick={() => void run(async () => { const sid = await persist(); await action.mutateAsync({ id: sid, action: 'schedule', at: new Date(at).toISOString() }) }, t('newsletters.scheduled'), true)}>{t('newsletters.schedule')}</Button>
            <Button type="button" disabled={action.isPending} onClick={() => void confirmAction({ title: t('newsletters.confirmSend'), confirmLabel: t('newsletters.sendNow'), tone: 'neutral' }).then((ok) => { if (ok) void run(async () => { const sid = await persist(); await action.mutateAsync({ id: sid, action: 'publish' }) }, t('newsletters.sent'), true) })}>{t('newsletters.sendNow')}</Button>
          </>
        )}
      </div>
    </div>
  )
}

/** Write a newsletter in both languages, choose who gets it, and send it now or at a set time. */
export function NewsletterDialog({ id, onClose }: { id: string | null; onClose: () => void }) {
  const { t } = useTranslation()
  return (
    <Dialog open onOpenChange={(o) => !o && onClose()}>
      <DialogContent closeLabel={t('common.close')} className="max-w-3xl">
        <DialogHeader title={t(id ? 'newsletters.edit' : 'newsletters.new')} />
        <Editor id={id} onClose={onClose} />
      </DialogContent>
    </Dialog>
  )
}

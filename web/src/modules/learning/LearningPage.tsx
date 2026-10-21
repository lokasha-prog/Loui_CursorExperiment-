import * as TabsPrimitive from '@radix-ui/react-tabs'
import { Clock, Plus, Search, Trash2, Upload } from 'lucide-react'
import { useMemo, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { flatten, useStudentOptions, useDeleteLesson, useFramework, useFrameworks, useImportFramework, useLessons, useRoomOptions, useSaveFramework, useSaveLesson } from '@/api/hooks'
import type { FrameworkDetail, Lesson } from '@/api/types'
import { PageHeader } from '@/components/layout/PageHeader'
import { ListError, ListSkeleton, LoadMore } from '@/components/shared/ListState'
import { Button } from '@/components/ui/button'
import { Field, Input, Select, Textarea } from '@/components/ui/form-controls'
import { Alert, Badge, Card, Dialog, DialogContent, DialogHeader, EmptyState } from '@/components/ui/surfaces'
import { toast } from '@/components/ui/toaster'
import { useApiErrorMessage } from '@/hooks/use-api-error'
import { localized } from '@/lib/utils'
import { can, useAuth } from '@/stores/auth'
import { AssessmentsTab } from './AssessmentsTab'
import { JourneyView } from './JourneyView'
import { ObservationsTab } from './ObservationsTab'

const lines = (text: string) => text.split('\n').map((l) => l.trim()).filter(Boolean)

function LessonDialog({ lesson, frameworkId, onClose }: { lesson: Lesson | null; frameworkId: string; onClose: () => void }) {
  const { t, i18n } = useTranslation()
  const lang = i18n.language
  const message = useApiErrorMessage()
  const frameworks = useFrameworks()
  const rooms = useRoomOptions()
  const [fw, setFw] = useState(lesson?.framework_id ?? frameworkId)
  const detail = useFramework(fw)
  const save = useSaveLesson(lesson?.id)
  const [en, setEn] = useState(lesson?.title.en ?? '')
  const [ar, setAr] = useState(lesson?.title.ar ?? '')
  const [descEn, setDescEn] = useState(lesson?.description?.en ?? '')
  const [descAr, setDescAr] = useState(lesson?.description?.ar ?? '')
  const [area, setArea] = useState(lesson?.area_id ?? '')
  const [band, setBand] = useState(lesson?.age_band_id ?? '')
  const [room, setRoom] = useState(lesson?.room_id ?? '')
  const [minutes, setMinutes] = useState(String(lesson?.duration_minutes ?? 30))
  const [materials, setMaterials] = useState((lesson?.materials ?? []).join('\n'))
  const [objectives, setObjectives] = useState((lesson?.objectives ?? []).join('\n'))
  const [links, setLinks] = useState(Object.entries(lesson?.links ?? {}).map(([url, title]) => `${url} ${title}`).join('\n'))
  const [picked, setPicked] = useState<string[]>(lesson?.milestone_ids ?? [])
  const [failure, setFailure] = useState<string | null>(null)
  const milestones = (detail.data?.milestones ?? []).filter((m) => (!area || m.area_id === area) && (!band || m.age_band_id === band))

  const submit = async () => {
    if (!en.trim()) return setFailure(t('validation.required'))
    if (!fw) return setFailure(t('learning.lesson.pickFramework'))
    setFailure(null)
    const linkMap: Record<string, string> = {}
    lines(links).forEach((l) => { const [url, ...rest] = l.split(/\s+/); linkMap[url] = rest.join(' ') || url })
    try {
      await save.mutateAsync({
        title: ar.trim() ? { en: en.trim(), ar: ar.trim() } : { en: en.trim() },
        description: descEn.trim() || descAr.trim() ? { ...(descEn.trim() ? { en: descEn.trim() } : {}), ...(descAr.trim() ? { ar: descAr.trim() } : {}) } : null,
        framework_id: fw, area_id: area || null, age_band_id: band || null, room_id: room || null, duration_minutes: Number(minutes) || 30,
        materials: lines(materials), objectives: lines(objectives), links: linkMap, milestone_ids: picked,
      })
      toast.success(t('learning.lesson.saved'))
      onClose()
    } catch (error) {
      setFailure(message(error))
    }
  }

  return (
    <Dialog open onOpenChange={(o) => !o && onClose()}>
      <DialogContent closeLabel={t('common.close')} className="max-w-2xl">
        <DialogHeader title={lesson ? t('learning.lesson.edit') : t('learning.lesson.new')} />
        <form onSubmit={(e) => { e.preventDefault(); void submit() }} className="max-h-[70vh] space-y-4 overflow-y-auto pe-1" noValidate>
          {failure && <Alert>{failure}</Alert>}
          <div className="grid gap-3 sm:grid-cols-2">
            <Field label={t('learning.lesson.titleEn')} htmlFor="ls-en" required><Input id="ls-en" value={en} maxLength={150} onChange={(e) => setEn(e.target.value)} /></Field>
            <Field label={t('learning.lesson.titleAr')} htmlFor="ls-ar"><Input id="ls-ar" dir="rtl" lang="ar" value={ar} maxLength={150} onChange={(e) => setAr(e.target.value)} /></Field>
            <Field label={t('learning.lesson.descEn')} htmlFor="ls-den"><Textarea id="ls-den" value={descEn} onChange={(e) => setDescEn(e.target.value)} /></Field>
            <Field label={t('learning.lesson.descAr')} htmlFor="ls-dar"><Textarea id="ls-dar" dir="rtl" lang="ar" value={descAr} onChange={(e) => setDescAr(e.target.value)} /></Field>
            <Field label={t('learning.framework')} htmlFor="ls-fw"><Select id="ls-fw" value={fw} onChange={(e) => { setFw(e.target.value); setArea(''); setBand(''); setPicked([]) }}><option value="">—</option>{(frameworks.data ?? []).map((f) => <option key={f.id} value={f.id}>{localized(f.name, lang)}</option>)}</Select></Field>
            <Field label={t('learning.room')} htmlFor="ls-room"><Select id="ls-room" value={room} onChange={(e) => setRoom(e.target.value)}><option value="">{t('learning.allRooms')}</option>{(rooms.data ?? []).map((r) => <option key={r.id} value={r.id}>{localized(r.name, lang)}</option>)}</Select></Field>
            <Field label={t('learning.area')} htmlFor="ls-area"><Select id="ls-area" value={area} onChange={(e) => setArea(e.target.value)}><option value="">—</option>{(detail.data?.areas ?? []).map((a) => <option key={a.id} value={a.id}>{localized(a.name, lang)}</option>)}</Select></Field>
            <Field label={t('learning.band')} htmlFor="ls-band"><Select id="ls-band" value={band} onChange={(e) => setBand(e.target.value)}><option value="">—</option>{(detail.data?.age_bands ?? []).map((b) => <option key={b.id} value={b.id}>{localized(b.name, lang)}</option>)}</Select></Field>
            <Field label={t('learning.lesson.minutes')} htmlFor="ls-min"><Input id="ls-min" type="number" min={5} max={480} value={minutes} onChange={(e) => setMinutes(e.target.value)} /></Field>
          </div>
          <div className="grid gap-3 sm:grid-cols-2">
            <Field label={t('learning.lesson.materials')} htmlFor="ls-mat" hint={t('learning.lesson.onePerLine')}><Textarea id="ls-mat" value={materials} onChange={(e) => setMaterials(e.target.value)} /></Field>
            <Field label={t('learning.lesson.objectives')} htmlFor="ls-obj" hint={t('learning.lesson.onePerLine')}><Textarea id="ls-obj" value={objectives} onChange={(e) => setObjectives(e.target.value)} /></Field>
          </div>
          <Field label={t('learning.lesson.links')} htmlFor="ls-links" hint={t('learning.lesson.linksHint')}><Textarea id="ls-links" dir="ltr" value={links} onChange={(e) => setLinks(e.target.value)} /></Field>
          {milestones.length > 0 && (
            <fieldset className="rounded-md border p-3">
              <legend className="px-1 text-sm font-bold">{t('learning.lesson.milestones')}</legend>
              <ul className="max-h-40 space-y-1 overflow-y-auto text-sm">
                {milestones.map((m) => (
                  <li key={m.id}><label className="flex items-start gap-2"><input type="checkbox" className="mt-1" checked={picked.includes(m.id)} onChange={(e) => setPicked((p) => (e.target.checked ? [...p, m.id] : p.filter((x) => x !== m.id)))} /><span>{localized(m.statement, lang)}</span></label></li>
                ))}
              </ul>
            </fieldset>
          )}
          <div className="flex justify-end gap-2 border-t pt-4"><Button type="button" variant="secondary" onClick={onClose}>{t('common.cancel')}</Button><Button type="submit" disabled={save.isPending}>{t('learning.lesson.save')}</Button></div>
        </form>
      </DialogContent>
    </Dialog>
  )
}

function LessonsTab({ canWrite }: { canWrite: boolean }) {
  const { t, i18n } = useTranslation()
  const lang = i18n.language
  const frameworks = useFrameworks()
  const [fw, setFw] = useState('')
  const [area, setArea] = useState('')
  const [band, setBand] = useState('')
  const [search, setSearch] = useState('')
  const detail = useFramework(fw)
  const query = useLessons({ frameworkId: fw, areaId: area, ageBandId: band, search })
  const remove = useDeleteLesson()
  const [editing, setEditing] = useState<{ lesson: Lesson | null } | null>(null)
  const lessons = flatten(query.data)
  const names = useMemo(() => {
    const map = new Map<string, string>()
    ;(detail.data?.areas ?? []).forEach((a) => map.set(a.id, localized(a.name, lang)))
    ;(detail.data?.age_bands ?? []).forEach((b) => map.set(b.id, localized(b.name, lang)))
    return map
  }, [detail.data, lang])

  return (
    <>
      <div className="mb-4 flex flex-wrap items-end gap-3">
        <div className="relative min-w-[220px] flex-1">
          <Search className="pointer-events-none absolute start-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground" aria-hidden="true" />
          <Input type="search" value={search} onChange={(e) => setSearch(e.target.value)} placeholder={t('learning.search')} aria-label={t('common.search')} className="ps-9" />
        </div>
        <Select value={fw} onChange={(e) => { setFw(e.target.value); setArea(''); setBand('') }} aria-label={t('learning.framework')} className="w-52"><option value="">{t('learning.allFrameworks')}</option>{(frameworks.data ?? []).map((f) => <option key={f.id} value={f.id}>{localized(f.name, lang)}</option>)}</Select>
        {fw && <Select value={area} onChange={(e) => setArea(e.target.value)} aria-label={t('learning.area')} className="w-56"><option value="">{t('learning.allAreas')}</option>{(detail.data?.areas ?? []).map((a) => <option key={a.id} value={a.id}>{localized(a.name, lang)}</option>)}</Select>}
        {fw && <Select value={band} onChange={(e) => setBand(e.target.value)} aria-label={t('learning.band')} className="w-44"><option value="">{t('learning.allBands')}</option>{(detail.data?.age_bands ?? []).map((b) => <option key={b.id} value={b.id}>{localized(b.name, lang)}</option>)}</Select>}
        {canWrite && <Button onClick={() => setEditing({ lesson: null })} disabled={(frameworks.data ?? []).length === 0}><Plus className="h-4 w-4" aria-hidden="true" />{t('learning.lesson.new')}</Button>}
      </div>
      {query.isLoading ? <ListSkeleton /> : query.isError ? <ListError error={query.error} onRetry={() => void query.refetch()} /> : lessons.length === 0 ? (
        <EmptyState title={t('learning.lesson.empty')} description={t('learning.lesson.emptyHint')} />
      ) : (
        <div className="grid gap-4 md:grid-cols-2">
          {lessons.map((l) => (
            <Card key={l.id} className="p-4">
              <div className="flex items-start justify-between gap-2">
                <h3 className="text-base font-bold">{localized(l.title, lang)}</h3>
                <Badge tone="neutral"><Clock className="me-1 inline h-3 w-3" aria-hidden="true" />{t('learning.lesson.min', { count: l.duration_minutes })}</Badge>
              </div>
              {l.description && <p className="mt-1 text-sm text-muted-foreground">{localized(l.description, lang)}</p>}
              <div className="mt-2 flex flex-wrap gap-1.5">
                {l.area_id && names.get(l.area_id) && <Badge tone="info">{names.get(l.area_id)}</Badge>}
                {l.age_band_id && names.get(l.age_band_id) && <Badge tone="neutral">{names.get(l.age_band_id)}</Badge>}
                {!l.room_id && <Badge tone="success">{t('learning.allRooms')}</Badge>}
              </div>
              {l.objectives.length > 0 && <ul className="mt-2 list-disc ps-5 text-sm">{l.objectives.map((o) => <li key={o}>{o}</li>)}</ul>}
              {l.materials.length > 0 && <p className="mt-2 text-xs text-muted-foreground"><span className="font-semibold">{t('learning.lesson.materials')}:</span> {l.materials.join(', ')}</p>}
              {Object.keys(l.links).length > 0 && <p className="mt-1 text-xs">{Object.entries(l.links).map(([url, title]) => <a key={url} href={url} target="_blank" rel="noopener noreferrer" className="me-3 font-semibold text-primary-dark underline-offset-4 hover:underline">{title}</a>)}</p>}
              {canWrite && (
                <div className="mt-3 flex gap-2 border-t pt-3">
                  <Button size="sm" variant="secondary" onClick={() => setEditing({ lesson: l })}>{t('common.edit')}</Button>
                  <Button size="sm" variant="ghost" onClick={() => { if (window.confirm(t('learning.lesson.confirmDelete'))) remove.mutate(l.id, { onSuccess: () => toast.success(t('learning.lesson.deleted')) }) }}><Trash2 className="h-4 w-4" aria-hidden="true" />{t('common.delete')}</Button>
                </div>
              )}
            </Card>
          ))}
        </div>
      )}
      <LoadMore visible={!!query.hasNextPage} loading={query.isFetchingNextPage} onClick={() => void query.fetchNextPage()} />
      {editing && <LessonDialog lesson={editing.lesson} frameworkId={fw} onClose={() => setEditing(null)} />}
    </>
  )
}

function FrameworkTree({ detail }: { detail: FrameworkDetail }) {
  const { t, i18n } = useTranslation()
  const lang = i18n.language
  return (
    <div className="space-y-4">
      {detail.areas.length === 0 && <EmptyState title={t('learning.curriculum.emptyTree')} description={t('learning.curriculum.emptyTreeHint')} />}
      {detail.areas.map((area) => {
        const inArea = detail.milestones.filter((m) => m.area_id === area.id)
        return (
          <Card key={area.id} className="p-4">
            <h3 className="mb-2 flex items-center gap-2 text-base font-bold">{localized(area.name, lang)} <Badge tone="neutral">{area.code}</Badge><span className="ms-auto text-xs font-normal text-muted-foreground">{t('learning.curriculum.count', { count: inArea.length })}</span></h3>
            {detail.age_bands.map((band) => {
              const items = inArea.filter((m) => m.age_band_id === band.id)
              if (items.length === 0) return null
              return (
                <section key={band.id} className="mt-3" aria-label={localized(band.name, lang)}>
                  <h4 className="mb-1 text-sm font-bold text-primary-dark">{localized(band.name, lang)} <span className="text-xs font-normal text-muted-foreground" dir="ltr">({band.min_months}-{band.max_months})</span></h4>
                  <ul className="list-disc space-y-0.5 ps-5 text-sm">{items.map((m) => <li key={m.id}>{localized(m.statement, lang)}</li>)}</ul>
                </section>
              )
            })}
          </Card>
        )
      })}
    </div>
  )
}

function NewFrameworkDialog({ onClose }: { onClose: () => void }) {
  const { t } = useTranslation()
  const save = useSaveFramework()
  const message = useApiErrorMessage()
  const [code, setCode] = useState('')
  const [en, setEn] = useState('')
  const [ar, setAr] = useState('')
  const [failure, setFailure] = useState<string | null>(null)
  const submit = async () => {
    if (!code.trim() || !en.trim()) return setFailure(t('validation.required'))
    try {
      await save.mutateAsync({ code: code.trim().toLowerCase(), name: ar.trim() ? { en: en.trim(), ar: ar.trim() } : { en: en.trim() } })
      toast.success(t('learning.curriculum.created'))
      onClose()
    } catch (error) {
      setFailure(message(error))
    }
  }
  return (
    <Dialog open onOpenChange={(o) => !o && onClose()}>
      <DialogContent closeLabel={t('common.close')} className="max-w-md">
        <DialogHeader title={t('learning.curriculum.new')} />
        <form onSubmit={(e) => { e.preventDefault(); void submit() }} className="space-y-3" noValidate>
          {failure && <Alert>{failure}</Alert>}
          <Field label={t('learning.curriculum.code')} htmlFor="nf-code" hint={t('learning.curriculum.codeHint')} required><Input id="nf-code" dir="ltr" value={code} maxLength={39} onChange={(e) => setCode(e.target.value)} /></Field>
          <Field label={t('learning.lesson.titleEn')} htmlFor="nf-en" required><Input id="nf-en" value={en} onChange={(e) => setEn(e.target.value)} /></Field>
          <Field label={t('learning.lesson.titleAr')} htmlFor="nf-ar"><Input id="nf-ar" dir="rtl" lang="ar" value={ar} onChange={(e) => setAr(e.target.value)} /></Field>
          <div className="flex justify-end gap-2 border-t pt-4"><Button type="button" variant="secondary" onClick={onClose}>{t('common.cancel')}</Button><Button type="submit" disabled={save.isPending}>{t('common.save')}</Button></div>
        </form>
      </DialogContent>
    </Dialog>
  )
}

function ImportDialog({ id, onClose }: { id: string; onClose: () => void }) {
  const { t } = useTranslation()
  const run = useImportFramework(id)
  const message = useApiErrorMessage()
  const [text, setText] = useState('')
  const [failure, setFailure] = useState<string | null>(null)
  const submit = async () => {
    let body: unknown
    try { body = JSON.parse(text) } catch { return setFailure(t('learning.curriculum.badJson')) }
    try {
      await run.mutateAsync(body)
      toast.success(t('learning.curriculum.imported'))
      onClose()
    } catch (error) {
      setFailure(message(error))
    }
  }
  return (
    <Dialog open onOpenChange={(o) => !o && onClose()}>
      <DialogContent closeLabel={t('common.close')} className="max-w-2xl">
        <DialogHeader title={t('learning.curriculum.import')} />
        <div className="space-y-3">
          <p className="text-sm text-muted-foreground">{t('learning.curriculum.importHint')}</p>
          {failure && <Alert>{failure}</Alert>}
          <Textarea dir="ltr" rows={12} value={text} onChange={(e) => setText(e.target.value)} aria-label={t('learning.curriculum.import')} placeholder='{ "areas": [{ "code": "cal", "name": { "en": "..." } }], "age_bands": [...], "milestones": [...] }' />
          <div className="flex justify-end gap-2 border-t pt-4"><Button type="button" variant="secondary" onClick={onClose}>{t('common.cancel')}</Button><Button onClick={() => void submit()} disabled={run.isPending || !text.trim()}>{t('learning.curriculum.importRun')}</Button></div>
        </div>
      </DialogContent>
    </Dialog>
  )
}

function CurriculumTab({ canManage }: { canManage: boolean }) {
  const { t, i18n } = useTranslation()
  const lang = i18n.language
  const frameworks = useFrameworks()
  const [selected, setSelected] = useState('')
  const [dialog, setDialog] = useState<'new' | 'import' | null>(null)
  const list = frameworks.data ?? []
  const current = selected || list[0]?.id || ''
  const detail = useFramework(current)

  return (
    <>
      <div className="mb-4 flex flex-wrap items-center gap-3">
        {list.length > 0 && <Select value={current} onChange={(e) => setSelected(e.target.value)} aria-label={t('learning.framework')} className="w-64">{list.map((f) => <option key={f.id} value={f.id}>{localized(f.name, lang)}</option>)}</Select>}
        {canManage && <Button variant="secondary" onClick={() => setDialog('new')}><Plus className="h-4 w-4" aria-hidden="true" />{t('learning.curriculum.new')}</Button>}
        {canManage && current && <Button variant="secondary" onClick={() => setDialog('import')}><Upload className="h-4 w-4" aria-hidden="true" />{t('learning.curriculum.import')}</Button>}
      </div>
      {frameworks.isLoading ? <ListSkeleton /> : list.length === 0 ? (
        <EmptyState title={t('learning.curriculum.none')} description={canManage ? t('learning.curriculum.noneHint') : t('learning.curriculum.noneStaff')} />
      ) : !detail.data ? <ListSkeleton /> : (
        <>
          <p className="mb-3 text-sm text-muted-foreground">{t('learning.curriculum.summary', { areas: detail.data.areas.length, bands: detail.data.age_bands.length, milestones: detail.data.milestones.length })}</p>
          <FrameworkTree detail={detail.data} />
        </>
      )}
      {dialog === 'new' && <NewFrameworkDialog onClose={() => setDialog(null)} />}
      {dialog === 'import' && current && <ImportDialog id={current} onClose={() => setDialog(null)} />}
    </>
  )
}

export default function LearningPage() {
  const { t } = useTranslation()
  const me = useAuth((s) => s.me)
  const [tab, setTab] = useState('lessons')
  const tabs = ['lessons', 'observations', 'assessments', 'journey', 'curriculum']
  const [journeyStudent, setJourneyStudent] = useState('')
  const students = useStudentOptions()
  return (
    <>
      <PageHeader title={t('learning.title')} subtitle={t('learning.subtitle')} />
      <TabsPrimitive.Root value={tab} onValueChange={setTab}>
        <TabsPrimitive.List className="mb-5 flex gap-1 overflow-x-auto border-b">
          {tabs.map((key) => (
            <TabsPrimitive.Trigger key={key} value={key} className="whitespace-nowrap border-b-2 border-transparent px-4 py-2.5 text-sm font-semibold text-muted-foreground transition-colors hover:text-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring data-[state=active]:border-primary data-[state=active]:font-bold data-[state=active]:text-primary-dark">
              {t(`learning.tabs.${key}`)}
            </TabsPrimitive.Trigger>
          ))}
        </TabsPrimitive.List>
        <TabsPrimitive.Content value="lessons"><LessonsTab canWrite={can(me, 'learning.write')} /></TabsPrimitive.Content>
        <TabsPrimitive.Content value="observations"><ObservationsTab canWrite={can(me, 'learning.write')} /></TabsPrimitive.Content>
        <TabsPrimitive.Content value="assessments"><AssessmentsTab canWrite={can(me, 'learning.write')} canApprove={can(me, 'learning.approve')} /></TabsPrimitive.Content>
        <TabsPrimitive.Content value="journey">
          <div className="mb-4"><Select value={journeyStudent} onChange={(e) => setJourneyStudent(e.target.value)} aria-label={t('learning.obs.child')} className="w-64"><option value="">{t('learning.journey.pick')}</option>{(students.data ?? []).map((st) => <option key={st.id} value={st.id}>{st.first_name} {st.last_name}</option>)}</Select></div>
          <JourneyView studentId={journeyStudent} />
        </TabsPrimitive.Content>
        <TabsPrimitive.Content value="curriculum"><CurriculumTab canManage={can(me, 'settings.write')} /></TabsPrimitive.Content>
      </TabsPrimitive.Root>
    </>
  )
}

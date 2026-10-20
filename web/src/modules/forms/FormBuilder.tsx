import { ArrowDown, ArrowUp, Plus, Trash2 } from 'lucide-react'
import { useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { useArchiveForm, useCreateForm, useForm, usePublishForm, useSaveFields, useUpdateForm } from '@/api/hooks'
import type { FormFieldDef } from '@/api/types'
import { Button } from '@/components/ui/button'
import { Field, Input, Select } from '@/components/ui/form-controls'
import { Alert, Badge, Dialog, DialogContent, DialogHeader, Skeleton } from '@/components/ui/surfaces'
import { toast } from '@/components/ui/toaster'
import { useApiErrorMessage } from '@/hooks/use-api-error'
import { FormRenderer } from './FormRenderer'

export const FORM_TYPES = ['enquiry', 'admission', 'check_in_out', 'incident', 'custom']
export const FIELD_TYPES = ['text', 'textarea', 'number', 'date', 'dropdown', 'radio', 'checkbox', 'file', 'signature', 'section']
const WITH_OPTIONS = ['dropdown', 'radio']

const newId = (fields: FormFieldDef[]) => {
  let n = fields.length + 1
  while (fields.some((f) => f.id === `q${n}`)) n++
  return `q${n}`
}
const named = (en: string, ar: string): Record<string, string> => (ar.trim() ? { en: en.trim(), ar: ar.trim() } : { en: en.trim() })

function FieldEditor({ field, index, count, all, onChange, onMove, onRemove }: {
  field: FormFieldDef
  index: number
  count: number
  all: FormFieldDef[]
  onChange: (patch: Partial<FormFieldDef>) => void
  onMove: (by: number) => void
  onRemove: () => void
}) {
  const { t } = useTranslation()
  const above = all.slice(0, index).filter((f) => f.type !== 'section')
  const trigger = above.find((f) => f.id === field.show_when_field_id)
  const choices = trigger ? (trigger.type === 'checkbox' ? [{ value: 'true', en: t('forms.builder.checked') }, { value: 'false', en: t('forms.builder.unchecked') }] : trigger.options.map((o) => ({ value: o.value, en: o.label.en ?? o.value }))) : []

  return (
    <li className="space-y-3 rounded-md border p-3">
      <div className="grid gap-2 sm:grid-cols-6">
        <Field label={t('forms.builder.type')} htmlFor={`fe-t-${index}`} className="sm:col-span-2">
          <Select id={`fe-t-${index}`} value={field.type} onChange={(e) => onChange({ type: e.target.value, options: WITH_OPTIONS.includes(e.target.value) && field.options.length === 0 ? [{ value: 'option1', label: { en: 'Option 1' } }] : field.options })}>
            {FIELD_TYPES.map((x) => <option key={x} value={x}>{t(`forms.fieldTypes.${x}`)}</option>)}
          </Select>
        </Field>
        <Field label={t('forms.builder.labelEn')} htmlFor={`fe-en-${index}`} className="sm:col-span-2"><Input id={`fe-en-${index}`} value={field.label.en ?? ''} onChange={(e) => onChange({ label: { ...field.label, en: e.target.value } })} /></Field>
        <Field label={t('forms.builder.labelAr')} htmlFor={`fe-ar-${index}`} className="sm:col-span-2"><Input id={`fe-ar-${index}`} dir="rtl" lang="ar" value={field.label.ar ?? ''} onChange={(e) => onChange({ label: { ...field.label, ar: e.target.value } })} /></Field>
      </div>
      {WITH_OPTIONS.includes(field.type) && (
        <div className="space-y-2">
          <p className="text-xs font-bold">{t('forms.builder.options')}</p>
          {field.options.map((o, i) => (
            <div key={i} className="grid gap-2 sm:grid-cols-[1fr_1fr_1fr_auto]">
              <Input aria-label={t('forms.builder.optionValue')} placeholder={t('forms.builder.optionValue')} dir="ltr" className="text-start" value={o.value} onChange={(e) => onChange({ options: field.options.map((x, k) => (k === i ? { ...x, value: e.target.value.replace(/\s+/g, '_') } : x)) })} />
              <Input aria-label={t('forms.builder.labelEn')} placeholder={t('forms.builder.labelEn')} value={o.label.en ?? ''} onChange={(e) => onChange({ options: field.options.map((x, k) => (k === i ? { ...x, label: { ...x.label, en: e.target.value } } : x)) })} />
              <Input aria-label={t('forms.builder.labelAr')} placeholder={t('forms.builder.labelAr')} dir="rtl" lang="ar" value={o.label.ar ?? ''} onChange={(e) => onChange({ options: field.options.map((x, k) => (k === i ? { ...x, label: { ...x.label, ar: e.target.value } } : x)) })} />
              <Button type="button" size="sm" variant="ghost" aria-label={t('common.delete')} onClick={() => onChange({ options: field.options.filter((_, k) => k !== i) })}><Trash2 className="h-4 w-4" aria-hidden="true" /></Button>
            </div>
          ))}
          <Button type="button" size="sm" variant="secondary" onClick={() => onChange({ options: [...field.options, { value: `option${field.options.length + 1}`, label: { en: `Option ${field.options.length + 1}` } }] })}><Plus className="h-4 w-4" aria-hidden="true" />{t('forms.builder.addOption')}</Button>
        </div>
      )}
      <div className="flex flex-wrap items-end gap-3">
        {field.type !== 'section' && <label className="flex items-center gap-1.5 text-sm"><input type="checkbox" className="h-4 w-4 accent-primary" checked={field.required} onChange={(e) => onChange({ required: e.target.checked })} />{t('forms.builder.required')}</label>}
        {above.length > 0 && (
          <>
            <Field label={t('forms.builder.showWhen')} htmlFor={`fe-sw-${index}`} className="min-w-44">
              <Select id={`fe-sw-${index}`} value={field.show_when_field_id ?? ''} onChange={(e) => onChange({ show_when_field_id: e.target.value || null, show_when_equals: null })}>
                <option value="">{t('forms.builder.always')}</option>
                {above.map((f) => <option key={f.id} value={f.id}>{f.label.en || f.id}</option>)}
              </Select>
            </Field>
            {trigger && (
              <Field label={t('forms.builder.equals')} htmlFor={`fe-eq-${index}`} className="min-w-40">
                {choices.length > 0 ? (
                  <Select id={`fe-eq-${index}`} value={field.show_when_equals ?? ''} onChange={(e) => onChange({ show_when_equals: e.target.value || null })}>
                    <option value="">—</option>
                    {choices.map((c) => <option key={c.value} value={c.value}>{c.en}</option>)}
                  </Select>
                ) : (
                  <Input id={`fe-eq-${index}`} value={field.show_when_equals ?? ''} onChange={(e) => onChange({ show_when_equals: e.target.value || null })} />
                )}
              </Field>
            )}
          </>
        )}
        <span className="ms-auto flex gap-1">
          <Button type="button" size="sm" variant="ghost" aria-label={t('forms.builder.up')} disabled={index === 0} onClick={() => onMove(-1)}><ArrowUp className="h-4 w-4" aria-hidden="true" /></Button>
          <Button type="button" size="sm" variant="ghost" aria-label={t('forms.builder.down')} disabled={index === count - 1} onClick={() => onMove(1)}><ArrowDown className="h-4 w-4" aria-hidden="true" /></Button>
          <Button type="button" size="sm" variant="ghost" aria-label={t('common.delete')} onClick={onRemove}><Trash2 className="h-4 w-4" aria-hidden="true" /></Button>
        </span>
      </div>
    </li>
  )
}

function Editor({ id, onClose, onCreated }: { id: string | null; onClose: () => void; onCreated: (id: string) => void }) {
  const { t } = useTranslation()
  const detail = useForm(id)
  const create = useCreateForm()
  const update = useUpdateForm(id ?? '')
  const saveFields = useSaveFields(id ?? '')
  const publish = usePublishForm(id ?? '')
  const archive = useArchiveForm(id ?? '')
  const message = useApiErrorMessage()
  const [nameEn, setNameEn] = useState('')
  const [nameAr, setNameAr] = useState('')
  const [type, setType] = useState('custom')
  const [isPublic, setIsPublic] = useState(false)
  const [fields, setFields] = useState<FormFieldDef[]>([])
  const [preview, setPreview] = useState(false)
  const [failure, setFailure] = useState<string | null>(null)
  const form = detail.data?.form

  useEffect(() => {
    if (!detail.data) return
    setNameEn(detail.data.form.name.en ?? '')
    setNameAr(detail.data.form.name.ar ?? '')
    setType(detail.data.form.type)
    setIsPublic(detail.data.form.is_public)
    setFields(detail.data.fields)
  }, [detail.data])

  const patch = (i: number, p: Partial<FormFieldDef>) => setFields((all) => all.map((f, k) => (k === i ? { ...f, ...p } : f)))
  const move = (i: number, by: number) => setFields((all) => { const next = [...all]; [next[i], next[i + by]] = [next[i + by], next[i]]; return next })
  const add = () => setFields((all) => [...all, { id: newId(all), type: 'text', label: { en: '' }, required: false, options: [], show_when_field_id: null, show_when_equals: null }])

  const run = async (action: () => Promise<unknown>, done: string) => {
    setFailure(null)
    try {
      await action()
      toast.success(done)
    } catch (error) {
      setFailure(message(error))
    }
  }

  const saveAll = async () => {
    if (!nameEn.trim()) return setFailure(t('validation.required'))
    const meta = { name: named(nameEn, nameAr), description: null, type, is_public: isPublic }
    setFailure(null)
    try {
      if (!id) {
        const made = (await create.mutateAsync(meta)) as { form: { id: string } }
        toast.success(t('forms.builder.created'))
        onCreated(made.form.id)
        return
      }
      await update.mutateAsync(meta)
      await saveFields.mutateAsync(fields.map((f) => ({ ...f, label: named(f.label.en ?? '', f.label.ar ?? '') })))
      toast.success(t('forms.builder.saved'))
    } catch (error) {
      setFailure(message(error))
    }
  }

  if (id && !detail.data) return <Skeleton className="h-64" />

  return (
    <div className="space-y-4">
      {failure && <Alert>{failure}</Alert>}
      <div className="grid gap-3 sm:grid-cols-2">
        <Field label={t('forms.builder.nameEn')} htmlFor="fb-en" required><Input id="fb-en" value={nameEn} maxLength={150} onChange={(e) => setNameEn(e.target.value)} /></Field>
        <Field label={t('forms.builder.nameAr')} htmlFor="fb-ar"><Input id="fb-ar" dir="rtl" lang="ar" value={nameAr} maxLength={150} onChange={(e) => setNameAr(e.target.value)} /></Field>
        <Field label={t('forms.builder.formType')} htmlFor="fb-type"><Select id="fb-type" value={type} onChange={(e) => setType(e.target.value)}>{FORM_TYPES.map((x) => <option key={x} value={x}>{t(`forms.types.${x}`)}</option>)}</Select></Field>
        <label className="flex items-center gap-2 self-end pb-2 text-sm"><input type="checkbox" className="h-4 w-4 accent-primary" checked={isPublic} onChange={(e) => setIsPublic(e.target.checked)} />{t('forms.builder.public')}</label>
      </div>

      {id && form && (
        <>
          <div className="flex flex-wrap items-center gap-2 text-sm">
            <Badge tone={form.status === 'live' ? 'success' : form.status === 'archived' ? 'neutral' : 'warning'}>{t(`forms.status.${form.status}`)}</Badge>
            <span className="text-muted-foreground">{form.published_version > 0 ? t('forms.builder.versionLive', { live: form.published_version, editing: detail.data!.editing_version }) : t('forms.builder.notPublished')}</span>
            {isPublic && form.status === 'live' && <span className="font-mono text-xs" dir="ltr">/forms/public/&lt;centre&gt;/{form.id}</span>}
          </div>
          <ol className="space-y-3">
            {fields.map((f, i) => <FieldEditor key={f.id} field={f} index={i} count={fields.length} all={fields} onChange={(p) => patch(i, p)} onMove={(by) => move(i, by)} onRemove={() => setFields((all) => all.filter((_, k) => k !== i))} />)}
          </ol>
          <div className="flex flex-wrap gap-2">
            <Button type="button" variant="secondary" onClick={add}><Plus className="h-4 w-4" aria-hidden="true" />{t('forms.builder.addField')}</Button>
            <Button type="button" variant="ghost" onClick={() => setPreview((p) => !p)}>{t('forms.builder.preview')}</Button>
          </div>
          {preview && (
            <div className="rounded-md border bg-slate-50 p-4">
              <p className="mb-3 text-xs font-bold uppercase text-muted-foreground">{t('forms.builder.previewNote')}</p>
              <FormRenderer fields={fields.map((f) => ({ ...f, label: named(f.label.en ?? '', f.label.ar ?? '') }))} submitLabel={t('forms.fill.submit')} onSubmit={() => toast.success(t('forms.builder.previewOk'))} />
            </div>
          )}
        </>
      )}

      <div className="flex flex-wrap justify-end gap-2 border-t pt-4">
        {id && form && form.status !== 'archived' && <Button type="button" variant="ghost" className="me-auto" onClick={() => void run(() => archive.mutateAsync(true), t('forms.builder.archived'))}>{t('forms.builder.archive')}</Button>}
        {id && form?.status === 'archived' && <Button type="button" variant="ghost" className="me-auto" onClick={() => void run(() => archive.mutateAsync(false), t('forms.builder.restored'))}>{t('forms.builder.restore')}</Button>}
        <Button type="button" variant="secondary" onClick={onClose}>{t('common.close')}</Button>
        <Button type="button" variant={id && detail.data?.has_unpublished_changes ? 'secondary' : 'default'} onClick={() => void saveAll()} disabled={create.isPending || update.isPending || saveFields.isPending}>{t('forms.builder.save')}</Button>
        {id && <Button type="button" onClick={() => void run(async () => { await saveAll(); await publish.mutateAsync() }, t('forms.builder.published'))} disabled={publish.isPending}>{t('forms.builder.publish')}</Button>}
      </div>
    </div>
  )
}

/** Make a new form, or edit one: name, kind, questions (with choices and show-only-if rules), preview, and publish. */
export function FormBuilder({ id, onClose }: { id: string | null; onClose: () => void }) {
  const { t } = useTranslation()
  const [current, setCurrent] = useState(id)
  return (
    <Dialog open onOpenChange={(o) => !o && onClose()}>
      <DialogContent closeLabel={t('common.close')} className="max-w-4xl">
        <DialogHeader title={t(current ? 'forms.builder.edit' : 'forms.builder.new')} />
        <Editor key={current ?? 'new'} id={current} onClose={onClose} onCreated={setCurrent} />
      </DialogContent>
    </Dialog>
  )
}

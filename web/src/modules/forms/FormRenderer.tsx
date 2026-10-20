import { useEffect, useRef, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { useUploadMedia } from '@/api/hooks'
import type { FormFieldDef } from '@/api/types'
import { Button } from '@/components/ui/button'
import { Field, Input, Select, Textarea } from '@/components/ui/form-controls'
import { toast } from '@/components/ui/toaster'
import { useApiErrorMessage } from '@/hooks/use-api-error'
import { localized } from '@/lib/utils'

export type Answers = Record<string, unknown>

/** A question is shown only when the answer it depends on matches. Checkboxes compare as "true" or "false". */
export function isVisible(field: FormFieldDef, answers: Answers) {
  if (!field.show_when_field_id) return true
  const value = answers[field.show_when_field_id]
  return (typeof value === 'boolean' ? String(value) : String(value ?? '')) === field.show_when_equals
}

/** The answers that matter: hidden questions are dropped, empty ones left out. */
export function cleanAnswers(fields: FormFieldDef[], answers: Answers): Answers {
  const out: Answers = {}
  for (const f of fields) {
    if (f.type === 'section' || !isVisible(f, answers)) continue
    const v = answers[f.id]
    if (v === undefined || v === null || v === '') continue
    out[f.id] = f.type === 'number' && typeof v === 'string' ? Number(v) : v
  }
  return out
}

/** A small drawing box: the signer draws with a finger or mouse and the picture is kept as the answer. */
function SignaturePad({ id, value, onChange }: { id: string; value: string; onChange: (png: string) => void }) {
  const { t } = useTranslation()
  const canvas = useRef<HTMLCanvasElement>(null)
  const drawing = useRef(false)

  useEffect(() => {
    const c = canvas.current
    const ctx = c?.getContext('2d')
    if (!c || !ctx) return
    ctx.lineWidth = 2
    ctx.lineCap = 'round'
    ctx.strokeStyle = '#0f172a'
  }, [])

  const point = (e: React.PointerEvent<HTMLCanvasElement>) => {
    const rect = e.currentTarget.getBoundingClientRect()
    return { x: ((e.clientX - rect.left) / rect.width) * e.currentTarget.width, y: ((e.clientY - rect.top) / rect.height) * e.currentTarget.height }
  }
  const down = (e: React.PointerEvent<HTMLCanvasElement>) => {
    const ctx = e.currentTarget.getContext('2d')
    if (!ctx) return
    drawing.current = true
    e.currentTarget.setPointerCapture(e.pointerId)
    const p = point(e)
    ctx.beginPath()
    ctx.moveTo(p.x, p.y)
  }
  const move = (e: React.PointerEvent<HTMLCanvasElement>) => {
    if (!drawing.current) return
    const ctx = e.currentTarget.getContext('2d')
    const p = point(e)
    ctx?.lineTo(p.x, p.y)
    ctx?.stroke()
  }
  const up = (e: React.PointerEvent<HTMLCanvasElement>) => {
    if (!drawing.current) return
    drawing.current = false
    onChange(e.currentTarget.toDataURL('image/png'))
  }
  const clear = () => {
    const c = canvas.current
    c?.getContext('2d')?.clearRect(0, 0, c.width, c.height)
    onChange('')
  }

  return (
    <div>
      <canvas
        id={id}
        ref={canvas}
        width={480}
        height={140}
        aria-label={t('forms.fill.signature')}
        className="w-full max-w-md touch-none rounded-md border bg-white"
        onPointerDown={down}
        onPointerMove={move}
        onPointerUp={up}
      />
      <div className="mt-1 flex items-center gap-2 text-xs text-muted-foreground">
        <span>{value ? t('forms.fill.signed') : t('forms.fill.signHere')}</span>
        <Button type="button" size="sm" variant="ghost" onClick={clear}>{t('forms.fill.clear')}</Button>
      </div>
    </div>
  )
}

function FileAnswer({ id, value, onChange }: { id: string; value: string; onChange: (mediaId: string) => void }) {
  const { t } = useTranslation()
  const upload = useUploadMedia()
  const message = useApiErrorMessage()
  const pick = async (file?: File) => {
    if (!file) return
    try {
      onChange((await upload.mutateAsync(file)).id)
    } catch (error) {
      toast.error(message(error))
    }
  }
  return (
    <div className="flex items-center gap-3 text-sm">
      <input id={id} type="file" onChange={(e) => void pick(e.target.files?.[0])} />
      {value && <span className="text-emerald-700">{t('forms.fill.uploaded')}</span>}
    </div>
  )
}

/**
 * Draws a form from its fields and collects the answers. Conditional questions appear and disappear as answers change;
 * `onProgress` is told which question the person last touched, so the centre can see where people give up.
 */
export function FormRenderer({
  fields, onSubmit, onProgress, busy, submitLabel, errors,
}: {
  fields: FormFieldDef[]
  onSubmit: (answers: Answers) => void
  onProgress?: (fieldId: string) => void
  busy?: boolean
  submitLabel: string
  errors?: Record<string, string>
}) {
  const { t, i18n } = useTranslation()
  const [answers, setAnswers] = useState<Answers>({})
  const [missing, setMissing] = useState<string[]>([])
  const set = (id: string, value: unknown) => {
    setAnswers((a) => ({ ...a, [id]: value }))
    onProgress?.(id)
  }
  const text = (d: Record<string, string> | null | undefined) => (d ? localized(d, i18n.language) : '')

  const submit = () => {
    const required = fields.filter((f) => f.type !== 'section' && f.required && isVisible(f, answers))
    const gaps = required.filter((f) => {
      const v = answers[f.id]
      return v === undefined || v === null || v === '' || (f.type === 'checkbox' && v === false)
    }).map((f) => f.id)
    setMissing(gaps)
    if (gaps.length === 0) onSubmit(cleanAnswers(fields, answers))
  }

  return (
    <form onSubmit={(e) => { e.preventDefault(); submit() }} className="space-y-4" noValidate>
      {fields.filter((f) => isVisible(f, answers)).map((f) => {
        if (f.type === 'section') return <h3 key={f.id} className="border-b pb-1 pt-3 font-heading text-lg font-semibold">{text(f.label)}</h3>
        const id = `ff-${f.id}`
        const error = missing.includes(f.id) ? t('validation.required') : errors?.[f.id]
        const value = answers[f.id]
        let control: React.ReactNode
        switch (f.type) {
          case 'textarea':
            control = <Textarea id={id} rows={4} value={(value as string) ?? ''} onChange={(e) => set(f.id, e.target.value)} />
            break
          case 'number':
            control = <Input id={id} inputMode="decimal" dir="ltr" className="text-start" value={(value as string) ?? ''} onChange={(e) => set(f.id, e.target.value)} />
            break
          case 'date':
            control = <Input id={id} type="date" dir="ltr" className="text-start" value={(value as string) ?? ''} onChange={(e) => set(f.id, e.target.value)} />
            break
          case 'dropdown':
            control = (
              <Select id={id} value={(value as string) ?? ''} onChange={(e) => set(f.id, e.target.value)}>
                <option value="">—</option>
                {f.options.map((o) => <option key={o.value} value={o.value}>{text(o.label)}</option>)}
              </Select>
            )
            break
          case 'radio':
            control = (
              <div className="flex flex-wrap gap-4" role="radiogroup" aria-label={text(f.label)}>
                {f.options.map((o) => (
                  <label key={o.value} className="flex items-center gap-1.5 text-sm">
                    <input type="radio" name={id} className="h-4 w-4 accent-primary" checked={value === o.value} onChange={() => set(f.id, o.value)} />
                    {text(o.label)}
                  </label>
                ))}
              </div>
            )
            break
          case 'checkbox':
            control = (
              <label className="flex items-center gap-2 text-sm">
                <input id={id} type="checkbox" className="h-4 w-4 accent-primary" checked={value === true} onChange={(e) => set(f.id, e.target.checked)} />
                {text(f.label)}
              </label>
            )
            break
          case 'file':
            control = <FileAnswer id={id} value={(value as string) ?? ''} onChange={(m) => set(f.id, m)} />
            break
          case 'signature':
            control = <SignaturePad id={id} value={(value as string) ?? ''} onChange={(png) => set(f.id, png)} />
            break
          default:
            control = <Input id={id} value={(value as string) ?? ''} maxLength={500} onChange={(e) => set(f.id, e.target.value)} />
        }
        return (
          <Field key={f.id} label={f.type === 'checkbox' ? '' : text(f.label)} htmlFor={id} required={f.required && f.type !== 'checkbox'} hint={text(f.help)} error={error}>
            {control}
          </Field>
        )
      })}
      <div className="flex justify-end border-t pt-4"><Button type="submit" disabled={busy}>{submitLabel}</Button></div>
    </form>
  )
}

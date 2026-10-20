import { IdCard } from 'lucide-react'
import { useMemo, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { api } from '@/api/client'
import { useIdCardTemplates, useStaffOptions, useStudentOptions } from '@/api/hooks'
import type { IdCardTemplate } from '@/api/types'
import { PageHeader } from '@/components/layout/PageHeader'
import { Button } from '@/components/ui/button'
import { Field, Input, Select } from '@/components/ui/form-controls'
import { Alert, Card, Skeleton } from '@/components/ui/surfaces'
import { toast } from '@/components/ui/toaster'
import { useApiErrorMessage } from '@/hooks/use-api-error'
import { cn, localized } from '@/lib/utils'

const FIELDS = ['name', 'photo', 'dob', 'room', 'program', 'barcode', 'expiry']

function Swatch({ t }: { t: IdCardTemplate }) {
  return (
    <span className="flex h-14 w-10 shrink-0 flex-col overflow-hidden rounded border" aria-hidden="true">
      <span className="h-4" style={{ backgroundColor: t.primary }} />
      <span className="h-1" style={{ backgroundColor: t.accent }} />
      <span className="flex-1" style={{ backgroundColor: t.secondary }} />
    </span>
  )
}

/** Print ID cards for children or staff: pick a design, adjust it, choose people, get one PDF with a card on each page. */
export default function ToolsPage() {
  const { t, i18n } = useTranslation()
  const message = useApiErrorMessage()
  const templates = useIdCardTemplates()
  const [scope, setScope] = useState<'student' | 'staff'>('student')
  const [templateId, setTemplateId] = useState('')
  const [colours, setColours] = useState<{ primary: string; secondary: string; accent: string } | null>(null)
  const [fields, setFields] = useState<string[]>(FIELDS)
  const [size, setSize] = useState('a6')
  const [language, setLanguage] = useState(i18n.language === 'ar' ? 'ar' : 'en')
  const [expiry, setExpiry] = useState('')
  const [picked, setPicked] = useState<string[]>([])
  const [search, setSearch] = useState('')
  const [busy, setBusy] = useState(false)
  const [failure, setFailure] = useState<string | null>(null)
  const students = useStudentOptions(scope === 'student')
  const staff = useStaffOptions(scope === 'staff')

  const list = useMemo(() => {
    const raw = scope === 'student' ? (students.data ?? []).map((s) => ({ id: s.id, name: `${s.first_name} ${s.last_name}` })) : (staff.data ?? []).map((s) => ({ id: s.id, name: `${s.first_name} ${s.last_name}` }))
    const term = search.trim().toLowerCase()
    return term ? raw.filter((p) => p.name.toLowerCase().includes(term)) : raw
  }, [scope, students.data, staff.data, search])
  const options = (templates.data ?? []).filter((x) => x.scope === scope)
  const template = options.find((x) => x.id === templateId) ?? options[0]
  const colour = colours ?? (template ? { primary: template.primary, secondary: template.secondary, accent: template.accent } : null)

  const chooseScope = (next: 'student' | 'staff') => {
    setScope(next)
    setPicked([])
    setTemplateId('')
    setColours(null)
  }
  const chooseTemplate = (x: IdCardTemplate) => {
    setTemplateId(x.id)
    setColours(null)
  }

  const generate = async () => {
    if (!template || !colour) return
    if (picked.length === 0) return setFailure(t('tools.cards.needPeople'))
    setFailure(null)
    setBusy(true)
    try {
      const pdf = await api.postBlob('/tools/id-cards', {
        scope, ids: picked, template: template.id, primary: colour.primary, secondary: colour.secondary, accent: colour.accent,
        fields, expiry: expiry || null, size, language,
      })
      const url = URL.createObjectURL(pdf)
      window.open(url, '_blank', 'noopener')
      window.setTimeout(() => URL.revokeObjectURL(url), 60_000)
      toast.success(t('tools.cards.ready', { count: picked.length }))
    } catch (error) {
      setFailure(message(error))
    } finally {
      setBusy(false)
    }
  }

  return (
    <>
      <PageHeader title={t('tools.title')} subtitle={t('tools.subtitle')} />
      <Card className="space-y-6 p-4">
        <h2 className="flex items-center gap-2 font-heading text-lg font-semibold"><IdCard className="h-5 w-5" aria-hidden="true" />{t('tools.cards.title')}</h2>
        {failure && <Alert>{failure}</Alert>}

        <div className="flex gap-2" role="group" aria-label={t('tools.cards.scope')}>
          {(['student', 'staff'] as const).map((s) => (
            <Button key={s} size="sm" variant={scope === s ? 'default' : 'secondary'} aria-pressed={scope === s} onClick={() => chooseScope(s)}>{t(`tools.cards.scopes.${s}`)}</Button>
          ))}
        </div>

        <section aria-labelledby="tl-design" className="space-y-2">
          <h3 id="tl-design" className="text-sm font-bold">{t('tools.cards.design')}</h3>
          {templates.isLoading ? <Skeleton className="h-20" /> : (
            <ul className="grid gap-2 sm:grid-cols-2 lg:grid-cols-5">
              {options.map((x) => (
                <li key={x.id}>
                  <button
                    type="button"
                    aria-pressed={template?.id === x.id}
                    onClick={() => chooseTemplate(x)}
                    className={cn('flex w-full items-center gap-3 rounded-lg border bg-white p-2 text-start transition-colors hover:bg-slate-50 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring', template?.id === x.id && 'border-primary bg-accent')}
                  >
                    <Swatch t={x} />
                    <span className="text-sm font-semibold">{localized(x.name, i18n.language)}</span>
                  </button>
                </li>
              ))}
            </ul>
          )}
        </section>

        {colour && (
          <section aria-labelledby="tl-colours" className="space-y-2">
            <h3 id="tl-colours" className="text-sm font-bold">{t('tools.cards.colours')}</h3>
            <div className="flex flex-wrap gap-4">
              {(['primary', 'secondary', 'accent'] as const).map((k) => (
                <label key={k} className="flex items-center gap-2 text-sm">
                  <input type="color" value={colour[k]} onChange={(e) => setColours({ ...colour, [k]: e.target.value })} className="h-9 w-12 cursor-pointer rounded border" />
                  {t(`tools.cards.colour.${k}`)}
                </label>
              ))}
            </div>
          </section>
        )}

        <section aria-labelledby="tl-show" className="space-y-2">
          <h3 id="tl-show" className="text-sm font-bold">{t('tools.cards.show')}</h3>
          <div className="flex flex-wrap gap-x-4 gap-y-2">
            {FIELDS.map((f) => (
              <label key={f} className="flex items-center gap-1.5 text-sm">
                <input type="checkbox" className="h-4 w-4 accent-primary" checked={fields.includes(f)} onChange={(e) => setFields(e.target.checked ? [...fields, f] : fields.filter((x) => x !== f))} />
                {t(`tools.cards.fields.${f}`)}
              </label>
            ))}
          </div>
          <div className="grid gap-3 sm:grid-cols-3">
            <Field label={t('tools.cards.size')} htmlFor="tl-size">
              <Select id="tl-size" value={size} onChange={(e) => setSize(e.target.value)}><option value="a6">{t('tools.cards.sizes.a6')}</option><option value="cr80">{t('tools.cards.sizes.cr80')}</option></Select>
            </Field>
            <Field label={t('tools.cards.language')} htmlFor="tl-lang">
              <Select id="tl-lang" value={language} onChange={(e) => setLanguage(e.target.value)}><option value="en">English</option><option value="ar">العربية</option></Select>
            </Field>
            <Field label={t('tools.cards.expiry')} htmlFor="tl-expiry"><Input id="tl-expiry" type="date" dir="ltr" className="text-start" value={expiry} onChange={(e) => setExpiry(e.target.value)} /></Field>
          </div>
        </section>

        <section aria-labelledby="tl-people" className="space-y-2">
          <div className="flex flex-wrap items-center gap-3">
            <h3 id="tl-people" className="text-sm font-bold">{t('tools.cards.people', { count: picked.length })}</h3>
            <Input type="search" className="max-w-56" placeholder={t('common.search')} aria-label={t('common.search')} value={search} onChange={(e) => setSearch(e.target.value)} />
            <Button size="sm" variant="secondary" onClick={() => setPicked(list.map((p) => p.id))}>{t('tools.cards.selectAll')}</Button>
            <Button size="sm" variant="ghost" onClick={() => setPicked([])}>{t('tools.cards.clear')}</Button>
          </div>
          {list.length === 0 ? <p className="text-sm text-muted-foreground">{t('tools.cards.noPeople')}</p> : (
            <ul className="grid max-h-64 gap-1 overflow-y-auto rounded-md border p-2 sm:grid-cols-2 lg:grid-cols-3">
              {list.map((p) => (
                <li key={p.id}>
                  <label className="flex items-center gap-2 rounded px-1.5 py-1 text-sm hover:bg-slate-50">
                    <input type="checkbox" className="h-4 w-4 accent-primary" checked={picked.includes(p.id)} onChange={(e) => setPicked(e.target.checked ? [...picked, p.id] : picked.filter((x) => x !== p.id))} />
                    {p.name}
                  </label>
                </li>
              ))}
            </ul>
          )}
        </section>

        <div className="flex justify-end"><Button onClick={() => void generate()} disabled={busy || !template}>{t('tools.cards.generate')}</Button></div>
      </Card>
    </>
  )
}

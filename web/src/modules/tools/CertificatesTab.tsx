import { Award, Plus, Trash2 } from 'lucide-react'
import { useMemo, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { api } from '@/api/client'
import { flatten, useCertificateHistory, useCertificateTemplates, useStudentOptions } from '@/api/hooks'
import type { CertificateTemplateItem } from '@/api/types'
import { LoadMore } from '@/components/shared/ListState'
import { Button } from '@/components/ui/button'
import { Field, Input, Select, Textarea } from '@/components/ui/form-controls'
import { Alert, Card, Skeleton } from '@/components/ui/surfaces'
import { toast } from '@/components/ui/toaster'
import { useApiErrorMessage } from '@/hooks/use-api-error'
import { formatDate, todayIso } from '@/lib/format'
import { cn, localized } from '@/lib/utils'

function Swatch({ t }: { t: CertificateTemplateItem }) {
  return (
    <span className="flex h-10 w-14 shrink-0 flex-col overflow-hidden rounded border" style={{ borderColor: t.accent }} aria-hidden="true">
      <span className="h-3" style={{ backgroundColor: t.primary }} />
      <span className="flex-1 bg-white" />
      <span className="h-1" style={{ backgroundColor: t.accent }} />
    </span>
  )
}

/** Print certificates for several children: choose a design, adjust the words and signatures, choose children, get one numbered PDF. */
export function CertificatesTab() {
  const { t, i18n } = useTranslation()
  const message = useApiErrorMessage()
  const templates = useCertificateTemplates()
  const history = useCertificateHistory()
  const students = useStudentOptions(true)
  const [templateId, setTemplateId] = useState('')
  const [title, setTitle] = useState('')
  const [body, setBody] = useState('')
  const [date, setDate] = useState(todayIso())
  const [language, setLanguage] = useState(i18n.language === 'ar' ? 'ar' : 'en')
  const [signatures, setSignatures] = useState<{ name: string; title: string }[]>([{ name: '', title: '' }])
  const [picked, setPicked] = useState<string[]>([])
  const [search, setSearch] = useState('')
  const [busy, setBusy] = useState(false)
  const [failure, setFailure] = useState<string | null>(null)
  const list = templates.data ?? []
  const template = list.find((x) => x.id === templateId) ?? list[0]
  const people = useMemo(() => {
    const raw = (students.data ?? []).map((s) => ({ id: s.id, name: `${s.first_name} ${s.last_name}` }))
    const term = search.trim().toLowerCase()
    return term ? raw.filter((p) => p.name.toLowerCase().includes(term)) : raw
  }, [students.data, search])

  const generate = async () => {
    if (!template) return
    if (picked.length === 0) return setFailure(t('tools.cards.needPeople'))
    setFailure(null)
    setBusy(true)
    try {
      const pdf = await api.postBlob('/tools/certificates', {
        template: template.id, student_ids: picked, title: title.trim() || null, body: body.trim() || null, date, language,
        signatures: signatures.filter((s) => s.name.trim()).map((s) => ({ name: s.name.trim(), title: s.title.trim() || null })),
      })
      const url = URL.createObjectURL(pdf)
      window.open(url, '_blank', 'noopener')
      window.setTimeout(() => URL.revokeObjectURL(url), 60_000)
      toast.success(t('tools.certificates.ready', { count: picked.length }))
      void history.refetch()
    } catch (error) {
      setFailure(message(error))
    } finally {
      setBusy(false)
    }
  }

  const issued = flatten(history.data)

  return (
    <div className="space-y-6">
      <Card className="space-y-6 p-4">
        <h2 className="flex items-center gap-2 font-heading text-lg font-semibold"><Award className="h-5 w-5" aria-hidden="true" />{t('tools.certificates.title')}</h2>
        {failure && <Alert>{failure}</Alert>}

        <section aria-labelledby="ct-design" className="space-y-2">
          <h3 id="ct-design" className="text-sm font-bold">{t('tools.cards.design')}</h3>
          {templates.isLoading ? <Skeleton className="h-20" /> : (
            <ul className="grid gap-2 sm:grid-cols-2 lg:grid-cols-5">
              {list.map((x) => (
                <li key={x.id}>
                  <button
                    type="button"
                    aria-pressed={template?.id === x.id}
                    onClick={() => setTemplateId(x.id)}
                    className={cn('flex w-full items-center gap-2 rounded-lg border bg-white p-2 text-start transition-colors hover:bg-slate-50 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring', template?.id === x.id && 'border-primary bg-accent')}
                  >
                    <Swatch t={x} />
                    <span className="text-sm font-semibold">{localized(x.name, i18n.language)}</span>
                  </button>
                </li>
              ))}
            </ul>
          )}
        </section>

        <section aria-labelledby="ct-words" className="space-y-3">
          <h3 id="ct-words" className="text-sm font-bold">{t('tools.certificates.words')}</h3>
          <div className="grid gap-3 sm:grid-cols-3">
            <Field label={t('tools.certificates.titleLabel')} htmlFor="ct-title" hint={template ? localized(template.default_title, language) : undefined}><Input id="ct-title" value={title} maxLength={150} onChange={(e) => setTitle(e.target.value)} /></Field>
            <Field label={t('tools.certificates.date')} htmlFor="ct-date"><Input id="ct-date" type="date" dir="ltr" className="text-start" value={date} onChange={(e) => setDate(e.target.value)} /></Field>
            <Field label={t('tools.cards.language')} htmlFor="ct-lang"><Select id="ct-lang" value={language} onChange={(e) => setLanguage(e.target.value)}><option value="en">English</option><option value="ar">العربية</option></Select></Field>
            <Field label={t('tools.certificates.body')} htmlFor="ct-body" hint={t('tools.certificates.bodyHint')} className="sm:col-span-3"><Textarea id="ct-body" rows={2} maxLength={600} placeholder={template ? localized(template.default_body, language) : ''} value={body} onChange={(e) => setBody(e.target.value)} /></Field>
          </div>
        </section>

        <section aria-labelledby="ct-sign" className="space-y-2">
          <h3 id="ct-sign" className="text-sm font-bold">{t('tools.certificates.signatures')}</h3>
          {signatures.map((s, i) => (
            <div key={i} className="grid gap-2 sm:grid-cols-[1fr_1fr_auto]">
              <Input aria-label={t('tools.certificates.signName')} placeholder={t('tools.certificates.signName')} value={s.name} maxLength={80} onChange={(e) => setSignatures((all) => all.map((x, k) => (k === i ? { ...x, name: e.target.value } : x)))} />
              <Input aria-label={t('tools.certificates.signTitle')} placeholder={t('tools.certificates.signTitle')} value={s.title} maxLength={80} onChange={(e) => setSignatures((all) => all.map((x, k) => (k === i ? { ...x, title: e.target.value } : x)))} />
              <Button type="button" size="sm" variant="ghost" aria-label={t('common.delete')} onClick={() => setSignatures((all) => all.filter((_, k) => k !== i))}><Trash2 className="h-4 w-4" aria-hidden="true" /></Button>
            </div>
          ))}
          {signatures.length < 3 && <Button type="button" size="sm" variant="secondary" onClick={() => setSignatures((all) => [...all, { name: '', title: '' }])}><Plus className="h-4 w-4" aria-hidden="true" />{t('tools.certificates.addSignature')}</Button>}
        </section>

        <section aria-labelledby="ct-people" className="space-y-2">
          <div className="flex flex-wrap items-center gap-3">
            <h3 id="ct-people" className="text-sm font-bold">{t('tools.cards.people', { count: picked.length })}</h3>
            <Input type="search" className="max-w-56" placeholder={t('common.search')} aria-label={t('common.search')} value={search} onChange={(e) => setSearch(e.target.value)} />
            <Button size="sm" variant="secondary" onClick={() => setPicked(people.map((p) => p.id))}>{t('tools.cards.selectAll')}</Button>
            <Button size="sm" variant="ghost" onClick={() => setPicked([])}>{t('tools.cards.clear')}</Button>
          </div>
          <ul className="grid max-h-56 gap-1 overflow-y-auto rounded-md border p-2 sm:grid-cols-2 lg:grid-cols-3">
            {people.map((p) => (
              <li key={p.id}>
                <label className="flex items-center gap-2 rounded px-1.5 py-1 text-sm hover:bg-slate-50">
                  <input type="checkbox" className="h-4 w-4 accent-primary" checked={picked.includes(p.id)} onChange={(e) => setPicked(e.target.checked ? [...picked, p.id] : picked.filter((x) => x !== p.id))} />
                  {p.name}
                </label>
              </li>
            ))}
          </ul>
        </section>

        <div className="flex justify-end"><Button onClick={() => void generate()} disabled={busy || !template}>{t('tools.cards.generate')}</Button></div>
      </Card>

      <Card className="p-4">
        <h2 className="mb-2 font-heading text-lg font-semibold">{t('tools.certificates.history')}</h2>
        {issued.length === 0 ? <p className="text-sm text-muted-foreground">{t('tools.certificates.noHistory')}</p> : (
          <ul className="divide-y text-sm">
            {issued.map((c) => (
              <li key={c.id} className="flex flex-wrap items-center gap-3 py-2">
                <span className="font-mono text-xs font-bold" dir="ltr">{c.number}</span>
                <span className="font-semibold">{c.student_name}</span>
                <span className="text-muted-foreground">{c.title}</span>
                <span className="ms-auto text-xs text-muted-foreground">{formatDate(c.issued_on, i18n.language)}</span>
              </li>
            ))}
          </ul>
        )}
        <LoadMore visible={!!history.hasNextPage} loading={history.isFetchingNextPage} onClick={() => void history.fetchNextPage()} />
      </Card>
    </div>
  )
}

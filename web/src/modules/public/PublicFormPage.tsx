import { useQuery } from '@tanstack/react-query'
import { CheckCircle2 } from 'lucide-react'
import { useEffect, useRef, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { useParams } from 'react-router-dom'
import { api } from '@/api/client'
import type { FillableForm } from '@/api/types'
import { LanguageSwitcher } from '@/components/layout/LanguageSwitcher'
import { Alert, Card, Skeleton } from '@/components/ui/surfaces'
import { serverFieldErrors, useApiErrorMessage } from '@/hooks/use-api-error'
import { localized } from '@/lib/utils'
import { FormRenderer, type Answers } from '../forms/FormRenderer'

/** A form the centre has made public (for example on its website). No sign-in; it cannot be tied to a child. */
export default function PublicFormPage() {
  const { centre = '', id = '' } = useParams()
  const { t, i18n } = useTranslation()
  const message = useApiErrorMessage()
  const base = `/public/forms/${encodeURIComponent(centre)}/${encodeURIComponent(id)}`
  const form = useQuery({ queryKey: ['public-form', centre, id], retry: false, queryFn: () => api.get<FillableForm>(base) })
  const [busy, setBusy] = useState(false)
  const [sent, setSent] = useState(false)
  const [failure, setFailure] = useState<string | null>(null)
  const [errors, setErrors] = useState<Record<string, string>>({})
  const startId = useRef<string | null>(null)
  const started = useRef(false)

  useEffect(() => {
    if (started.current || !form.data) return
    started.current = true
    void api.post<{ start_id: string }>(`${base}/starts`).then((r) => { startId.current = r.start_id }).catch(() => undefined)
  }, [form.data, base])

  const progress = (fieldId: string) => {
    if (startId.current) void api.post(`${base}/starts/${startId.current}/progress`, { field_id: fieldId }).catch(() => undefined)
  }

  const submit = async (answers: Answers) => {
    setBusy(true)
    setFailure(null)
    setErrors({})
    try {
      await api.post(`${base}/submissions`, { answers, start_id: startId.current })
      setSent(true)
    } catch (error) {
      setErrors(Object.fromEntries(Object.keys(serverFieldErrors(error)).filter((k) => k.startsWith('answers.')).map((k) => [k.slice(8), t('forms.fill.fix')])))
      setFailure(message(error))
    } finally {
      setBusy(false)
    }
  }

  return (
    <main className="mx-auto min-h-screen max-w-xl px-4 py-8">
      <div className="mb-4 flex justify-end"><LanguageSwitcher /></div>
      {form.isLoading ? <Skeleton className="h-96" /> : form.isError || !form.data ? (
        <Card className="p-8 text-center"><h1 className="text-xl font-bold">{t('enquiries.public.unavailable')}</h1><p className="mt-2 text-sm text-muted-foreground">{t('enquiries.public.unavailableBody')}</p></Card>
      ) : sent ? (
        <Card className="p-8 text-center">
          <CheckCircle2 className="mx-auto mb-3 h-10 w-10 text-emerald-600" aria-hidden="true" />
          <h1 className="text-xl font-bold">{t('enquiries.public.thanks')}</h1>
          <p className="mt-2 text-sm text-muted-foreground">{t('forms.fill.sentPublic')}</p>
        </Card>
      ) : (
        <Card className="p-6">
          <h1 className="font-heading text-2xl font-bold">{localized(form.data.name, i18n.language)}</h1>
          {form.data.description && <p className="mb-5 mt-1 text-sm text-muted-foreground">{localized(form.data.description, i18n.language)}</p>}
          {failure && <Alert className="mb-4">{failure}</Alert>}
          <FormRenderer fields={form.data.fields} submitLabel={t('forms.fill.submit')} busy={busy} errors={errors} onProgress={progress} onSubmit={(a) => void submit(a)} />
        </Card>
      )}
    </main>
  )
}

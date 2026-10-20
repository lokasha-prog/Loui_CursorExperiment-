import { useQuery } from '@tanstack/react-query'
import { CheckCircle2 } from 'lucide-react'
import { useEffect, useRef, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { useParams } from 'react-router-dom'
import { api } from '@/api/client'
import type { PublicForm } from '@/api/types'
import { LanguageSwitcher } from '@/components/layout/LanguageSwitcher'
import { Button } from '@/components/ui/button'
import { Field, Input, Select, Textarea } from '@/components/ui/form-controls'
import { Alert, Card, Skeleton } from '@/components/ui/surfaces'
import { useApiErrorMessage } from '@/hooks/use-api-error'
import { localized } from '@/lib/utils'

declare global {
  interface Window {
    grecaptcha?: { render: (el: HTMLElement, options: { sitekey: string }) => number; getResponse: (id?: number) => string }
  }
}

/** Loads Google's "I am not a robot" box when the centre has set it up. Without a site key the form relies on the hidden-field and rate-limit guards only. */
function Captcha({ siteKey, onReady }: { siteKey: string; onReady: (read: () => string) => void }) {
  const box = useRef<HTMLDivElement>(null)
  useEffect(() => {
    const draw = () => {
      if (!box.current || !window.grecaptcha || box.current.childElementCount > 0) return
      const id = window.grecaptcha.render(box.current, { sitekey: siteKey })
      onReady(() => window.grecaptcha?.getResponse(id) ?? '')
    }
    if (window.grecaptcha) return draw()
    const script = document.createElement('script')
    script.src = 'https://www.google.com/recaptcha/api.js?render=explicit'
    script.async = true
    script.onload = () => window.setTimeout(draw, 300)
    document.head.appendChild(script)
  }, [siteKey, onReady])
  return <div ref={box} />
}

/** The page a family opens from the nursery's website to ask about a place. No sign-in. */
export default function EnquiryFormPage() {
  const { centre = '' } = useParams()
  const { t, i18n } = useTranslation()
  const message = useApiErrorMessage()
  const form = useQuery({ queryKey: ['public-form', centre], retry: false, queryFn: () => api.get<PublicForm>(`/public/enquiry-form/${encodeURIComponent(centre)}`) })
  const [values, setValues] = useState({ family_name: '', child_name: '', phone: '', email: '', preferred_contact: 'phone', child_date_of_birth: '', desired_start_date: '', message: '', website: '' })
  const [readCaptcha, setReadCaptcha] = useState<(() => string) | null>(null)
  const [busy, setBusy] = useState(false)
  const [sent, setSent] = useState(false)
  const [failure, setFailure] = useState<string | null>(null)
  const set = (patch: Partial<typeof values>) => setValues((v) => ({ ...v, ...patch }))

  const submit = async () => {
    if (!values.family_name.trim() || !values.child_name.trim()) return setFailure(t('validation.required'))
    if (!values.phone.trim() && !values.email.trim()) return setFailure(t('enquiries.form.needContact'))
    setFailure(null)
    setBusy(true)
    try {
      await api.post(`/public/enquiries/${encodeURIComponent(centre)}`, {
        family_name: values.family_name.trim(), child_name: values.child_name.trim(), phone: values.phone.trim() || null, email: values.email.trim() || null,
        preferred_contact: values.preferred_contact, child_date_of_birth: values.child_date_of_birth || null, desired_start_date: values.desired_start_date || null,
        message: values.message.trim() || null, website: values.website, captcha_token: readCaptcha?.() || null,
      })
      setSent(true)
    } catch (error) {
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
          <p className="mt-2 text-sm text-muted-foreground">{t('enquiries.public.thanksBody')}</p>
        </Card>
      ) : (
        <Card className="p-6">
          <h1 className="font-heading text-2xl font-bold">{localized(form.data.name, i18n.language)}</h1>
          <p className="mb-5 mt-1 text-sm text-muted-foreground">{t('enquiries.public.intro')}</p>
          <form onSubmit={(e) => { e.preventDefault(); void submit() }} className="space-y-4" noValidate>
            {failure && <Alert>{failure}</Alert>}
            <div className="grid gap-3 sm:grid-cols-2">
              <Field label={t('enquiries.public.family')} htmlFor="pf-family" required><Input id="pf-family" autoComplete="name" value={values.family_name} onChange={(e) => set({ family_name: e.target.value })} /></Field>
              <Field label={t('enquiries.form.child')} htmlFor="pf-child" required><Input id="pf-child" value={values.child_name} onChange={(e) => set({ child_name: e.target.value })} /></Field>
              <Field label={t('enquiries.form.phone')} htmlFor="pf-phone"><Input id="pf-phone" type="tel" autoComplete="tel" dir="ltr" className="text-start" value={values.phone} onChange={(e) => set({ phone: e.target.value })} /></Field>
              <Field label={t('enquiries.form.email')} htmlFor="pf-email"><Input id="pf-email" type="email" autoComplete="email" dir="ltr" className="text-start" value={values.email} onChange={(e) => set({ email: e.target.value })} /></Field>
              <Field label={t('enquiries.form.preferred')} htmlFor="pf-pref">
                <Select id="pf-pref" value={values.preferred_contact} onChange={(e) => set({ preferred_contact: e.target.value })}>
                  {['phone', 'whatsapp', 'email'].map((c) => <option key={c} value={c}>{t(`enquiries.contact.${c}`)}</option>)}
                </Select>
              </Field>
              <Field label={t('enquiries.form.dob')} htmlFor="pf-dob"><Input id="pf-dob" type="date" dir="ltr" className="text-start" value={values.child_date_of_birth} onChange={(e) => set({ child_date_of_birth: e.target.value })} /></Field>
              <Field label={t('enquiries.form.start')} htmlFor="pf-start" className="sm:col-span-2"><Input id="pf-start" type="date" dir="ltr" className="text-start" value={values.desired_start_date} onChange={(e) => set({ desired_start_date: e.target.value })} /></Field>
              <Field label={t('enquiries.public.message')} htmlFor="pf-msg" className="sm:col-span-2"><Textarea id="pf-msg" rows={4} maxLength={1500} value={values.message} onChange={(e) => set({ message: e.target.value })} /></Field>
            </div>
            {/* Only robots fill this in; people never see it. */}
            <div aria-hidden="true" className="absolute -start-[9999px] h-0 w-0 overflow-hidden">
              <label>Website<input tabIndex={-1} autoComplete="off" value={values.website} onChange={(e) => set({ website: e.target.value })} /></label>
            </div>
            {form.data.captcha_site_key && <Captcha siteKey={form.data.captcha_site_key} onReady={(read) => setReadCaptcha(() => read)} />}
            <Button type="submit" className="w-full" disabled={busy}>{t('enquiries.public.send')}</Button>
          </form>
        </Card>
      )}
    </main>
  )
}

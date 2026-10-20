import { useEffect, useRef, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { api } from '@/api/client'
import { useFillableForm, useStartForm, useStudentOptions, useSubmitForm } from '@/api/hooks'
import { Field, Select } from '@/components/ui/form-controls'
import { Alert, Dialog, DialogContent, DialogHeader, Skeleton } from '@/components/ui/surfaces'
import { toast } from '@/components/ui/toaster'
import { serverFieldErrors, useApiErrorMessage } from '@/hooks/use-api-error'
import { localized } from '@/lib/utils'
import { FormRenderer, type Answers } from './FormRenderer'

/** Fill in a live form, for a child when one is chosen. Opening it counts as a start; the last question touched is reported so the centre sees where people stop. */
export function FillDialog({ formId, studentId, onClose }: { formId: string; studentId: string | null; onClose: () => void }) {
  const { t, i18n } = useTranslation()
  const form = useFillableForm(formId)
  const students = useStudentOptions(true)
  const start = useStartForm()
  const submit = useSubmitForm(formId)
  const message = useApiErrorMessage()
  const [child, setChild] = useState(studentId ?? '')
  const [failure, setFailure] = useState<string | null>(null)
  const [errors, setErrors] = useState<Record<string, string>>({})
  const startId = useRef<string | null>(null)
  const started = useRef(false)

  useEffect(() => {
    if (started.current || !form.data) return
    started.current = true
    start.mutate(formId, { onSuccess: (r) => { startId.current = r.start_id } })
  }, [form.data, formId, start])

  const progress = (fieldId: string) => {
    if (startId.current) void api.post(`/forms/${formId}/starts/${startId.current}/progress`, { field_id: fieldId }).catch(() => undefined)
  }

  const send = async (answers: Answers) => {
    setFailure(null)
    setErrors({})
    try {
      await submit.mutateAsync({ answers, student_id: child || null, start_id: startId.current })
      toast.success(t('forms.fill.sent'))
      onClose()
    } catch (error) {
      setErrors(Object.fromEntries(Object.keys(serverFieldErrors(error)).filter((k) => k.startsWith('answers.')).map((k) => [k.slice(8), t('forms.fill.fix')])))
      setFailure(message(error))
    }
  }

  return (
    <Dialog open onOpenChange={(o) => !o && onClose()}>
      <DialogContent closeLabel={t('common.close')} className="max-w-2xl">
        <DialogHeader title={form.data ? localized(form.data.name, i18n.language) : t('forms.fill.title')} />
        {!form.data ? <Skeleton className="h-48" /> : (
          <div className="space-y-4">
            {failure && <Alert>{failure}</Alert>}
            {form.data.description && <p className="text-sm text-muted-foreground">{localized(form.data.description, i18n.language)}</p>}
            <Field label={t('forms.fill.child')} htmlFor="fill-child">
              <Select id="fill-child" value={child} onChange={(e) => setChild(e.target.value)}>
                <option value="">{t('forms.fill.noChild')}</option>
                {(students.data ?? []).map((s) => <option key={s.id} value={s.id}>{s.first_name} {s.last_name}</option>)}
              </Select>
            </Field>
            <FormRenderer fields={form.data.fields} submitLabel={t('forms.fill.submit')} busy={submit.isPending} errors={errors} onProgress={progress} onSubmit={(a) => void send(a)} />
          </div>
        )}
      </DialogContent>
    </Dialog>
  )
}

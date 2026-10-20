import { MessageCircleHeart } from 'lucide-react'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { useLocation } from 'react-router-dom'
import { useSendFeedback } from '@/api/hooks'
import { Button } from '@/components/ui/button'
import { Field, Select, Textarea } from '@/components/ui/form-controls'
import { Alert, Dialog, DialogContent, DialogHeader } from '@/components/ui/surfaces'
import { toast } from '@/components/ui/toaster'
import { useApiErrorMessage } from '@/hooks/use-api-error'

/** A button on every screen during the pilot: tell the team about a problem, an idea or something you liked. */
export function FeedbackButton() {
  const { t } = useTranslation()
  const location = useLocation()
  const send = useSendFeedback()
  const message = useApiErrorMessage()
  const [open, setOpen] = useState(false)
  const [kind, setKind] = useState('problem')
  const [text, setText] = useState('')
  const [rating, setRating] = useState('')
  const [failure, setFailure] = useState<string | null>(null)

  const submit = async () => {
    if (!text.trim()) return setFailure(t('validation.required'))
    setFailure(null)
    try {
      await send.mutateAsync({ kind, message: text.trim(), page: location.pathname, rating: rating ? Number(rating) : null })
      toast.success(t('feedback.thanks'))
      setText('')
      setRating('')
      setOpen(false)
    } catch (error) {
      setFailure(message(error))
    }
  }

  return (
    <>
      <Button
        variant="secondary"
        size="sm"
        onClick={() => setOpen(true)}
        className="fixed bottom-4 end-4 z-30 rounded-full shadow-md"
      >
        <MessageCircleHeart className="h-4 w-4" aria-hidden="true" />
        {t('feedback.button')}
      </Button>
      <Dialog open={open} onOpenChange={setOpen}>
        <DialogContent closeLabel={t('common.close')} className="max-w-md">
          <DialogHeader title={t('feedback.title')} description={t('feedback.description')} />
          <form onSubmit={(e) => { e.preventDefault(); void submit() }} className="space-y-4" noValidate>
            {failure && <Alert>{failure}</Alert>}
            <Field label={t('feedback.kind')} htmlFor="fb-kind">
              <Select id="fb-kind" value={kind} onChange={(e) => setKind(e.target.value)}>
                <option value="problem">{t('feedback.kinds.problem')}</option>
                <option value="idea">{t('feedback.kinds.idea')}</option>
                <option value="praise">{t('feedback.kinds.praise')}</option>
              </Select>
            </Field>
            <Field label={t('feedback.message')} htmlFor="fb-message" required>
              <Textarea id="fb-message" rows={4} maxLength={2000} value={text} onChange={(e) => setText(e.target.value)} />
            </Field>
            <Field label={t('feedback.rating')} htmlFor="fb-rating">
              <Select id="fb-rating" value={rating} onChange={(e) => setRating(e.target.value)}>
                <option value="">—</option>
                {[1, 2, 3, 4, 5].map((n) => <option key={n} value={n}>{n}</option>)}
              </Select>
            </Field>
            <div className="flex justify-end gap-2 border-t pt-4">
              <Button type="button" variant="secondary" onClick={() => setOpen(false)}>{t('common.cancel')}</Button>
              <Button type="submit" disabled={send.isPending}>{t('feedback.send')}</Button>
            </div>
          </form>
        </DialogContent>
      </Dialog>
    </>
  )
}

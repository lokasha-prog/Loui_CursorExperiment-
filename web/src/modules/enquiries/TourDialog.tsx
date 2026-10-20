import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { useSaveTour, useStaffOptions } from '@/api/hooks'
import type { Tour } from '@/api/types'
import { Button } from '@/components/ui/button'
import { Field, Input, Select, Textarea } from '@/components/ui/form-controls'
import { Alert, Dialog, DialogContent, DialogHeader } from '@/components/ui/surfaces'
import { toast } from '@/components/ui/toaster'
import { useApiErrorMessage } from '@/hooks/use-api-error'

export const TOUR_TYPES = ['general', 'program_focus', 'birthday_party']

/** The browser gives a local date and time without a zone; the server wants an exact moment. */
export const toInstant = (local: string) => new Date(local).toISOString()

export const toLocalInput = (iso: string) => {
  const d = new Date(iso)
  const pad = (n: number) => String(n).padStart(2, '0')
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`
}

/** Book or move a tour for an enquiry. The server refuses times that are blocked off or already taken. */
export function TourDialog({ enquiryId, tour, onClose }: { enquiryId: string; tour: Tour | null; onClose: () => void }) {
  const { t } = useTranslation()
  const staff = useStaffOptions()
  const save = useSaveTour(tour?.id)
  const message = useApiErrorMessage()
  const [start, setStart] = useState(tour ? toLocalInput(tour.starts_at) : '')
  const [duration, setDuration] = useState(String(tour?.duration_minutes ?? 30))
  const [staffId, setStaffId] = useState(tour?.staff_id ?? '')
  const [type, setType] = useState(tour?.tour_type ?? 'general')
  const [adults, setAdults] = useState(String(tour?.adults ?? 1))
  const [children, setChildren] = useState(String(tour?.children ?? 1))
  const [notes, setNotes] = useState(tour?.notes ?? '')
  const [failure, setFailure] = useState<string | null>(null)

  const submit = async () => {
    if (!start) return setFailure(t('validation.required'))
    setFailure(null)
    try {
      await save.mutateAsync({
        enquiry_id: enquiryId, starts_at: toInstant(start), duration_minutes: Number(duration) || 30, staff_id: staffId || null,
        tour_type: type, adults: Number(adults) || 0, children: Number(children) || 0, notes: notes.trim() || null,
      })
      toast.success(t('enquiries.tour.saved'))
      onClose()
    } catch (error) {
      setFailure(message(error))
    }
  }

  return (
    <Dialog open onOpenChange={(o) => !o && onClose()}>
      <DialogContent closeLabel={t('common.close')} className="max-w-lg">
        <DialogHeader title={t(tour ? 'enquiries.tour.edit' : 'enquiries.tour.add')} />
        <form onSubmit={(e) => { e.preventDefault(); void submit() }} className="space-y-4" noValidate>
          {failure && <Alert>{failure}</Alert>}
          <div className="grid gap-3 sm:grid-cols-2">
            <Field label={t('enquiries.tour.when')} htmlFor="tr-when" required><Input id="tr-when" type="datetime-local" dir="ltr" className="text-start" value={start} onChange={(e) => setStart(e.target.value)} /></Field>
            <Field label={t('enquiries.tour.duration')} htmlFor="tr-dur"><Input id="tr-dur" inputMode="numeric" dir="ltr" className="text-start" value={duration} onChange={(e) => setDuration(e.target.value.replace(/\D/g, ''))} /></Field>
            <Field label={t('enquiries.tour.staff')} htmlFor="tr-staff">
              <Select id="tr-staff" value={staffId} onChange={(e) => setStaffId(e.target.value)}>
                <option value="">—</option>
                {(staff.data ?? []).map((s) => <option key={s.id} value={s.id}>{s.first_name} {s.last_name}</option>)}
              </Select>
            </Field>
            <Field label={t('enquiries.tour.type')} htmlFor="tr-type">
              <Select id="tr-type" value={type} onChange={(e) => setType(e.target.value)}>{TOUR_TYPES.map((x) => <option key={x} value={x}>{t(`enquiries.tour.types.${x}`)}</option>)}</Select>
            </Field>
            <Field label={t('enquiries.tour.adults')} htmlFor="tr-adults"><Input id="tr-adults" inputMode="numeric" dir="ltr" className="text-start" value={adults} onChange={(e) => setAdults(e.target.value.replace(/\D/g, ''))} /></Field>
            <Field label={t('enquiries.tour.children')} htmlFor="tr-kids"><Input id="tr-kids" inputMode="numeric" dir="ltr" className="text-start" value={children} onChange={(e) => setChildren(e.target.value.replace(/\D/g, ''))} /></Field>
            <Field label={t('enquiries.tour.notes')} htmlFor="tr-notes" className="sm:col-span-2"><Textarea id="tr-notes" rows={3} value={notes} onChange={(e) => setNotes(e.target.value)} /></Field>
          </div>
          <div className="flex justify-end gap-2 border-t pt-4">
            <Button type="button" variant="secondary" onClick={onClose}>{t('common.cancel')}</Button>
            <Button type="submit" disabled={save.isPending}>{t('enquiries.tour.save')}</Button>
          </div>
        </form>
      </DialogContent>
    </Dialog>
  )
}

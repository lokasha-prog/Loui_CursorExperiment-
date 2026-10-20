import { Archive, CalendarPlus, GraduationCap } from 'lucide-react'
import { useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { useAddActivity, useArchiveEnquiry, useChangeStage, useConvertEnquiry, useEnquiry, useProgramOptions, useRoomOptions, useSaveEnquiry, useStaffOptions } from '@/api/hooks'
import { Button } from '@/components/ui/button'
import { Field, Input, Select, Textarea } from '@/components/ui/form-controls'
import { Alert, Badge, Dialog, DialogContent, DialogHeader, Skeleton } from '@/components/ui/surfaces'
import { toast } from '@/components/ui/toaster'
import { useApiErrorMessage } from '@/hooks/use-api-error'
import { formatDate } from '@/lib/format'
import { localized } from '@/lib/utils'
import { TourDialog } from './TourDialog'

export const STAGES = ['no_stage', 'interested', 'contacted', 'tour_appointment', 'toured', 'enrolled']
export const SOURCES = ['walk_in', 'website', 'referral', 'social_media', 'phone', 'email', 'existing_parent']
const CONTACT = ['phone', 'whatsapp', 'email']
const KINDS = ['call', 'whatsapp', 'email', 'visit', 'note']

/** Everything about one enquiry: the details, who has been in touch, where it is in the pipeline, and the next step (book a tour or enrol). */
export function EnquiryDialog({ id, open, onClose }: { id: string | null; open: boolean; onClose: () => void }) {
  const { t, i18n } = useTranslation()
  const detail = useEnquiry(id)
  const staff = useStaffOptions(open)
  const programs = useProgramOptions(open)
  const rooms = useRoomOptions(open)
  const save = useSaveEnquiry(id ?? undefined)
  const stage = useChangeStage(id ?? '')
  const activity = useAddActivity(id ?? '')
  const archive = useArchiveEnquiry(id ?? '')
  const convert = useConvertEnquiry(id ?? '')
  const message = useApiErrorMessage()
  const [form, setForm] = useState({
    family_name: '', child_name: '', phone: '', email: '', preferred_contact: 'phone', child_date_of_birth: '', desired_start_date: '',
    program_id: '', source: 'walk_in', assigned_staff_id: '', notes: '',
  })
  const [kind, setKind] = useState('call')
  const [summary, setSummary] = useState('')
  const [room, setRoom] = useState('')
  const [booking, setBooking] = useState(false)
  const [failure, setFailure] = useState<string | null>(null)
  const current = detail.data?.enquiry

  useEffect(() => {
    if (!open) return
    setFailure(null)
    setSummary('')
    if (!id) {
      setForm({ family_name: '', child_name: '', phone: '', email: '', preferred_contact: 'phone', child_date_of_birth: '', desired_start_date: '', program_id: '', source: 'walk_in', assigned_staff_id: '', notes: '' })
    } else if (current) {
      setForm({
        family_name: current.family_name, child_name: current.child_name, phone: current.phone ?? '', email: current.email ?? '', preferred_contact: current.preferred_contact,
        child_date_of_birth: current.child_date_of_birth ?? '', desired_start_date: current.desired_start_date ?? '', program_id: current.program_id ?? '',
        source: current.source, assigned_staff_id: current.assigned_staff_id ?? '', notes: current.notes ?? '',
      })
    }
  }, [open, id, current])

  const set = (patch: Partial<typeof form>) => setForm((f) => ({ ...f, ...patch }))
  const run = async (action: () => Promise<unknown>, done?: string) => {
    try {
      await action()
      if (done) toast.success(done)
    } catch (error) {
      toast.error(message(error))
    }
  }

  const submit = async () => {
    if (!form.family_name.trim() || !form.child_name.trim()) return setFailure(t('validation.required'))
    if (!form.phone.trim() && !form.email.trim()) return setFailure(t('enquiries.form.needContact'))
    setFailure(null)
    try {
      await save.mutateAsync({
        family_name: form.family_name.trim(), child_name: form.child_name.trim(), phone: form.phone.trim() || null, email: form.email.trim() || null,
        preferred_contact: form.preferred_contact, child_date_of_birth: form.child_date_of_birth || null, desired_start_date: form.desired_start_date || null,
        program_id: form.program_id || null, source: form.source, assigned_staff_id: form.assigned_staff_id || null, notes: form.notes.trim() || null,
      })
      toast.success(t('enquiries.saved'))
      if (!id) onClose()
    } catch (error) {
      setFailure(message(error))
    }
  }

  const enrolled = current?.stage === 'enrolled'

  return (
    <>
      <Dialog open={open} onOpenChange={(o) => !o && onClose()}>
        <DialogContent closeLabel={t('common.close')} className="max-w-3xl">
          <DialogHeader title={id ? `${current?.family_name ?? ''} · ${current?.child_name ?? ''}` : t('enquiries.add')} />
          {id && detail.isLoading ? <Skeleton className="h-64" /> : (
            <div className="space-y-6">
              <form onSubmit={(e) => { e.preventDefault(); void submit() }} className="space-y-4" noValidate>
                {failure && <Alert>{failure}</Alert>}
                <div className="grid gap-3 sm:grid-cols-2">
                  <Field label={t('enquiries.form.family')} htmlFor="eq-family" required><Input id="eq-family" value={form.family_name} onChange={(e) => set({ family_name: e.target.value })} /></Field>
                  <Field label={t('enquiries.form.child')} htmlFor="eq-child" required><Input id="eq-child" value={form.child_name} onChange={(e) => set({ child_name: e.target.value })} /></Field>
                  <Field label={t('enquiries.form.phone')} htmlFor="eq-phone"><Input id="eq-phone" type="tel" dir="ltr" className="text-start" value={form.phone} onChange={(e) => set({ phone: e.target.value })} /></Field>
                  <Field label={t('enquiries.form.email')} htmlFor="eq-email"><Input id="eq-email" type="email" dir="ltr" className="text-start" value={form.email} onChange={(e) => set({ email: e.target.value })} /></Field>
                  <Field label={t('enquiries.form.preferred')} htmlFor="eq-pref">
                    <Select id="eq-pref" value={form.preferred_contact} onChange={(e) => set({ preferred_contact: e.target.value })}>{CONTACT.map((c) => <option key={c} value={c}>{t(`enquiries.contact.${c}`)}</option>)}</Select>
                  </Field>
                  <Field label={t('enquiries.form.source')} htmlFor="eq-source">
                    <Select id="eq-source" value={form.source} onChange={(e) => set({ source: e.target.value })}>{SOURCES.map((s) => <option key={s} value={s}>{t(`enquiries.sources.${s}`)}</option>)}</Select>
                  </Field>
                  <Field label={t('enquiries.form.dob')} htmlFor="eq-dob"><Input id="eq-dob" type="date" dir="ltr" className="text-start" value={form.child_date_of_birth} onChange={(e) => set({ child_date_of_birth: e.target.value })} /></Field>
                  <Field label={t('enquiries.form.start')} htmlFor="eq-start"><Input id="eq-start" type="date" dir="ltr" className="text-start" value={form.desired_start_date} onChange={(e) => set({ desired_start_date: e.target.value })} /></Field>
                  <Field label={t('enquiries.form.program')} htmlFor="eq-program">
                    <Select id="eq-program" value={form.program_id} onChange={(e) => set({ program_id: e.target.value })}>
                      <option value="">—</option>
                      {(programs.data ?? []).map((p) => <option key={p.id} value={p.id}>{localized(p.name, i18n.language)}</option>)}
                    </Select>
                  </Field>
                  <Field label={t('enquiries.form.staff')} htmlFor="eq-staff">
                    <Select id="eq-staff" value={form.assigned_staff_id} onChange={(e) => set({ assigned_staff_id: e.target.value })}>
                      <option value="">—</option>
                      {(staff.data ?? []).map((s) => <option key={s.id} value={s.id}>{s.first_name} {s.last_name}</option>)}
                    </Select>
                  </Field>
                  <Field label={t('enquiries.form.notes')} htmlFor="eq-notes" className="sm:col-span-2"><Textarea id="eq-notes" rows={3} value={form.notes} onChange={(e) => set({ notes: e.target.value })} /></Field>
                </div>
                <div className="flex justify-end gap-2">
                  <Button type="submit" disabled={save.isPending}>{t('enquiries.form.save')}</Button>
                </div>
              </form>

              {id && current && (
                <>
                  <section aria-labelledby="eq-pipeline" className="space-y-3 border-t pt-4">
                    <h3 id="eq-pipeline" className="text-sm font-bold">{t('enquiries.pipeline')}</h3>
                    <div className="flex flex-wrap items-end gap-3">
                      <Field label={t('enquiries.stage')} htmlFor="eq-stage">
                        <Select id="eq-stage" value={current.stage} disabled={enrolled || stage.isPending} onChange={(e) => void run(() => stage.mutateAsync({ stage: e.target.value }))}>
                          {STAGES.filter((s) => s !== 'enrolled' || enrolled).map((s) => <option key={s} value={s}>{t(`enquiries.stages.${s}`)}</option>)}
                        </Select>
                      </Field>
                      {!enrolled && <Button variant="secondary" onClick={() => setBooking(true)}><CalendarPlus className="h-4 w-4" aria-hidden="true" />{t('enquiries.tour.add')}</Button>}
                      {!enrolled && (
                        <>
                          <Select aria-label={t('enquiries.convertRoom')} value={room} onChange={(e) => setRoom(e.target.value)} className="w-44">
                            <option value="">{t('enquiries.convertRoom')}</option>
                            {(rooms.data ?? []).map((r) => <option key={r.id} value={r.id}>{localized(r.name, i18n.language)}</option>)}
                          </Select>
                          <Button onClick={() => void run(() => convert.mutateAsync({ room_id: room || null, start_date: null }), t('enquiries.converted'))} disabled={convert.isPending}>
                            <GraduationCap className="h-4 w-4" aria-hidden="true" />{t('enquiries.convert')}
                          </Button>
                        </>
                      )}
                      <Button variant="ghost" className="ms-auto" onClick={() => void run(() => archive.mutateAsync(!current.is_archived))}>
                        <Archive className="h-4 w-4" aria-hidden="true" />{current.is_archived ? t('enquiries.unarchive') : t('enquiries.archive')}
                      </Button>
                    </div>
                    <ol className="flex flex-wrap gap-2 text-xs">
                      {detail.data!.history.map((h, i) => (
                        <li key={i} className="rounded-full border px-2.5 py-1">{t(`enquiries.stages.${h.to_stage}`)} · {formatDate(h.at, i18n.language)}{h.reason ? ` — ${h.reason}` : ''}</li>
                      ))}
                    </ol>
                  </section>

                  <section aria-labelledby="eq-log" className="space-y-3 border-t pt-4">
                    <h3 id="eq-log" className="text-sm font-bold">{t('enquiries.log')}</h3>
                    <form className="grid gap-2 sm:grid-cols-[10rem_1fr_auto]" onSubmit={(e) => { e.preventDefault(); if (summary.trim()) void run(async () => { await activity.mutateAsync({ kind, summary: summary.trim() }); setSummary('') }) }}>
                      <Select aria-label={t('enquiries.kind')} value={kind} onChange={(e) => setKind(e.target.value)}>{KINDS.map((k) => <option key={k} value={k}>{t(`enquiries.kinds.${k}`)}</option>)}</Select>
                      <Input aria-label={t('enquiries.summary')} placeholder={t('enquiries.summary')} value={summary} onChange={(e) => setSummary(e.target.value)} maxLength={1000} />
                      <Button type="submit" variant="secondary" disabled={activity.isPending || !summary.trim()}>{t('enquiries.addLog')}</Button>
                    </form>
                    {detail.data!.activities.length === 0 ? <p className="text-sm text-muted-foreground">{t('enquiries.logEmpty')}</p> : (
                      <ul className="divide-y text-sm">
                        {detail.data!.activities.map((a) => (
                          <li key={a.id} className="flex flex-wrap items-baseline gap-2 py-2"><Badge tone="info">{t(`enquiries.kinds.${a.kind}`)}</Badge><span>{a.summary}</span><span className="ms-auto text-xs text-muted-foreground">{formatDate(a.at, i18n.language)}</span></li>
                        ))}
                      </ul>
                    )}
                  </section>
                </>
              )}
            </div>
          )}
        </DialogContent>
      </Dialog>
      {booking && id && <TourDialog enquiryId={id} tour={null} onClose={() => setBooking(false)} />}
    </>
  )
}

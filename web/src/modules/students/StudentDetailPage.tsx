import { zodResolver } from '@hookform/resolvers/zod'
import { ArrowLeft, IdCard, Lock, Mail, Pencil, Phone, Plus, Trash2, UserCheck } from 'lucide-react'
import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { useTranslation } from 'react-i18next'
import { Link, useParams } from 'react-router-dom'
import { z } from 'zod'
import { useAddGuardian, useGuardians, useRemoveGuardian, useRoomOptions, useStudent, useTransitionStudent } from '@/api/hooks'
import { ApiError } from '@/api/client'
import { ProtectedMedia } from '@/components/shared/ProtectedMedia'
import { StatusBadge } from '@/components/shared/StatusBadge'
import { StudentDialog } from './StudentForm'
import { countryName } from '@/lib/countries'
import { Button } from '@/components/ui/button'
import { Field, Input, Select } from '@/components/ui/form-controls'
import { Alert, Avatar, Badge, Card, Dialog, DialogContent, DialogHeader, EmptyState, Skeleton } from '@/components/ui/surfaces'
import { toast } from '@/components/ui/toaster'
import { useApiErrorMessage } from '@/hooks/use-api-error'
import { ageLabel, emptyToNull, formatDate } from '@/lib/format'
import { initials, localized } from '@/lib/utils'
import { can, useAuth } from '@/stores/auth'
import { CreateUserDialog, type UserPrefill } from '@/modules/users/UserForm'

const TRANSITIONS: Record<string, string[]> = {
  signed_up: ['active', 'withdrawn'],
  active: ['on_hold', 'inactive', 'withdrawn', 'transferred'],
  on_hold: ['active', 'inactive', 'withdrawn'],
  inactive: ['active', 'withdrawn'],
  withdrawn: [],
  transferred: [],
}

const RELATIONSHIPS = ['mother', 'father', 'grandparent', 'legal_guardian', 'other'] as const

const guardianSchema = z.object({
  firstName: z.string().trim().min(1, 'validation.required'),
  lastName: z.string().trim().min(1, 'validation.required'),
  email: z.string().trim().min(1, 'validation.required').email('validation.email'),
  phone: z.string().optional(),
  emiratesId: z.string().optional(),
  relationship: z.string(),
  isPrimary: z.boolean(),
  canPickup: z.boolean(),
})

type GuardianValues = z.infer<typeof guardianSchema>

function Detail({ label, value }: { label: string; value?: string | null }) {
  const { t } = useTranslation()
  return (
    <div>
      <dt className="text-[11px] font-bold uppercase tracking-wide text-muted-foreground">{label}</dt>
      <dd className="mt-0.5 whitespace-pre-line text-sm">{value || <span className="text-muted-foreground">{t('students.detail.notRecorded')}</span>}</dd>
    </div>
  )
}

function AddGuardianDialog({ studentId, open, onOpenChange }: { studentId: string; open: boolean; onOpenChange: (open: boolean) => void }) {
  const { t } = useTranslation()
  const add = useAddGuardian(studentId)
  const message = useApiErrorMessage()
  const [failure, setFailure] = useState<string | null>(null)
  const {
    register,
    handleSubmit,
    reset,
    formState: { errors, isSubmitting },
  } = useForm<GuardianValues>({
    resolver: zodResolver(guardianSchema),
    defaultValues: { relationship: 'mother', isPrimary: false, canPickup: true },
  })

  const onSubmit = handleSubmit(async (values) => {
    setFailure(null)
    try {
      await add.mutateAsync({
        first_name: values.firstName.trim(),
        last_name: values.lastName.trim(),
        email: values.email.trim(),
        phone: emptyToNull(values.phone),
        emirates_id: emptyToNull(values.emiratesId),
        relationship: values.relationship,
        is_primary: values.isPrimary,
        can_pickup: values.canPickup,
        receives_communications: true,
      })
      toast.success(t('students.guardians.added'))
      reset()
      onOpenChange(false)
    } catch (error) {
      setFailure(error instanceof ApiError && error.status === 409 ? t('errors.DUPLICATE_RESOURCE') : message(error))
    }
  })

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent closeLabel={t('common.close')} className="max-w-lg">
        <DialogHeader title={t('students.guardians.add')} />
        <form onSubmit={onSubmit} noValidate className="space-y-3">
          {failure && <Alert>{failure}</Alert>}
          <div className="grid gap-3 sm:grid-cols-2">
            <Field label={t('students.form.guardianFirst')} htmlFor="g-first" required error={errors.firstName && t(errors.firstName.message!)}>
              <Input id="g-first" aria-invalid={!!errors.firstName} {...register('firstName')} />
            </Field>
            <Field label={t('students.form.guardianLast')} htmlFor="g-last" required error={errors.lastName && t(errors.lastName.message!)}>
              <Input id="g-last" aria-invalid={!!errors.lastName} {...register('lastName')} />
            </Field>
            <Field label={t('students.form.guardianEmail')} htmlFor="g-email" required error={errors.email && t(errors.email.message!)}>
              <Input id="g-email" type="email" dir="ltr" className="text-start" aria-invalid={!!errors.email} {...register('email')} />
            </Field>
            <Field label={t('students.form.guardianPhone')} htmlFor="g-phone">
              <Input id="g-phone" type="tel" dir="ltr" className="text-start" {...register('phone')} />
            </Field>
            <Field label={t('students.form.guardianIdNumber')} htmlFor="g-id">
              <Input id="g-id" dir="ltr" className="text-start" maxLength={30} autoComplete="off" {...register('emiratesId')} />
            </Field>
            <Field label={t('students.form.relationship')} htmlFor="g-rel">
              <Select id="g-rel" {...register('relationship')}>
                {RELATIONSHIPS.map((r) => (
                  <option key={r} value={r}>{t(`students.guardians.relationship.${r}`)}</option>
                ))}
              </Select>
            </Field>
          </div>
          <label className="flex items-center gap-2 text-sm">
            <input type="checkbox" className="h-4 w-4 accent-[hsl(var(--primary))]" {...register('isPrimary')} />
            {t('students.guardians.primary')}
          </label>
          <label className="flex items-center gap-2 text-sm">
            <input type="checkbox" className="h-4 w-4 accent-[hsl(var(--primary))]" {...register('canPickup')} />
            {t('students.guardians.canPickup')}
          </label>
          <div className="flex justify-end gap-2 border-t pt-4">
            <Button type="button" variant="secondary" onClick={() => onOpenChange(false)}>{t('common.cancel')}</Button>
            <Button type="submit" disabled={isSubmitting}>{isSubmitting ? t('common.saving') : t('common.add')}</Button>
          </div>
        </form>
      </DialogContent>
    </Dialog>
  )
}

function StatusChanger({ studentId, current }: { studentId: string; current: string }) {
  const { t } = useTranslation()
  const transition = useTransitionStudent(studentId)
  const message = useApiErrorMessage()
  const options = TRANSITIONS[current] ?? []
  const [next, setNext] = useState('')
  const [reason, setReason] = useState('')
  const [failure, setFailure] = useState<string | null>(null)
  if (options.length === 0) return null

  const apply = async () => {
    setFailure(null)
    try {
      await transition.mutateAsync({ status: next, reason: reason.trim() || undefined })
      toast.success(t('students.detail.statusChanged'))
      setNext('')
      setReason('')
    } catch (error) {
      setFailure(message(error))
    }
  }

  return (
    <Card className="p-5">
      <h2 className="mb-3 font-heading text-lg font-semibold">{t('students.detail.changeStatus')}</h2>
      {failure && <Alert className="mb-3">{failure}</Alert>}
      <div className="grid gap-3 sm:grid-cols-[200px_1fr_auto] sm:items-end">
        <Field label={t('students.detail.newStatus')} htmlFor="new-status">
          <Select id="new-status" value={next} onChange={(e) => setNext(e.target.value)}>
            <option value="">—</option>
            {options.map((s) => (
              <option key={s} value={s}>{t(`students.status.${s}`)}</option>
            ))}
          </Select>
        </Field>
        <Field label={t('students.detail.reason')} htmlFor="status-reason">
          <Input id="status-reason" value={reason} maxLength={500} onChange={(e) => setReason(e.target.value)} />
        </Field>
        <Button onClick={() => void apply()} disabled={!next || transition.isPending}>
          {t('students.detail.apply')}
        </Button>
      </div>
    </Card>
  )
}

export default function StudentDetailPage() {
  const { id = '' } = useParams()
  const { t, i18n } = useTranslation()
  const me = useAuth((s) => s.me)
  const canWrite = can(me, 'students.write')
  const student = useStudent(id)
  const guardians = useGuardians(id)
  const removeGuardian = useRemoveGuardian(id)
  const rooms = useRoomOptions(can(me, 'centre_structure.read'))
  const [guardianOpen, setGuardianOpen] = useState(false)
  const [editOpen, setEditOpen] = useState(false)
  const [signIn, setSignIn] = useState<UserPrefill | null>(null)
  const canManageUsers = can(me, 'user_management.manage')
  const message = useApiErrorMessage()

  const back = (
    <Link to="/students" className="mb-4 inline-flex items-center gap-1.5 text-sm font-semibold text-muted-foreground hover:text-foreground">
      <ArrowLeft className="h-4 w-4 rtl:rotate-180" aria-hidden="true" />
      {t('students.detail.backToList')}
    </Link>
  )

  if (student.isLoading) return <>{back}<Skeleton className="h-64" /></>
  if (student.isError || !student.data) {
    return (
      <>
        {back}
        <Card><EmptyState icon={<Lock className="h-7 w-7" aria-hidden="true" />} title={t('students.detail.notFound')} description={student.error ? message(student.error) : undefined} /></Card>
      </>
    )
  }

  const s = student.data
  const arabicName = [s.first_name_arabic, s.last_name_arabic].filter(Boolean).join(' ')
  const room = localized(rooms.data?.find((r) => r.id === s.room_id)?.name, i18n.language)

  const remove = async (guardianId: string) => {
    if (!window.confirm(t('students.guardians.confirmRemove'))) return
    try {
      await removeGuardian.mutateAsync(guardianId)
      toast.success(t('students.guardians.removed'))
    } catch (error) {
      toast.error(message(error))
    }
  }

  return (
    <>
      {back}
      <Card className="mb-6 flex flex-wrap items-center gap-5 p-6">
        {s.photo_media_id ? (
          <ProtectedMedia id={s.photo_media_id} contentType="image/*" alt={`${s.first_name} ${s.last_name}`} className="h-16 w-16 rounded-full object-cover" />
        ) : (
          <Avatar text={initials(s.first_name, s.last_name)} className="h-16 w-16 text-xl" />
        )}
        <div className="min-w-0 flex-1">
          <h1 className="text-2xl font-bold">{s.first_name} {s.last_name}</h1>
          {arabicName && <p lang="ar" dir="rtl" className="text-lg text-muted-foreground">{arabicName}</p>}
          <div className="mt-2 flex flex-wrap items-center gap-2">
            <StatusBadge status={s.status} labelKey="students.status" />
            <span className="text-sm text-muted-foreground">{ageLabel(s.date_of_birth, t)}</span>
            {room && <Badge tone="info">{room}</Badge>}
            <span className="text-xs text-muted-foreground">{t('students.detail.sinceStatus', { date: formatDate(s.status_changed_at, i18n.language) })}</span>
          </div>
        </div>
        <div className="flex flex-col items-end gap-2">
          <div className="font-mono text-sm" dir="ltr">{s.barcode}</div>
          {canWrite && (
            <Button size="sm" variant="secondary" onClick={() => setEditOpen(true)}>
              <Pencil className="h-3.5 w-3.5" aria-hidden="true" />
              {t('common.edit')}
            </Button>
          )}
        </div>
      </Card>

      <div className="grid gap-6 lg:grid-cols-3">
        <div className="space-y-6 lg:col-span-2">
          <Card className="p-5">
            <h2 className="mb-4 font-heading text-lg font-semibold">{t('students.detail.overview')}</h2>
            <dl className="grid gap-4 sm:grid-cols-2">
              <Detail label={t('students.detail.dateOfBirth')} value={formatDate(s.date_of_birth, i18n.language)} />
              <Detail label={t('students.form.gender')} value={s.gender ? t(`students.gender.${s.gender}`) : null} />
              <Detail label={t('students.detail.nationality')} value={countryName(s.nationality, i18n.language)} />
              <Detail label={t('students.detail.language')} value={s.primary_language} />
              <Detail label={t('students.detail.enrolled')} value={formatDate(s.start_date, i18n.language)} />
              <Detail label={t('students.detail.ended')} value={formatDate(s.end_date, i18n.language)} />
            </dl>
          </Card>

          <Card className="p-5">
            <div className="mb-1 flex items-center justify-between">
              <h2 className="font-heading text-lg font-semibold">{t('students.detail.health')}</h2>
              <span className="inline-flex items-center gap-1 text-[11px] text-muted-foreground">
                <Lock className="h-3 w-3" aria-hidden="true" />
                {t('students.detail.protected')}
              </span>
            </div>
            <p className="mb-4 text-xs text-muted-foreground">{t('students.detail.protectedNote')}</p>
            <dl className="grid gap-4 sm:grid-cols-2">
              <Detail label={t('students.detail.allergyInfo')} value={s.allergy_info} />
              <Detail label={t('students.detail.medicalNotes')} value={s.medical_notes} />
              <Detail label={t('students.detail.dietary')} value={s.dietary_requirements} />
              {canWrite && <Detail label={t('students.detail.custodyNotes')} value={s.custody_notes} />}
              {canWrite && <Detail label={t('students.detail.passport')} value={s.passport_number} />}
              {canWrite && <Detail label={t('students.detail.emiratesId')} value={s.emirates_id} />}
            </dl>
            {canWrite && s.id_photo_media_id && (
              <div className="mt-4">
                <p className="mb-1 flex items-center gap-1 text-[11px] font-bold uppercase tracking-wide text-muted-foreground"><IdCard className="h-3 w-3" aria-hidden="true" />{t('students.form.idPhoto')}</p>
                <ProtectedMedia id={s.id_photo_media_id} contentType="image/*" alt={t('students.form.idPhoto')} className="max-h-48 w-auto" />
              </div>
            )}
          </Card>

          {canWrite && <StatusChanger studentId={s.id} current={s.status} />}
        </div>

        <Card className="h-fit p-5">
          <div className="mb-4 flex items-center justify-between">
            <h2 className="font-heading text-lg font-semibold">{t('students.detail.guardians')}</h2>
            {canWrite && (
              <Button size="sm" variant="secondary" onClick={() => setGuardianOpen(true)}>
                <Plus className="h-3.5 w-3.5" aria-hidden="true" />
                {t('common.add')}
              </Button>
            )}
          </div>
          {guardians.isLoading ? (
            <Skeleton className="h-24" />
          ) : (guardians.data?.length ?? 0) === 0 ? (
            <p className="text-sm text-muted-foreground">{t('students.guardians.empty')}</p>
          ) : (
            <ul className="space-y-4">
              {guardians.data!.map((g) => (
                <li key={g.id} className="rounded-md border p-3">
                  <div className="flex items-start justify-between gap-2">
                    <div>
                      <p className="font-bold">{g.first_name} {g.last_name}</p>
                      <p className="text-xs text-muted-foreground">{t(`students.guardians.relationship.${g.relationship}`)}</p>
                    </div>
                    <div className="flex flex-wrap justify-end gap-1">
                      {g.is_primary && <Badge tone="success">{t('students.guardians.primary')}</Badge>}
                      {g.has_login && (
                        <Badge tone="info">
                          <UserCheck className="me-1 h-3 w-3" aria-hidden="true" />
                          {t('students.guardians.hasLogin')}
                        </Badge>
                      )}
                    </div>
                  </div>
                  <p className="mt-2 flex items-center gap-1.5 text-xs" dir="ltr"><Mail className="h-3 w-3 text-muted-foreground" aria-hidden="true" />{g.email}</p>
                  {g.phone && <p className="mt-1 flex items-center gap-1.5 text-xs" dir="ltr"><Phone className="h-3 w-3 text-muted-foreground" aria-hidden="true" />{g.phone}</p>}
                  {g.emirates_id && <p className="mt-1 flex items-center gap-1.5 text-xs" dir="ltr"><IdCard className="h-3 w-3 text-muted-foreground" aria-hidden="true" />{g.emirates_id}</p>}
                  <div className="mt-2 flex items-center justify-between">
                    <span className="text-[11px] text-muted-foreground">{g.can_pickup ? t('students.guardians.canPickup') : t('students.guardians.noPickup')}</span>
                    {canManageUsers && !g.has_login && (
                      <Button
                        variant="secondary"
                        size="sm"
                        onClick={() => setSignIn({ firstName: g.first_name, lastName: g.last_name, email: g.email, role: 'parent', guardianId: g.id })}
                      >
                        {t('students.guardians.createSignIn')}
                      </Button>
                    )}
                    {canWrite && (
                      <Button variant="ghost" size="sm" onClick={() => void remove(g.id)} aria-label={`${t('students.guardians.remove')}: ${g.first_name} ${g.last_name}`}>
                        <Trash2 className="h-3.5 w-3.5" aria-hidden="true" />
                      </Button>
                    )}
                  </div>
                </li>
              ))}
            </ul>
          )}
        </Card>
      </div>

      {canWrite && <AddGuardianDialog studentId={s.id} open={guardianOpen} onOpenChange={setGuardianOpen} />}
      {canWrite && <StudentDialog open={editOpen} onOpenChange={setEditOpen} student={s} />}
      {canManageUsers && (
        <CreateUserDialog
          open={!!signIn}
          onOpenChange={(o) => {
            if (!o) {
              setSignIn(null)
              void guardians.refetch()
            }
          }}
          prefill={signIn ?? undefined}
        />
      )}
    </>
  )
}

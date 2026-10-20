import { zodResolver } from '@hookform/resolvers/zod'
import { useEffect, useMemo, useState } from 'react'
import { useForm } from 'react-hook-form'
import { useTranslation } from 'react-i18next'
import { z } from 'zod'
import { useCreateStudent, useProgramOptions, useRoomOptions, useUpdateStudent } from '@/api/hooks'
import type { CreateStudentRequest, StudentDetail } from '@/api/types'
import { PhotoField } from '@/components/shared/PhotoField'
import { Button } from '@/components/ui/button'
import { Field, Input, Select, Textarea } from '@/components/ui/form-controls'
import { Alert, Dialog, DialogContent, DialogHeader } from '@/components/ui/surfaces'
import { toast } from '@/components/ui/toaster'
import { useApiErrorMessage } from '@/hooks/use-api-error'
import { countryList } from '@/lib/countries'
import { ageInMonths, emptyToNull, todayIso } from '@/lib/format'
import { localized } from '@/lib/utils'

const RELATIONSHIPS = ['mother', 'father', 'grandparent', 'legal_guardian', 'other'] as const
const GENDERS = ['male', 'female'] as const

export const studentSchema = z
  .object({
    firstName: z.string().trim().min(1, 'validation.required'),
    lastName: z.string().trim().min(1, 'validation.required'),
    firstNameAr: z.string().optional(),
    lastNameAr: z.string().optional(),
    dateOfBirth: z
      .string()
      .min(1, 'validation.required')
      .refine((value) => value <= todayIso(), 'validation.futureDate'),
    gender: z.string().optional(),
    nationality: z.string().optional(),
    primaryLanguage: z.string().optional(),
    emiratesId: z.string().optional(),
    photoId: z.string().nullable().optional(),
    idPhotoId: z.string().nullable().optional(),
    roomId: z.string().optional(),
    programId: z.string().optional(),
    medicalNotes: z.string().optional(),
    allergyInfo: z.string().optional(),
    dietaryRequirements: z.string().optional(),
    guardianFirst: z.string().optional(),
    guardianLast: z.string().optional(),
    guardianEmail: z.string().optional(),
    guardianPhone: z.string().optional(),
    guardianEmiratesId: z.string().optional(),
    guardianRelationship: z.string().optional(),
  })
  .superRefine((values, context) => {
    // The guardian block is optional, but once started it must be complete.
    const started = [values.guardianFirst, values.guardianLast, values.guardianEmail].some((v) => v?.trim())
    if (!started) return
    for (const field of ['guardianFirst', 'guardianLast', 'guardianEmail'] as const) {
      if (!values[field]?.trim()) context.addIssue({ code: 'custom', path: [field], message: 'validation.required' })
    }
    if (values.guardianEmail?.trim() && !/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(values.guardianEmail.trim())) {
      context.addIssue({ code: 'custom', path: ['guardianEmail'], message: 'validation.email' })
    }
  })

export type StudentFormValues = z.infer<typeof studentSchema>

type Fields = CreateStudentRequest['student']

/** The child's details as the server wants them. When editing, details this form does not show are kept as they were. */
export function toFields(values: StudentFormValues, base?: StudentDetail): Fields {
  return {
    first_name: values.firstName.trim(),
    last_name: values.lastName.trim(),
    first_name_arabic: emptyToNull(values.firstNameAr),
    last_name_arabic: emptyToNull(values.lastNameAr),
    date_of_birth: values.dateOfBirth,
    gender: emptyToNull(values.gender),
    room_id: emptyToNull(values.roomId),
    group_id: base?.group_id ?? null,
    program_id: emptyToNull(values.programId),
    key_person_staff_id: base?.key_person_staff_id ?? null,
    start_date: base?.start_date ?? null,
    nationality: emptyToNull(values.nationality),
    religion: base?.religion ?? null,
    primary_language: emptyToNull(values.primaryLanguage),
    medical_notes: emptyToNull(values.medicalNotes),
    allergy_info: emptyToNull(values.allergyInfo),
    dietary_requirements: emptyToNull(values.dietaryRequirements),
    custody_notes: base?.custody_notes ?? null,
    passport_number: base?.passport_number ?? null,
    emirates_id: emptyToNull(values.emiratesId),
    label_ids: base?.label_ids ?? null,
    photo_media_id: values.photoId ?? null,
    id_photo_media_id: values.idPhotoId ?? null,
  }
}

export function toCreateRequest(values: StudentFormValues): CreateStudentRequest {
  const hasGuardian = !!values.guardianEmail?.trim()
  return {
    student: toFields(values),
    guardians: hasGuardian
      ? [
          {
            first_name: values.guardianFirst!.trim(),
            last_name: values.guardianLast!.trim(),
            email: values.guardianEmail!.trim(),
            phone: emptyToNull(values.guardianPhone),
            relationship: values.guardianRelationship || 'mother',
            is_primary: true,
            emirates_id: emptyToNull(values.guardianEmiratesId),
          },
        ]
      : null,
  }
}

function valuesFrom(student?: StudentDetail): Partial<StudentFormValues> {
  if (!student) return { guardianRelationship: 'mother' }
  return {
    firstName: student.first_name,
    lastName: student.last_name,
    firstNameAr: student.first_name_arabic ?? '',
    lastNameAr: student.last_name_arabic ?? '',
    dateOfBirth: student.date_of_birth,
    gender: student.gender === 'male' || student.gender === 'female' ? student.gender : '',
    nationality: student.nationality ?? '',
    primaryLanguage: student.primary_language ?? '',
    emiratesId: student.emirates_id ?? '',
    photoId: student.photo_media_id,
    idPhotoId: student.id_photo_media_id,
    roomId: student.room_id ?? '',
    programId: student.program_id ?? '',
    medicalNotes: student.medical_notes ?? '',
    allergyInfo: student.allergy_info ?? '',
    dietaryRequirements: student.dietary_requirements ?? '',
  }
}

/** Adds a child, or (when a student is passed) edits one. Rooms and programmes offered depend on the child's age. */
export function StudentDialog({ open, onOpenChange, student }: { open: boolean; onOpenChange: (open: boolean) => void; student?: StudentDetail }) {
  const { t, i18n } = useTranslation()
  const editing = !!student
  const create = useCreateStudent()
  const update = useUpdateStudent(student?.id ?? '')
  const rooms = useRoomOptions(open)
  const programs = useProgramOptions(open)
  const message = useApiErrorMessage()
  const [failure, setFailure] = useState<string | null>(null)
  const {
    register,
    handleSubmit,
    reset,
    watch,
    setValue,
    formState: { errors, isSubmitting },
  } = useForm<StudentFormValues>({ resolver: zodResolver(studentSchema), defaultValues: valuesFrom(student) })

  useEffect(() => {
    if (open) {
      reset(valuesFrom(student))
      setFailure(null)
    }
  }, [open, student, reset])

  const dob = watch('dateOfBirth')
  const roomId = watch('roomId')
  const programId = watch('programId')
  const validDob = !!dob && dob <= todayIso()
  const age = validDob ? ageInMonths(dob) : null
  const countries = useMemo(() => countryList(i18n.language), [i18n.language])

  const fittingRooms = useMemo(
    () => (age === null ? [] : (rooms.data ?? []).filter((r) => r.is_active && age >= r.min_age_months && age <= r.max_age_months)),
    [rooms.data, age],
  )
  const fittingPrograms = useMemo(
    () => (age === null ? [] : (programs.data ?? []).filter((p) => p.is_active && age >= p.min_age_months && age <= p.max_age_months)),
    [programs.data, age],
  )

  // Keep the choices honest: drop a room or programme that no longer fits, and pre-select the programme when only one fits.
  useEffect(() => {
    if (!rooms.data || !programs.data) return
    if (roomId && !fittingRooms.some((r) => r.id === roomId)) setValue('roomId', '')
    if (programId && !fittingPrograms.some((p) => p.id === programId)) setValue('programId', '')
    else if (!programId && fittingPrograms.length === 1) setValue('programId', fittingPrograms[0].id)
  }, [fittingRooms, fittingPrograms, roomId, programId, rooms.data, programs.data, setValue])

  const err = (key?: string) => (key ? t(key) : undefined)

  const onSubmit = handleSubmit(async (values) => {
    setFailure(null)
    try {
      if (student) {
        await update.mutateAsync({ student: toFields(values, student) })
        toast.success(t('students.form.updated'))
      } else {
        await create.mutateAsync(toCreateRequest(values))
        toast.success(t('students.form.created'))
      }
      onOpenChange(false)
    } catch (error) {
      setFailure(message(error))
    }
  })

  const needDob = age === null
  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent closeLabel={t('common.close')}>
        <DialogHeader
          title={editing ? t('students.form.editTitle') : t('students.form.createTitle')}
          description={editing ? t('students.form.editDescription') : t('students.form.createDescription')}
        />
        <form onSubmit={onSubmit} noValidate className="space-y-6">
          {failure && <Alert>{failure}</Alert>}

          <fieldset className="space-y-3">
            <legend className="mb-2 text-[11px] font-bold uppercase tracking-wide text-muted-foreground">{t('students.form.sectionChild')}</legend>
            <div className="grid gap-4 sm:grid-cols-2">
              <PhotoField label={t('students.form.photo')} hint={t('students.form.photoHint')} value={watch('photoId') ?? null} onChange={(id) => setValue('photoId', id, { shouldDirty: true })} />
              <PhotoField label={t('students.form.idPhoto')} hint={t('students.form.idPhotoHint')} value={watch('idPhotoId') ?? null} onChange={(id) => setValue('idPhotoId', id, { shouldDirty: true })} />
            </div>
            <div className="grid gap-3 sm:grid-cols-2">
              <Field label={t('students.form.firstName')} htmlFor="firstName" required error={err(errors.firstName?.message)}>
                <Input id="firstName" aria-invalid={!!errors.firstName} {...register('firstName')} />
              </Field>
              <Field label={t('students.form.lastName')} htmlFor="lastName" required error={err(errors.lastName?.message)}>
                <Input id="lastName" aria-invalid={!!errors.lastName} {...register('lastName')} />
              </Field>
              <Field label={t('students.form.firstNameAr')} htmlFor="firstNameAr">
                <Input id="firstNameAr" dir="rtl" lang="ar" {...register('firstNameAr')} />
              </Field>
              <Field label={t('students.form.lastNameAr')} htmlFor="lastNameAr">
                <Input id="lastNameAr" dir="rtl" lang="ar" {...register('lastNameAr')} />
              </Field>
              <Field label={t('students.form.dateOfBirth')} htmlFor="dateOfBirth" required error={err(errors.dateOfBirth?.message)}>
                <Input id="dateOfBirth" type="date" max={todayIso()} dir="ltr" className="text-start" aria-invalid={!!errors.dateOfBirth} {...register('dateOfBirth')} />
              </Field>
              <Field label={t('students.form.gender')} htmlFor="gender">
                <Select id="gender" {...register('gender')}>
                  <option value="">—</option>
                  {GENDERS.map((g) => (
                    <option key={g} value={g}>{t(`students.gender.${g}`)}</option>
                  ))}
                </Select>
              </Field>
              <Field label={t('students.form.nationality')} htmlFor="nationality">
                <Select id="nationality" {...register('nationality')}>
                  <option value="">—</option>
                  {countries.map((c) => (
                    <option key={c.code} value={c.code}>{c.name}</option>
                  ))}
                </Select>
              </Field>
              <Field label={t('students.form.language')} htmlFor="primaryLanguage">
                <Input id="primaryLanguage" {...register('primaryLanguage')} />
              </Field>
              <Field label={t('students.form.studentIdNumber')} htmlFor="emiratesId" hint={t('students.form.idNumberHint')} className="sm:col-span-2">
                <Input id="emiratesId" dir="ltr" className="text-start" maxLength={30} autoComplete="off" {...register('emiratesId')} />
              </Field>
            </div>
          </fieldset>

          <fieldset className="space-y-3">
            <legend className="mb-2 text-[11px] font-bold uppercase tracking-wide text-muted-foreground">{t('students.form.sectionPlacement')}</legend>
            {needDob ? (
              <p className="text-xs text-muted-foreground">{t('students.form.placementNeedsDob')}</p>
            ) : (
              <p className="text-xs text-muted-foreground">{t('students.form.placementAge', { age: age! })}</p>
            )}
            <div className="grid gap-3 sm:grid-cols-2">
              <Field label={t('students.form.room')} htmlFor="roomId" hint={!needDob && fittingRooms.length === 0 ? t('students.form.noRoomFits') : undefined}>
                <Select id="roomId" disabled={needDob} {...register('roomId')}>
                  <option value="">—</option>
                  {fittingRooms.map((room) => (
                    <option key={room.id} value={room.id}>{localized(room.name, i18n.language)} ({t('rooms.ageRange', { min: room.min_age_months, max: room.max_age_months })})</option>
                  ))}
                </Select>
              </Field>
              <Field label={t('students.form.program')} htmlFor="programId" hint={!needDob && fittingPrograms.length === 0 ? t('students.form.noProgramFits') : undefined}>
                <Select id="programId" disabled={needDob} {...register('programId')}>
                  <option value="">—</option>
                  {fittingPrograms.map((program) => (
                    <option key={program.id} value={program.id}>{localized(program.name, i18n.language)}</option>
                  ))}
                </Select>
              </Field>
            </div>
          </fieldset>

          <fieldset className="space-y-3">
            <legend className="mb-2 text-[11px] font-bold uppercase tracking-wide text-muted-foreground">{t('students.form.sectionHealth')}</legend>
            <Field label={t('students.form.allergyInfo')} htmlFor="allergyInfo">
              <Textarea id="allergyInfo" rows={2} {...register('allergyInfo')} />
            </Field>
            <Field label={t('students.form.medicalNotes')} htmlFor="medicalNotes">
              <Textarea id="medicalNotes" rows={2} {...register('medicalNotes')} />
            </Field>
            <Field label={t('students.form.dietary')} htmlFor="dietaryRequirements">
              <Textarea id="dietaryRequirements" rows={2} {...register('dietaryRequirements')} />
            </Field>
          </fieldset>

          {!editing && (
            <fieldset className="space-y-3">
              <legend className="mb-2 text-[11px] font-bold uppercase tracking-wide text-muted-foreground">{t('students.form.sectionGuardian')}</legend>
              <div className="grid gap-3 sm:grid-cols-2">
                <Field label={t('students.form.guardianFirst')} htmlFor="guardianFirst" error={err(errors.guardianFirst?.message)}>
                  <Input id="guardianFirst" aria-invalid={!!errors.guardianFirst} {...register('guardianFirst')} />
                </Field>
                <Field label={t('students.form.guardianLast')} htmlFor="guardianLast" error={err(errors.guardianLast?.message)}>
                  <Input id="guardianLast" aria-invalid={!!errors.guardianLast} {...register('guardianLast')} />
                </Field>
                <Field label={t('students.form.guardianEmail')} htmlFor="guardianEmail" error={err(errors.guardianEmail?.message)}>
                  <Input id="guardianEmail" type="email" dir="ltr" className="text-start" aria-invalid={!!errors.guardianEmail} {...register('guardianEmail')} />
                </Field>
                <Field label={t('students.form.guardianPhone')} htmlFor="guardianPhone">
                  <Input id="guardianPhone" type="tel" dir="ltr" className="text-start" {...register('guardianPhone')} />
                </Field>
                <Field label={t('students.form.guardianIdNumber')} htmlFor="guardianEmiratesId">
                  <Input id="guardianEmiratesId" dir="ltr" className="text-start" maxLength={30} autoComplete="off" {...register('guardianEmiratesId')} />
                </Field>
                <Field label={t('students.form.relationship')} htmlFor="guardianRelationship">
                  <Select id="guardianRelationship" {...register('guardianRelationship')}>
                    {RELATIONSHIPS.map((r) => (
                      <option key={r} value={r}>{t(`students.guardians.relationship.${r}`)}</option>
                    ))}
                  </Select>
                </Field>
              </div>
              <p className="text-[11px] text-muted-foreground">{t('students.form.addGuardianLater')}</p>
            </fieldset>
          )}

          <div className="flex justify-end gap-2 border-t pt-4">
            <Button type="button" variant="secondary" onClick={() => onOpenChange(false)}>{t('common.cancel')}</Button>
            <Button type="submit" disabled={isSubmitting}>{isSubmitting ? t('common.saving') : editing ? t('common.save') : t('common.create')}</Button>
          </div>
        </form>
      </DialogContent>
    </Dialog>
  )
}

export const CreateStudentDialog = StudentDialog

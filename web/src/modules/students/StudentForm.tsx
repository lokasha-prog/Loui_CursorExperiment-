import { zodResolver } from '@hookform/resolvers/zod'
import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { useTranslation } from 'react-i18next'
import { z } from 'zod'
import { useCreateStudent, useProgramOptions, useRoomOptions } from '@/api/hooks'
import type { CreateStudentRequest } from '@/api/types'
import { Button } from '@/components/ui/button'
import { Field, Input, Select, Textarea } from '@/components/ui/form-controls'
import { Alert, Dialog, DialogContent, DialogHeader } from '@/components/ui/surfaces'
import { toast } from '@/components/ui/toaster'
import { useApiErrorMessage } from '@/hooks/use-api-error'
import { emptyToNull, todayIso } from '@/lib/format'
import { localized } from '@/lib/utils'

const RELATIONSHIPS = ['mother', 'father', 'grandparent', 'legal_guardian', 'other'] as const
const GENDERS = ['male', 'female', 'other', 'unspecified'] as const

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
    roomId: z.string().optional(),
    programId: z.string().optional(),
    medicalNotes: z.string().optional(),
    allergyInfo: z.string().optional(),
    dietaryRequirements: z.string().optional(),
    guardianFirst: z.string().optional(),
    guardianLast: z.string().optional(),
    guardianEmail: z.string().optional(),
    guardianPhone: z.string().optional(),
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

export function toCreateRequest(values: StudentFormValues): CreateStudentRequest {
  const hasGuardian = !!values.guardianEmail?.trim()
  return {
    student: {
      first_name: values.firstName.trim(),
      last_name: values.lastName.trim(),
      first_name_arabic: emptyToNull(values.firstNameAr),
      last_name_arabic: emptyToNull(values.lastNameAr),
      date_of_birth: values.dateOfBirth,
      gender: emptyToNull(values.gender),
      room_id: emptyToNull(values.roomId),
      group_id: null,
      program_id: emptyToNull(values.programId),
      key_person_staff_id: null,
      start_date: null,
      nationality: emptyToNull(values.nationality),
      religion: null,
      primary_language: emptyToNull(values.primaryLanguage),
      medical_notes: emptyToNull(values.medicalNotes),
      allergy_info: emptyToNull(values.allergyInfo),
      dietary_requirements: emptyToNull(values.dietaryRequirements),
      custody_notes: null,
      passport_number: null,
      emirates_id: null,
      label_ids: null,
    },
    guardians: hasGuardian
      ? [
          {
            first_name: values.guardianFirst!.trim(),
            last_name: values.guardianLast!.trim(),
            email: values.guardianEmail!.trim(),
            phone: emptyToNull(values.guardianPhone),
            relationship: values.guardianRelationship || 'mother',
            is_primary: true,
          },
        ]
      : null,
  }
}

export function CreateStudentDialog({ open, onOpenChange }: { open: boolean; onOpenChange: (open: boolean) => void }) {
  const { t, i18n } = useTranslation()
  const create = useCreateStudent()
  const rooms = useRoomOptions(open)
  const programs = useProgramOptions(open)
  const message = useApiErrorMessage()
  const [failure, setFailure] = useState<string | null>(null)
  const {
    register,
    handleSubmit,
    reset,
    formState: { errors, isSubmitting },
  } = useForm<StudentFormValues>({ resolver: zodResolver(studentSchema), defaultValues: { guardianRelationship: 'mother' } })

  const err = (key?: string) => (key ? t(key) : undefined)

  const onSubmit = handleSubmit(async (values) => {
    setFailure(null)
    try {
      await create.mutateAsync(toCreateRequest(values))
      toast.success(t('students.form.created'))
      reset({ guardianRelationship: 'mother' })
      onOpenChange(false)
    } catch (error) {
      setFailure(message(error))
    }
  })

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent closeLabel={t('common.close')}>
        <DialogHeader title={t('students.form.createTitle')} description={t('students.form.createDescription')} />
        <form onSubmit={onSubmit} noValidate className="space-y-6">
          {failure && <Alert>{failure}</Alert>}

          <fieldset className="space-y-3">
            <legend className="mb-2 text-[11px] font-bold uppercase tracking-wide text-muted-foreground">{t('students.form.sectionChild')}</legend>
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
                <Input id="nationality" {...register('nationality')} />
              </Field>
              <Field label={t('students.form.language')} htmlFor="primaryLanguage">
                <Input id="primaryLanguage" {...register('primaryLanguage')} />
              </Field>
            </div>
          </fieldset>

          <fieldset className="space-y-3">
            <legend className="mb-2 text-[11px] font-bold uppercase tracking-wide text-muted-foreground">{t('students.form.sectionPlacement')}</legend>
            <div className="grid gap-3 sm:grid-cols-2">
              <Field label={t('students.form.room')} htmlFor="roomId">
                <Select id="roomId" {...register('roomId')}>
                  <option value="">—</option>
                  {rooms.data?.map((room) => (
                    <option key={room.id} value={room.id}>{localized(room.name, i18n.language)}</option>
                  ))}
                </Select>
              </Field>
              <Field label={t('students.form.program')} htmlFor="programId">
                <Select id="programId" {...register('programId')}>
                  <option value="">—</option>
                  {programs.data?.map((program) => (
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

          <div className="flex justify-end gap-2 border-t pt-4">
            <Button type="button" variant="secondary" onClick={() => onOpenChange(false)}>{t('common.cancel')}</Button>
            <Button type="submit" disabled={isSubmitting}>{isSubmitting ? t('common.saving') : t('common.create')}</Button>
          </div>
        </form>
      </DialogContent>
    </Dialog>
  )
}

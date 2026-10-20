import { zodResolver } from '@hookform/resolvers/zod'
import { useEffect, useMemo, useState } from 'react'
import { useForm } from 'react-hook-form'
import { useTranslation } from 'react-i18next'
import { z } from 'zod'
import { ApiError } from '@/api/client'
import { useCreateUser, useSetupStatus, useStaff, flatten } from '@/api/hooks'
import { Button } from '@/components/ui/button'
import { Field, Input, Select } from '@/components/ui/form-controls'
import { Alert, Dialog, DialogContent, DialogHeader } from '@/components/ui/surfaces'
import { toast } from '@/components/ui/toaster'
import { useApiErrorMessage } from '@/hooks/use-api-error'
import { useAuth } from '@/stores/auth'

export const ROLE_NAMES = ['super_admin', 'centre_admin', 'teacher', 'parent', 'receptionist', 'accounts', 'it', 'marketing'] as const

export interface UserPrefill {
  firstName?: string
  lastName?: string
  email?: string
  role?: string
  guardianId?: string
}

function buildSchema(minPassword: number) {
  return z.object({
    firstName: z.string().trim().min(1, 'validation.required'),
    lastName: z.string().trim().min(1, 'validation.required'),
    email: z.string().trim().min(1, 'validation.required').email('validation.email'),
    password: z.string().min(minPassword, 'validation.passwordLength').regex(/(?=.*\p{L})(?=.*\d)/u, 'validation.passwordMix'),
    role: z.string().min(1, 'validation.required'),
    language: z.string(),
    staffId: z.string().optional(),
  })
}

type Values = z.infer<ReturnType<typeof buildSchema>>

export function CreateUserDialog({
  open,
  onOpenChange,
  prefill,
}: {
  open: boolean
  onOpenChange: (open: boolean) => void
  prefill?: UserPrefill
}) {
  const { t } = useTranslation()
  const me = useAuth((s) => s.me)
  const policy = useSetupStatus()
  const minPassword = policy.data?.passwordMinLength ?? 1
  const schema = useMemo(() => buildSchema(minPassword), [minPassword])
  const create = useCreateUser()
  const staff = useStaff({})
  const message = useApiErrorMessage()
  const [failure, setFailure] = useState<string | null>(null)
  const roles = ROLE_NAMES.filter((r) => r !== 'super_admin' || me?.roles.includes('super_admin'))

  const {
    register,
    handleSubmit,
    reset,
    setError,
    formState: { errors, isSubmitting },
  } = useForm<Values>({
    resolver: zodResolver(schema),
    defaultValues: { firstName: '', lastName: '', email: '', password: '', role: 'teacher', language: 'en' },
  })

  useEffect(() => {
    if (open) {
      reset({
        firstName: prefill?.firstName ?? '',
        lastName: prefill?.lastName ?? '',
        email: prefill?.email ?? '',
        password: '',
        role: prefill?.role ?? 'teacher',
        language: 'en',
      })
    }
  }, [open, prefill, reset])

  const err = (key?: string) => (key ? t(key, { min: minPassword }) : undefined)

  const onSubmit = handleSubmit(async (values) => {
    setFailure(null)
    try {
      await create.mutateAsync({
        email: values.email.trim(),
        first_name: values.firstName.trim(),
        last_name: values.lastName.trim(),
        password: values.password,
        roles: [values.role],
        preferred_language: values.language,
        staff_id: values.staffId || null,
        guardian_id: prefill?.guardianId ?? null,
      })
      toast.success(t('users.form.created'))
      onOpenChange(false)
    } catch (error) {
      if (error instanceof ApiError && error.status === 409) setError('email', { message: 'errors.DUPLICATE_RESOURCE' })
      else setFailure(message(error))
    }
  })

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent closeLabel={t('common.close')} className="max-w-lg">
        <DialogHeader title={t('users.form.createTitle')} description={t('users.form.createDescription')} />
        <form onSubmit={onSubmit} noValidate className="space-y-4">
          {failure && <Alert>{failure}</Alert>}
          <div className="grid gap-3 sm:grid-cols-2">
            <Field label={t('users.form.firstName')} htmlFor="u-first" required error={err(errors.firstName?.message)}>
              <Input id="u-first" aria-invalid={!!errors.firstName} {...register('firstName')} />
            </Field>
            <Field label={t('users.form.lastName')} htmlFor="u-last" required error={err(errors.lastName?.message)}>
              <Input id="u-last" aria-invalid={!!errors.lastName} {...register('lastName')} />
            </Field>
            <Field label={t('users.form.email')} htmlFor="u-email" required error={err(errors.email?.message)} className="sm:col-span-2">
              <Input id="u-email" type="email" dir="ltr" className="text-start" aria-invalid={!!errors.email} {...register('email')} />
            </Field>
            <Field label={t('users.form.password')} htmlFor="u-password" hint={t('users.form.passwordHint', { min: minPassword })} required error={err(errors.password?.message)} className="sm:col-span-2">
              <Input id="u-password" type="password" autoComplete="new-password" dir="ltr" className="text-start" aria-invalid={!!errors.password} {...register('password')} />
            </Field>
            <Field label={t('users.form.role')} htmlFor="u-role" required error={err(errors.role?.message)}>
              <Select id="u-role" disabled={!!prefill?.role} {...register('role')}>
                {roles.map((r) => (
                  <option key={r} value={r}>{t(`roles.${r}`)}</option>
                ))}
              </Select>
            </Field>
            <Field label={t('users.form.language')} htmlFor="u-lang">
              <Select id="u-lang" {...register('language')}>
                <option value="en">{t('common.english')}</option>
                <option value="ar">{t('common.arabic')}</option>
              </Select>
            </Field>
            {!prefill?.guardianId && (
              <Field label={t('users.form.linkStaff')} htmlFor="u-staff" className="sm:col-span-2">
                <Select id="u-staff" {...register('staffId')}>
                  <option value="">{t('users.form.none')}</option>
                  {flatten(staff.data).map((s) => (
                    <option key={s.id} value={s.id}>{s.first_name} {s.last_name}</option>
                  ))}
                </Select>
              </Field>
            )}
          </div>
          <div className="flex justify-end gap-2 border-t pt-4">
            <Button type="button" variant="secondary" onClick={() => onOpenChange(false)}>{t('common.cancel')}</Button>
            <Button type="submit" disabled={isSubmitting}>{isSubmitting ? t('common.saving') : t('common.create')}</Button>
          </div>
        </form>
      </DialogContent>
    </Dialog>
  )
}

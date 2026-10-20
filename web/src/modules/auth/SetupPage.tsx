import { zodResolver } from '@hookform/resolvers/zod'
import { CheckCircle2 } from 'lucide-react'
import { useMemo, useState } from 'react'
import { useForm } from 'react-hook-form'
import { useTranslation } from 'react-i18next'
import { Link, Navigate } from 'react-router-dom'
import { z } from 'zod'
import { ApiError, authApi } from '@/api/client'
import { useSetupStatus } from '@/api/hooks'
import { Button } from '@/components/ui/button'
import { Field, Input } from '@/components/ui/form-controls'
import { Alert, Skeleton } from '@/components/ui/surfaces'
import { AuthLayout } from './AuthLayout'

function buildSchema(minPassword: number) {
  return z.object({
    bootstrapToken: z.string().min(1, 'validation.required'),
    tenantNameEn: z.string().min(1, 'validation.required'),
    tenantNameAr: z.string().optional(),
    firstName: z.string().min(1, 'validation.required'),
    lastName: z.string().min(1, 'validation.required'),
    email: z.string().min(1, 'validation.required').email('validation.email'),
    password: z
      .string()
      .min(minPassword, 'validation.passwordLength')
      .regex(/(?=.*\p{L})(?=.*\d)/u, 'validation.passwordMix'),
  })
}

type Values = z.infer<ReturnType<typeof buildSchema>>

/** The internal centre code is made from the English name, so nobody has to type or remember one. */
export function slugify(name: string): string {
  return name.toLowerCase().replace(/[^a-z0-9]+/g, '-').replace(/^-+|-+$/g, '') || 'centre'
}

export default function SetupPage() {
  const { t } = useTranslation()
  const status = useSetupStatus()
  const [failure, setFailure] = useState<string | null>(null)
  const [done, setDone] = useState(false)
  const minPassword = status.data?.passwordMinLength ?? 1
  const schema = useMemo(() => buildSchema(minPassword), [minPassword])
  const {
    register,
    handleSubmit,
    formState: { errors, isSubmitting },
  } = useForm<Values>({ resolver: zodResolver(schema) })

  if (status.isLoading) return <AuthLayout><Skeleton className="h-64" /></AuthLayout>
  if (status.data && !status.data.setupRequired && !done) return <Navigate to="/login" replace />

  const message = (key?: string) => (key ? t(key, { min: minPassword }) : undefined)

  const onSubmit = handleSubmit(async (values) => {
    setFailure(null)
    try {
      await authApi.bootstrap({
        bootstrap_token: values.bootstrapToken.trim(),
        tenant_slug: slugify(values.tenantNameEn),
        tenant_name: { en: values.tenantNameEn.trim(), ...(values.tenantNameAr?.trim() ? { ar: values.tenantNameAr.trim() } : {}) },
        admin_email: values.email.trim(),
        admin_first_name: values.firstName.trim(),
        admin_last_name: values.lastName.trim(),
        admin_password: values.password,
      })
      setDone(true)
    } catch (error) {
      if (error instanceof ApiError && error.status === 404) setFailure(t('auth.setup.notAllowed'))
      else if (error instanceof ApiError && error.status === 409) setFailure(t('auth.setup.alreadyDone'))
      else if (error instanceof ApiError && error.status === 400) setFailure(t('errors.VALIDATION_ERROR'))
      else setFailure(t('errors.NETWORK'))
    }
  })

  if (done) {
    return (
      <AuthLayout>
        <div className="flex flex-col items-center gap-4 py-6 text-center">
          <CheckCircle2 className="h-12 w-12 text-success" aria-hidden="true" />
          <h2 className="font-heading text-2xl font-bold">{t('auth.setup.doneTitle')}</h2>
          <p className="text-sm text-muted-foreground">{t('auth.setup.doneBody')}</p>
          <Button asChild size="lg" className="w-full">
            <Link to="/login">{t('auth.setup.goToLogin')}</Link>
          </Button>
        </div>
      </AuthLayout>
    )
  }

  return (
    <AuthLayout>
      <h2 className="font-heading text-2xl font-bold">{t('auth.setup.title')}</h2>
      <p className="mb-5 mt-1 text-sm text-muted-foreground">{t('auth.setup.subtitle')}</p>
      <form onSubmit={onSubmit} noValidate className="space-y-4">
        {failure && <Alert>{failure}</Alert>}
        <Field label={t('auth.setup.setupKey')} htmlFor="bootstrapToken" hint={t('auth.setup.setupKeyHint')} required error={message(errors.bootstrapToken?.message)}>
          <Input id="bootstrapToken" type="password" autoComplete="off" dir="ltr" className="text-start" aria-invalid={!!errors.bootstrapToken} {...register('bootstrapToken')} />
        </Field>
        <fieldset className="space-y-3">
          <legend className="mb-1 text-[11px] font-bold uppercase tracking-wide text-muted-foreground">{t('auth.setup.section.centre')}</legend>
          <Field label={t('auth.setup.centreName')} htmlFor="tenantNameEn" required error={message(errors.tenantNameEn?.message)}>
            <Input id="tenantNameEn" dir="ltr" className="text-start" aria-invalid={!!errors.tenantNameEn} {...register('tenantNameEn')} />
          </Field>
          <Field label={t('auth.setup.centreNameAr')} htmlFor="tenantNameAr">
            <Input id="tenantNameAr" dir="rtl" lang="ar" {...register('tenantNameAr')} />
          </Field>
        </fieldset>
        <fieldset className="space-y-3">
          <legend className="mb-1 text-[11px] font-bold uppercase tracking-wide text-muted-foreground">{t('auth.setup.section.admin')}</legend>
          <div className="grid grid-cols-2 gap-3">
            <Field label={t('auth.setup.adminFirst')} htmlFor="firstName" required error={message(errors.firstName?.message)}>
              <Input id="firstName" autoComplete="given-name" aria-invalid={!!errors.firstName} {...register('firstName')} />
            </Field>
            <Field label={t('auth.setup.adminLast')} htmlFor="lastName" required error={message(errors.lastName?.message)}>
              <Input id="lastName" autoComplete="family-name" aria-invalid={!!errors.lastName} {...register('lastName')} />
            </Field>
          </div>
          <Field label={t('auth.setup.adminEmail')} htmlFor="email" required error={message(errors.email?.message)}>
            <Input id="email" type="email" autoComplete="email" dir="ltr" className="text-start" aria-invalid={!!errors.email} {...register('email')} />
          </Field>
          <Field label={t('auth.setup.adminPassword')} htmlFor="password" hint={t('auth.setup.passwordHint', { min: minPassword })} required error={message(errors.password?.message)}>
            <Input id="password" type="password" autoComplete="new-password" dir="ltr" className="text-start" aria-invalid={!!errors.password} {...register('password')} />
          </Field>
        </fieldset>
        <Button type="submit" size="lg" className="w-full" disabled={isSubmitting}>
          {isSubmitting ? t('auth.setup.submitting') : t('auth.setup.submit')}
        </Button>
      </form>
    </AuthLayout>
  )
}

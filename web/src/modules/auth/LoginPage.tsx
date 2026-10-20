import { zodResolver } from '@hookform/resolvers/zod'
import { Eye, EyeOff } from 'lucide-react'
import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { useTranslation } from 'react-i18next'
import { Link, Navigate, useLocation, useNavigate } from 'react-router-dom'
import { z } from 'zod'
import { useSetupStatus } from '@/api/hooks'
import { ApiError, authApi } from '@/api/client'
import { Button } from '@/components/ui/button'
import { Field, Input } from '@/components/ui/form-controls'
import { Alert } from '@/components/ui/surfaces'
import { useAuth } from '@/stores/auth'
import { AuthLayout } from './AuthLayout'

const schema = z.object({
  tenant: z.string().optional(),
  email: z.string().min(1, 'validation.required').email('validation.email'),
  password: z.string().min(1, 'validation.required'),
})

type Values = z.infer<typeof schema>

export default function LoginPage() {
  const { t } = useTranslation()
  const me = useAuth((s) => s.me)
  const navigate = useNavigate()
  const location = useLocation()
  const setup = useSetupStatus()
  const [showPassword, setShowPassword] = useState(false)
  const [failure, setFailure] = useState<string | null>(null)
  const {
    register,
    handleSubmit,
    formState: { errors, isSubmitting },
  } = useForm<Values>({ resolver: zodResolver(schema) })

  const destination = (location.state as { from?: string } | null)?.from ?? '/dashboard'
  if (me) return <Navigate to={destination} replace />

  const onSubmit = handleSubmit(async (values) => {
    setFailure(null)
    try {
      await authApi.login({ tenant: values.tenant?.trim(), email: values.email.trim(), password: values.password })
      navigate(destination, { replace: true })
    } catch (error) {
      if (error instanceof ApiError && error.status === 401) {
        setFailure(error.message.toLowerCase().includes('locked') ? t('auth.login.locked') : t('auth.login.invalid'))
      } else if (error instanceof ApiError && error.status === 403) {
        setFailure(t('auth.login.inactive'))
      } else if (error instanceof ApiError && error.code === 'RATE_LIMIT_EXCEEDED') {
        setFailure(t('errors.RATE_LIMIT_EXCEEDED'))
      } else {
        setFailure(t('errors.NETWORK'))
      }
    }
  })

  return (
    <AuthLayout>
      <h2 className="font-heading text-2xl font-bold">{t('auth.login.title')}</h2>
      <p className="mb-6 mt-1 text-sm text-muted-foreground">{t('auth.login.subtitle')}</p>

      <form onSubmit={onSubmit} noValidate className="space-y-4">
        {failure && <Alert>{failure}</Alert>}
        <Field label={t('auth.login.centre')} htmlFor="tenant" hint={t('auth.login.centreHint')}>
          <Input id="tenant" autoComplete="organization" dir="ltr" className="text-start" {...register('tenant')} />
        </Field>
        <Field label={t('auth.login.email')} htmlFor="email" required error={errors.email && t(errors.email.message!)}>
          <Input
            id="email"
            type="email"
            autoComplete="username"
            dir="ltr"
            className="text-start"
            aria-invalid={!!errors.email}
            aria-describedby={errors.email ? 'email-error' : undefined}
            {...register('email')}
          />
        </Field>
        <Field label={t('auth.login.password')} htmlFor="password" required error={errors.password && t(errors.password.message!)}>
          <div className="relative">
            <Input
              id="password"
              type={showPassword ? 'text' : 'password'}
              autoComplete="current-password"
              dir="ltr"
              className="pe-10 text-start"
              aria-invalid={!!errors.password}
              aria-describedby={errors.password ? 'password-error' : undefined}
              {...register('password')}
            />
            <button
              type="button"
              onClick={() => setShowPassword((v) => !v)}
              aria-label={showPassword ? t('auth.login.hide') : t('auth.login.show')}
              className="absolute end-2 top-1/2 -translate-y-1/2 rounded p-1 text-muted-foreground hover:text-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
            >
              {showPassword ? <EyeOff className="h-4 w-4" /> : <Eye className="h-4 w-4" />}
            </button>
          </div>
        </Field>
        <Button type="submit" size="lg" className="w-full" disabled={isSubmitting}>
          {isSubmitting ? t('auth.login.submitting') : t('auth.login.submit')}
        </Button>
      </form>

      {setup.data?.setupRequired && (
        <p className="mt-6 text-center text-sm text-muted-foreground">
          {t('auth.login.firstTime')}{' '}
          <Link to="/setup" className="font-bold text-primary-dark underline-offset-4 hover:underline">
            {t('auth.login.setupLink')}
          </Link>
        </p>
      )}
    </AuthLayout>
  )
}

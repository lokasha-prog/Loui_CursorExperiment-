import { zodResolver } from '@hookform/resolvers/zod'
import { Building2, ChevronDown, Eye, EyeOff } from 'lucide-react'
import { useQuery } from '@tanstack/react-query'
import { useEffect, useMemo, useState } from 'react'
import { useForm } from 'react-hook-form'
import { useTranslation } from 'react-i18next'
import { Link, Navigate, useLocation, useNavigate } from 'react-router-dom'
import { z } from 'zod'
import { ApiError, authApi } from '@/api/client'
import { useSetupStatus } from '@/api/hooks'
import type { CentreChoice } from '@/api/types'
import { Button } from '@/components/ui/button'
import { Field, Input } from '@/components/ui/form-controls'
import { Alert } from '@/components/ui/surfaces'
import { localized } from '@/lib/utils'
import { useAuth } from '@/stores/auth'
import { AuthLayout } from './AuthLayout'

const schema = z.object({
  email: z.string().min(1, 'validation.required').email('validation.email'),
  password: z.string().min(1, 'validation.required'),
})

type Values = z.infer<typeof schema>

export default function LoginPage() {
  const { t, i18n } = useTranslation()
  const me = useAuth((s) => s.me)
  const navigate = useNavigate()
  const location = useLocation()
  const setup = useSetupStatus()
  const [showPassword, setShowPassword] = useState(false)
  const [failure, setFailure] = useState<string | null>(null)
  const testUsers = useQuery({
    queryKey: ['dev-test-users'],
    queryFn: () => authApi.devTestUsers(),
    retry: false,
    staleTime: Infinity,
  })
  const ownerLogin = useQuery({
    queryKey: ['dev-owner-login'],
    queryFn: () => authApi.devOwnerLogin(),
    retry: false,
    staleTime: Infinity,
  })
  const [quickRole, setQuickRole] = useState<string | null>(null)
  const [choices, setChoices] = useState<CentreChoice[] | null>(null)
  const [submitted, setSubmitted] = useState<Values | null>(null)
  const {
    register,
    handleSubmit,
    setValue,
    formState: { errors, isSubmitting },
  } = useForm<Values>({ resolver: zodResolver(schema) })

  useEffect(() => {
    const owner = ownerLogin.data
    if (!owner?.email || !owner.password) return
    setValue('email', owner.email)
    setValue('password', owner.password)
  }, [ownerLogin.data, setValue])

  const accounts = useMemo(() => {
    const owner = ownerLogin.data
    const roles = testUsers.data ?? []
    return [
      ...(owner ? [{ key: 'owner', label: `${owner.name} · ${t('roles.super_admin')}`, email: owner.email, password: owner.password, role: null as string | null }] : []),
      ...roles.map((user) => ({ key: user.role, label: t(`roles.${user.role}`), email: user.email, password: '', role: user.role })),
    ]
  }, [ownerLogin.data, testUsers.data, t])

  const pickAccount = (key: string) => {
    const account = accounts.find((item) => item.key === key)
    if (!account) return
    setValue('email', account.email, { shouldValidate: true })
    setValue('password', account.password, { shouldValidate: !!account.password })
    setQuickRole(account.role)
  }

  const destination = (location.state as { from?: string } | null)?.from ?? '/dashboard'
  if (me) return <Navigate to={destination} replace />

  const attempt = async (values: Values, centre?: string) => {
    setFailure(null)
    try {
      // The centre is found automatically from the e-mail address; a code is only sent when choosing between several.
      const result = await authApi.login({ email: values.email.trim(), password: values.password, tenant: centre })
      if ('chooseCentre' in result) {
        setSubmitted(values)
        setChoices(result.chooseCentre)
        return
      }
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
  }

  const onSubmit = handleSubmit((values) => attempt(values))

  const enterAs = async (role: string) => {
    setFailure(null)
    try {
      await authApi.devQuickLogin(role)
      navigate(destination, { replace: true })
    } catch {
      setFailure(t('errors.NETWORK'))
    }
  }

  if (choices && submitted) {
    return (
      <AuthLayout>
        <h2 className="font-heading text-2xl font-bold">{t('auth.login.chooseCentre')}</h2>
        <p className="mb-5 mt-1 text-sm text-muted-foreground">{t('auth.login.chooseCentreHint')}</p>
        {failure && <Alert className="mb-3">{failure}</Alert>}
        <ul className="space-y-2">
          {choices.map((centre) => (
            <li key={centre.id}>
              <Button variant="secondary" size="lg" className="w-full justify-start" onClick={() => void attempt(submitted, centre.slug)}>
                <Building2 className="h-4 w-4 text-primary-dark" aria-hidden="true" />
                {localized(centre.name, i18n.language)}
              </Button>
            </li>
          ))}
        </ul>
        <Button variant="ghost" size="sm" className="mt-4" onClick={() => setChoices(null)}>
          {t('auth.login.backToSignIn')}
        </Button>
      </AuthLayout>
    )
  }

  return (
    <AuthLayout>
      <h2 className="font-heading text-2xl font-bold">{t('auth.login.title')}</h2>
      <p className="mb-6 mt-1 text-sm text-muted-foreground">{t('auth.login.subtitle')}</p>

      <form
        onSubmit={(event) => {
          if (quickRole) {
            event.preventDefault()
            void enterAs(quickRole)
            return
          }
          void onSubmit(event)
        }}
        noValidate
        className="space-y-4"
      >
        {failure && <Alert>{failure}</Alert>}
        <Field label={t('auth.login.email')} htmlFor="email" required error={errors.email && t(errors.email.message!)}>
          <div className="relative">
            <Input
              id="email"
              type="email"
              autoComplete="username"
              dir="ltr"
              className="pe-10 text-start"
              aria-invalid={!!errors.email}
              aria-describedby={errors.email ? 'email-error' : undefined}
              {...register('email', { onChange: () => setQuickRole(null) })}
            />
            {accounts.length > 0 && (
              <>
                <select
                  id="dev-account"
                  aria-label={t('auth.dev.pick')}
                  className="absolute end-0 top-0 h-10 w-10 cursor-pointer opacity-0"
                  value=""
                  onChange={(event) => {
                    pickAccount(event.target.value)
                    event.target.value = ''
                  }}
                >
                  <option value="">{t('auth.dev.pick')}</option>
                  {accounts.map((account) => (
                    <option key={account.key} value={account.key}>
                      {account.label} — {account.email}
                    </option>
                  ))}
                </select>
                <ChevronDown className="pointer-events-none absolute end-2 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground" aria-hidden="true" />
              </>
            )}
          </div>
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

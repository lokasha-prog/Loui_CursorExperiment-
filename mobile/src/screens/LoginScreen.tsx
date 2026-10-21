import { useState } from 'react'
import { View } from 'react-native'
import { DEV_LOGIN } from '../api'
import { useAuth } from '../auth'
import { useI18n, pick, type Lang } from '../i18n'
import { Banner, Body, Btn, Card, Field, Heading, Screen, Title } from '../ui'

export function LoginScreen() {
  const { t, lang, setLang } = useI18n()
  const { signIn, devSignIn } = useAuth()
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [centres, setCentres] = useState<{ slug: string; name: Record<string, string> }[] | null>(null)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState(false)

  const submit = async (tenant?: string) => {
    setBusy(true)
    setError(false)
    try {
      const result = await signIn(email.trim(), password, tenant)
      if (result) setCentres(result.choose)
    } catch {
      setError(true)
    } finally {
      setBusy(false)
    }
  }

  return (
    <Screen>
      <View style={{ alignItems: 'center', paddingVertical: 24, gap: 6 }}>
        <Title>{t('appName')}</Title>
        <Body muted>{t('staffApp')}</Body>
      </View>
      {error && <Banner tone="danger" text={t('signInFailed')} />}
      {centres ? (
        <Card>
          <Heading>{t('chooseCentre')}</Heading>
          {centres.map((c) => <Btn key={c.slug} kind="secondary" label={pick(c.name, lang)} onPress={() => void submit(c.slug)} busy={busy} />)}
        </Card>
      ) : (
        <Card>
          <Field label={t('email')} value={email} onChangeText={setEmail} autoCapitalize="none" keyboardType="email-address" autoComplete="email" />
          <Field label={t('password')} value={password} onChangeText={setPassword} secureTextEntry autoComplete="password" />
          <Btn label={busy ? t('signingIn') : t('signIn')} onPress={() => void submit()} busy={busy} disabled={!email || !password} />
        </Card>
      )}
      {DEV_LOGIN && (
        <Card>
          <Body muted>Test users (this computer only)</Body>
          {['it', 'teacher', 'centre_admin'].map((role) => <Btn key={role} kind="secondary" label={role} onPress={() => void devSignIn(role)} />)}
        </Card>
      )}
      <LanguageSwitch lang={lang} setLang={setLang} />
    </Screen>
  )
}

export function LanguageSwitch({ lang, setLang }: { lang: Lang; setLang: (l: Lang) => void }) {
  const { t } = useI18n()
  return (
    <View style={{ gap: 8 }}>
      <Btn kind={lang === 'en' ? 'primary' : 'secondary'} label={t('english')} onPress={() => setLang('en')} />
      <Btn kind={lang === 'ar' ? 'primary' : 'secondary'} label={t('arabic')} onPress={() => setLang('ar')} />
    </View>
  )
}


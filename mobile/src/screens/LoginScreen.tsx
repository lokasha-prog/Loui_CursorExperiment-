import { useState } from 'react'
import { View } from 'react-native'
import { DEV_LOGIN } from '../api'
import { apiBase, setApiUrl } from '../core'
import { useAuth } from '../auth'
import { useI18n, pick, type Lang } from '../i18n'
import { Ionicons } from '@expo/vector-icons'
import { Banner, Body, Btn, Card, Chip, Field, Heading, Row, Screen, Title, color } from '../ui'

export function LoginScreen() {
  const { t, lang, setLang } = useI18n()
  const { signIn, devSignIn } = useAuth()
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [centres, setCentres] = useState<{ slug: string; name: Record<string, string> }[] | null>(null)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState(false)
  const [server, setServer] = useState(apiBase())
  const [serverSaved, setServerSaved] = useState(false)

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
      <View style={{ alignItems: 'center', paddingTop: 28, paddingBottom: 12, gap: 10 }}>
        <View style={{ width: 84, height: 84, borderRadius: 28, backgroundColor: color.primary, alignItems: 'center', justifyContent: 'center', overflow: 'hidden' }}>
          <View style={{ position: 'absolute', top: -18, right: -14, width: 56, height: 56, borderRadius: 28, backgroundColor: 'rgba(255,255,255,0.14)' }} />
          <View style={{ position: 'absolute', bottom: -20, left: -10, width: 50, height: 50, borderRadius: 25, backgroundColor: 'rgba(245,181,68,0.35)' }} />
          <Ionicons name="happy" size={46} color={color.white} />
        </View>
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
          <Btn icon="log-in" label={busy ? t('signingIn') : t('signIn')} onPress={() => void submit()} busy={busy} disabled={!email || !password} />
        </Card>
      )}
      <Card>
        <Field label={t('serverAddress')} value={server} onChangeText={(v) => { setServer(v); setServerSaved(false) }} autoCapitalize="none" keyboardType="url" />
        <Body muted>{t('serverHint')}</Body>
        <Btn kind="secondary" label={serverSaved ? t('serverSaved') : t('serverSave')} onPress={() => void setApiUrl(server).then(() => setServerSaved(true))} />
      </Card>
      {DEV_LOGIN && (
        <Card>
          <Body muted>Test users (this computer only)</Body>
          {['it', 'teacher', 'parent'].map((role) => <Btn key={role} kind="secondary" icon="flash" label={role} onPress={() => void devSignIn(role)} />)}
        </Card>
      )}
      <LanguageSwitch lang={lang} setLang={setLang} />
    </Screen>
  )
}

export function LanguageSwitch({ lang, setLang }: { lang: Lang; setLang: (l: Lang) => void }) {
  const { t } = useI18n()
  return (
    <Row gap={10}>
      <Chip active={lang === 'en'} label={t('english')} onPress={() => setLang('en')} />
      <Chip active={lang === 'ar'} label={t('arabic')} onPress={() => setLang('ar')} />
    </Row>
  )
}

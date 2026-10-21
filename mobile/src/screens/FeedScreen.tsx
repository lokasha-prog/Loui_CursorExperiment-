import { useCallback, useEffect, useState } from 'react'
import { View } from 'react-native'
import { api, ApiError } from '../api'
import { cached } from '../cache'
import { doOrQueue } from '../queue'
import { useAuth } from '../auth'
import { useI18n, pick } from '../i18n'
import { Banner, Body, Btn, Card, Field, Heading, Pill, Row, Screen, Title } from '../ui'

interface Post { id: string; type: string; caption: Record<string, string> | null; posted_by_name: string | null; status: 'pending' | 'approved' | 'rejected'; created_at: string }
const TYPES = ['diary', 'food', 'toilet', 'mood', 'announcement'] as const

export function FeedScreen() {
  const { t, lang } = useI18n()
  const { can } = useAuth()
  const [posts, setPosts] = useState<Post[]>([])
  const [type, setType] = useState<(typeof TYPES)[number]>('diary')
  const [caption, setCaption] = useState('')
  const [busy, setBusy] = useState(false)
  const [offline, setOffline] = useState(false)
  const [message, setMessage] = useState<{ text: string; tone: 'info' | 'danger' } | null>(null)

  const load = useCallback(async () => {
    const r = await cached('feed', () => api.page<Post>('/activities?limit=30'))
    setPosts(r.data ?? [])
    setOffline(r.offline)
  }, [])
  useEffect(() => { void load() }, [load])

  const post = async () => {
    setBusy(true)
    try {
      const sent = await doOrQueue('/activities', { type, caption: { [lang]: caption.trim() }, student_ids: [], media_ids: [] }, 'feed post')
      setCaption('')
      setMessage({ text: sent === 'done' ? t('posted') : t('savedForLater'), tone: 'info' })
      await load()
    } catch (error) {
      setMessage({ text: error instanceof ApiError ? error.message : t('retry'), tone: 'danger' })
    } finally {
      setBusy(false)
    }
  }

  return (
    <Screen refreshing={busy} onRefresh={() => void load()}>
      <Title>{t('tabs.feed')}</Title>
      {offline && <Banner tone="info" text={t('updatedOffline')} />}
      {message && <Banner tone={message.tone} text={message.text} />}
      {can('activities.write') && <Card>
        <Heading>{t('newPost')}</Heading>
        <Body muted>{t('postType')}</Body>
        <Row>{TYPES.map((k) => <Btn key={k} kind={k === type ? 'primary' : 'secondary'} label={t(`types.${k}`)} onPress={() => setType(k)} />)}</Row>
        <Field label={t('caption')} value={caption} onChangeText={setCaption} multiline numberOfLines={3} style={{ minHeight: 80 }} />
        <Btn label={t('post')} onPress={() => void post()} busy={busy} disabled={!caption.trim()} />
      </Card>}
      {posts.length === 0 ? <Body muted>{t('noPosts')}</Body> : posts.map((p) => (
        <Card key={p.id}>
          <Row><Pill tone="info" label={t(`types.${p.type as (typeof TYPES)[number]}`)} /><Pill tone={p.status === 'approved' ? 'ok' : p.status === 'rejected' ? 'danger' : 'warn'} label={t(`approval.${p.status}`)} /></Row>
          <Body>{pick(p.caption, lang)}</Body>
          <View><Body muted>{p.posted_by_name ?? ''} · {new Date(p.created_at).toLocaleString()}</Body></View>
        </Card>
      ))}
    </Screen>
  )
}

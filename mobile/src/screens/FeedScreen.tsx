import * as ImagePicker from 'expo-image-picker'
import { useCallback, useEffect, useState } from 'react'
import { Image, View } from 'react-native'
import { api, ApiError, isOffline, upload } from '../api'
import { StoredPhoto } from '../media'
import { cached } from '../cache'
import { doOrQueue } from '../queue'
import { useAuth } from '../auth'
import { useI18n, pick } from '../i18n'
import { Banner, Body, Btn, Card, Chip, Empty, Field, Heading, Hero, Pill, Row, Screen } from '../ui'

interface Post { id: string; type: string; caption: Record<string, string> | null; posted_by_name: string | null; status: 'pending' | 'approved' | 'rejected'; created_at: string; media: { id: string; content_type: string }[] }
const TYPES = ['diary', 'food', 'toilet', 'mood', 'announcement'] as const

export function FeedScreen() {
  const { t, lang } = useI18n()
  const { can } = useAuth()
  const [posts, setPosts] = useState<Post[]>([])
  const [type, setType] = useState<(typeof TYPES)[number]>('diary')
  const [caption, setCaption] = useState('')
  const [photo, setPhoto] = useState<ImagePicker.ImagePickerAsset | null>(null)
  const [busy, setBusy] = useState(false)
  const [offline, setOffline] = useState(false)
  const [message, setMessage] = useState<{ text: string; tone: 'info' | 'danger' } | null>(null)

  const load = useCallback(async () => {
    const r = await cached('feed', () => api.page<Post>('/activities?limit=30'))
    setPosts(r.data ?? [])
    setOffline(r.offline)
  }, [])
  useEffect(() => { void load() }, [load])

  const pickPhoto = async (camera: boolean) => {
    const permission = camera ? await ImagePicker.requestCameraPermissionsAsync() : await ImagePicker.requestMediaLibraryPermissionsAsync()
    if (!permission.granted) return
    const result = camera ? await ImagePicker.launchCameraAsync({ quality: 0.6 }) : await ImagePicker.launchImageLibraryAsync({ quality: 0.6 })
    if (!result.canceled) setPhoto(result.assets[0])
  }

  const post = async () => {
    setBusy(true)
    try {
      let mediaIds: string[] = []
      let photoSkipped = false
      if (photo) {
        try {
          const media = await upload<{ id: string }>('/media', photo.uri, photo.fileName ?? 'photo.jpg', photo.mimeType ?? 'image/jpeg')
          mediaIds = [media.id]
        } catch (error) {
          if (isOffline(error)) photoSkipped = true
          else throw error
        }
      }
      const sent = await doOrQueue('/activities', { type, caption: { [lang]: caption.trim() }, student_ids: [], media_ids: mediaIds }, 'feed post')
      setCaption('')
      setPhoto(null)
      setMessage({ text: sent === 'done' ? t('posted') : photoSkipped ? `${t('savedForLater')} ${t('postPhotoNeedsSignal')}` : t('savedForLater'), tone: 'info' })
      await load()
    } catch (error) {
      setMessage({ text: error instanceof ApiError ? error.message : t('retry'), tone: 'danger' })
    } finally {
      setBusy(false)
    }
  }

  return (
    <Screen refreshing={busy} onRefresh={() => void load()}>
      <Hero title={t('tabs.feed')} icon="images" />
      {offline && <Banner tone="info" text={t('updatedOffline')} />}
      {message && <Banner tone={message.tone} text={message.text} />}
      {can('activities.write') && <Card>
        <Heading>{t('newPost')}</Heading>
        <Body muted>{t('postType')}</Body>
        <Row>{TYPES.map((k) => <Chip key={k} active={k === type} label={t(`types.${k}`)} onPress={() => setType(k)} />)}</Row>
        <Field label={t('caption')} value={caption} onChangeText={setCaption} multiline numberOfLines={3} style={{ minHeight: 80 }} />
        <Body muted>{photo ? t('obsPhotoTaken') : t('obsPhoto')}</Body>
        <Row><Btn kind="secondary" icon="camera" label={t('takePhoto')} onPress={() => void pickPhoto(true)} /><Btn kind="secondary" icon="image" label={t('choosePhoto')} onPress={() => void pickPhoto(false)} /></Row>
        {photo && <Image accessibilityLabel={t('obsPhotoTaken')} source={{ uri: photo.uri }} style={{ width: '100%', height: 160, borderRadius: 10 }} resizeMode="cover" />}
        <Btn icon="send" label={t('post')} onPress={() => void post()} busy={busy} disabled={!caption.trim()} />
      </Card>}
      {posts.length === 0 ? <Empty icon="images-outline" text={t('noPosts')} /> : posts.map((p) => (
        <Card key={p.id}>
          <Row><Pill tone="info" label={t(`types.${p.type as (typeof TYPES)[number]}`)} /><Pill tone={p.status === 'approved' ? 'ok' : p.status === 'rejected' ? 'danger' : 'warn'} label={t(`approval.${p.status}`)} /></Row>
          <Body>{pick(p.caption, lang)}</Body>
          {(p.media ?? []).filter((m) => m.content_type.startsWith('image/')).map((m) => <StoredPhoto key={m.id} id={m.id} label={t('tabs.feed')} style={{ width: '100%', height: 200, borderRadius: 10 }} />)}
          <View><Body muted>{p.posted_by_name ?? ''} · {new Date(p.created_at).toLocaleString()}</Body></View>
        </Card>
      ))}
    </Screen>
  )
}

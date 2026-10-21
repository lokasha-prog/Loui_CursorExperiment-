import * as ImagePicker from 'expo-image-picker'
import { useEffect, useState } from 'react'
import { Switch, View } from 'react-native'
import { api, ApiError, upload } from '../api'
import { cached } from '../cache'
import { useI18n } from '../i18n'
import { doOrQueue } from '../queue'
import { Banner, Body, Btn, Card, Field, Heading, Row, Screen, Title } from '../ui'

interface Child { id: string; first_name: string; last_name: string }
const TYPES = ['informal', 'formal', 'concern'] as const

/** A teacher records what a child did in a few taps: pick the child, say what happened, add a photo if there is signal. Saved on the phone when there is none. */
export function ObserveScreen() {
  const { t, lang } = useI18n()
  const [children, setChildren] = useState<Child[]>([])
  const [search, setSearch] = useState('')
  const [child, setChild] = useState<Child | null>(null)
  const [type, setType] = useState<(typeof TYPES)[number]>('informal')
  const [notes, setNotes] = useState('')
  const [share, setShare] = useState(false)
  const [photo, setPhoto] = useState<ImagePicker.ImagePickerAsset | null>(null)
  const [busy, setBusy] = useState(false)
  const [message, setMessage] = useState<{ text: string; tone: 'info' | 'warn' | 'danger' } | null>(null)

  useEffect(() => { void cached('children.staff', () => api.page<Child>('/students?limit=100&status=active')).then((r) => setChildren(r.data ?? [])) }, [])

  const shown = children.filter((c) => `${c.first_name} ${c.last_name}`.toLowerCase().includes(search.toLowerCase())).slice(0, 8)

  const pickPhoto = async (camera: boolean) => {
    const permission = camera ? await ImagePicker.requestCameraPermissionsAsync() : await ImagePicker.requestMediaLibraryPermissionsAsync()
    if (!permission.granted) return
    const result = camera ? await ImagePicker.launchCameraAsync({ quality: 0.6 }) : await ImagePicker.launchImageLibraryAsync({ quality: 0.6 })
    if (!result.canceled) setPhoto(result.assets[0])
  }

  const save = async () => {
    if (!child || !notes.trim()) return
    setBusy(true)
    setMessage(null)
    try {
      let mediaIds: string[] = []
      let photoSkipped = false
      if (photo) {
        try {
          const media = await upload<{ id: string }>('/media', photo.uri, photo.fileName ?? 'photo.jpg', photo.mimeType ?? 'image/jpeg')
          mediaIds = [media.id]
        } catch (error) {
          if (error instanceof ApiError && error.status === 0) photoSkipped = true
          else throw error
        }
      }
      const result = await doOrQueue('/learning/observations', { student_id: child.id, type, notes: { [lang]: notes.trim() }, share_with_parents: share && type !== 'concern', media_ids: mediaIds }, `observation ${child.first_name}`)
      setMessage({ text: result === 'done' ? t('obsSaved') : photoSkipped || photo ? `${t('obsQueued')} ${t('obsPhotoNeedsSignal')}` : t('obsQueued'), tone: result === 'done' ? 'info' : 'warn' })
      setNotes('')
      setPhoto(null)
    } catch (error) {
      setMessage({ text: error instanceof ApiError ? error.message : t('retry'), tone: 'danger' })
    } finally {
      setBusy(false)
    }
  }

  return (
    <Screen>
      <Title>{t('observe')}</Title>
      {message && <Banner tone={message.tone} text={message.text} />}
      <Card>
        <Heading>{t('pickChild')}</Heading>
        {child ? (
          <Row><Body bold>{child.first_name} {child.last_name}</Body><Btn kind="secondary" label={t('cancel')} onPress={() => setChild(null)} /></Row>
        ) : (
          <>
            <Field label={t('search')} value={search} onChangeText={setSearch} />
            {shown.map((c) => <Btn key={c.id} kind="secondary" label={`${c.first_name} ${c.last_name}`} onPress={() => setChild(c)} />)}
          </>
        )}
      </Card>
      {child && (
        <Card>
          <Body muted>{t('obsType')}</Body>
          <Row>{TYPES.map((k) => <Btn key={k} kind={k === type ? 'primary' : 'secondary'} label={t(`obsTypes.${k}`)} onPress={() => setType(k)} />)}</Row>
          <Field label={t('obsNotes')} value={notes} onChangeText={setNotes} multiline numberOfLines={4} style={{ minHeight: 100 }} />
          <Row>
            <Body>{type === 'concern' ? t('obsConcernPrivate') : t('obsShare')}</Body>
            <Switch accessibilityLabel={t('obsShare')} value={share && type !== 'concern'} disabled={type === 'concern'} onValueChange={setShare} />
          </Row>
          <View style={{ gap: 8 }}>
            <Body muted>{photo ? t('obsPhotoTaken') : t('obsPhoto')}</Body>
            <Row><Btn kind="secondary" label={t('takePhoto')} onPress={() => void pickPhoto(true)} /><Btn kind="secondary" label={t('choosePhoto')} onPress={() => void pickPhoto(false)} /></Row>
          </View>
          <Btn label={t('obsSave')} onPress={() => void save()} busy={busy} disabled={!notes.trim()} />
        </Card>
      )}
    </Screen>
  )
}

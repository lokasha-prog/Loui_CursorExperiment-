import { useCallback, useEffect, useState } from 'react'
import { Pressable } from 'react-native'
import { api, ApiError } from '../api'
import { cached } from '../cache'
import { doOrQueue } from '../queue'
import { useAuth } from '../auth'
import { useI18n } from '../i18n'
import { Banner, Body, Btn, Card, Chip, Empty, Field, Heading, Hero, Pill, Row, Screen } from '../ui'

interface Child { id: string; first_name: string; last_name: string }
interface Thread { id: string; subject: string; last_message_preview: string | null; last_sender_name: string | null; unread: boolean }
interface Detail { id: string; subject: string; can_reply: boolean; messages: { id: string; sender_name: string | null; from_staff: boolean; body: string; created_at: string }[] }

export function MessagesScreen() {
  const { t } = useI18n()
  const { can, me } = useAuth()
  const [threads, setThreads] = useState<Thread[]>([])
  const [open, setOpen] = useState<Detail | null>(null)
  const [text, setText] = useState('')
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [notice, setNotice] = useState<string | null>(null)
  const [composing, setComposing] = useState(false)
  const [subject, setSubject] = useState('')
  const [children, setChildren] = useState<Child[]>([])
  const [aboutChild, setAboutChild] = useState('')
  const parent = !!me?.roles.includes('parent') && me.roles.length === 1 && can('messaging.write')

  const load = useCallback(async () => {
    const r = await cached('threads', () => api.page<Thread>('/messages/threads?limit=50'))
    setThreads(r.data ?? [])
    if (parent) {
      const kids = await cached('children', () => api.page<Child>('/students?limit=100'))
      setChildren(kids.data ?? [])
      setAboutChild((c) => c || kids.data?.[0]?.id || '')
    }
  }, [parent])
  useEffect(() => { void load() }, [load])

  const show = async (id: string) => {
    const r = await cached(`thread.${id}`, () => api.get<Detail>(`/messages/threads/${id}`))
    if (r.data) setOpen(r.data)
  }

  const send = async () => {
    if (!open) return
    setBusy(true)
    try {
      const sent = await doOrQueue(`/messages/threads/${open.id}/messages`, { body: text.trim() }, 'reply')
      setText('')
      if (sent === 'done') await show(open.id)
      else setError(t('savedForLater'))
    } catch (e) {
      setError(e instanceof ApiError ? e.message : t('retry'))
    } finally {
      setBusy(false)
    }
  }

  if (open) {
    return (
      <Screen onBack={() => { setOpen(null); void load() }} backLabel={t('back')}>
        <Hero title={open.subject} icon="chatbubbles" />
        {error && <Banner tone="danger" text={error} />}
        {open.messages.map((m) => (
          <Card key={m.id} style={{ backgroundColor: m.from_staff ? '#E6F4F5' : '#FFFFFF' }}>
            <Body bold>{m.sender_name ?? ''}</Body>
            <Body>{m.body}</Body>
            <Body muted>{new Date(m.created_at).toLocaleString()}</Body>
          </Card>
        ))}
        {open.can_reply && (
          <Card>
            <Field label={t('typeMessage')} value={text} onChangeText={setText} multiline style={{ minHeight: 70 }} />
            <Btn label={t('send')} onPress={() => void send()} busy={busy} disabled={!text.trim()} />
          </Card>
        )}
      </Screen>
    )
  }

  const startThread = async () => {
    setBusy(true)
    setError(null)
    try {
      const sent = await doOrQueue('/messages/threads', { subject: subject.trim(), body: text.trim(), student_id: aboutChild || null, broadcast: false }, 'new message')
      setSubject('')
      setText('')
      setComposing(false)
      setNotice(sent === 'done' ? t('messageSent') : t('savedForLater'))
      await load()
    } catch (e) {
      setError(e instanceof ApiError ? e.message : t('retry'))
    } finally {
      setBusy(false)
    }
  }

  if (composing) {
    return (
      <Screen onBack={() => setComposing(false)} backLabel={t('back')}>
        <Hero title={t('newMessage')} icon="create" />
        {error && <Banner tone="danger" text={error} />}
        <Card>
          {children.length > 1 && <>
            <Body muted>{t('aboutChild')}</Body>
            <Row>{children.map((c) => <Chip key={c.id} active={c.id === aboutChild} label={c.first_name} onPress={() => setAboutChild(c.id)} />)}</Row>
          </>}
          <Field label={t('subject')} value={subject} onChangeText={setSubject} maxLength={200} />
          <Field label={t('typeMessage')} value={text} onChangeText={setText} multiline style={{ minHeight: 100 }} />
          <Btn icon="send" label={t('send')} onPress={() => void startThread()} busy={busy} disabled={!subject.trim() || !text.trim()} />
        </Card>
      </Screen>
    )
  }

  return (
    <Screen refreshing={busy} onRefresh={() => void load()}>
      <Hero title={t('messages')} icon="chatbubbles" />
      {notice && <Banner tone="info" text={notice} />}
      {parent && <Btn icon="create" label={t('newMessage')} onPress={() => { setNotice(null); setComposing(true) }} />}
      {threads.length === 0 ? <Empty icon="chatbubble-outline" text={t('noMessages')} /> : threads.map((th) => (
        <Pressable key={th.id} accessibilityRole="button" onPress={() => void show(th.id)}>
          <Card>
            <Row><Heading>{th.subject}</Heading>{th.unread && <Pill tone="info" label={t('unread')} />}</Row>
            <Body muted>{th.last_sender_name ? `${th.last_sender_name}: ` : ''}{th.last_message_preview ?? ''}</Body>
          </Card>
        </Pressable>
      ))}
    </Screen>
  )
}

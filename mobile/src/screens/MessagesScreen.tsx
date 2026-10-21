import { useCallback, useEffect, useState } from 'react'
import { Pressable } from 'react-native'
import { api, ApiError } from '../api'
import { useI18n } from '../i18n'
import { Banner, Body, Btn, Card, Field, Heading, Pill, Row, Screen, Title } from '../ui'

interface Thread { id: string; subject: string; last_message_preview: string | null; last_sender_name: string | null; unread: boolean }
interface Detail { id: string; subject: string; can_reply: boolean; messages: { id: string; sender_name: string | null; from_staff: boolean; body: string; created_at: string }[] }

export function MessagesScreen() {
  const { t } = useI18n()
  const [threads, setThreads] = useState<Thread[]>([])
  const [open, setOpen] = useState<Detail | null>(null)
  const [text, setText] = useState('')
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const load = useCallback(async () => {
    try { setThreads(await api.page<Thread>('/messages/threads?limit=50')) } catch { /* offline */ }
  }, [])
  useEffect(() => { void load() }, [load])

  const show = async (id: string) => setOpen(await api.get<Detail>(`/messages/threads/${id}`))

  const send = async () => {
    if (!open) return
    setBusy(true)
    try {
      await api.post(`/messages/threads/${open.id}/messages`, { body: text.trim() })
      setText('')
      await show(open.id)
    } catch (e) {
      setError(e instanceof ApiError ? e.message : t('retry'))
    } finally {
      setBusy(false)
    }
  }

  if (open) {
    return (
      <Screen>
        <Btn kind="secondary" label={t('back')} onPress={() => { setOpen(null); void load() }} />
        <Title>{open.subject}</Title>
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

  return (
    <Screen refreshing={busy} onRefresh={() => void load()}>
      <Title>{t('messages')}</Title>
      {threads.length === 0 ? <Body muted>{t('noMessages')}</Body> : threads.map((th) => (
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

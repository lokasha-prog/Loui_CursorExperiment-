import { Eraser, MessagesSquare, Send, ShieldCheck, X } from 'lucide-react'
import { useEffect, useId, useRef, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { useDataChat, type DataChatReply } from '@/api/hooks'
import { Button } from '@/components/ui/button'
import { Textarea } from '@/components/ui/form-controls'
import { Alert } from '@/components/ui/surfaces'
import { can, useAuth } from '@/stores/auth'
import { aiErrorText } from './aiError'

interface Turn {
  role: 'user' | 'assistant'
  content: string
  reply?: DataChatReply
}

/** Earlier messages sent with each question so follow-ups ("and in Busy Bees?") make sense; the server takes up to 12. */
const HISTORY = 12

/**
 * "Chat with data" on every screen for the super administrator: a button above the feedback button opens a panel; the
 * conversation lives in the app shell, so it stays while moving between pages. The AI Brain answers from counts it looks
 * up (never names).
 */
export function DataChatWidget() {
  const me = useAuth((s) => s.me)
  if (!can(me, 'ai.chat')) return null
  return <DataChat />
}

function DataChat() {
  const { t, i18n } = useTranslation()
  const chat = useDataChat()
  const [open, setOpen] = useState(false)
  const [turns, setTurns] = useState<Turn[]>([])
  const [question, setQuestion] = useState('')
  const launcher = useRef<HTMLButtonElement>(null)
  const input = useRef<HTMLTextAreaElement>(null)
  const end = useRef<HTMLDivElement>(null)
  const panelId = useId()
  const titleId = useId()
  const language = i18n.language === 'ar' ? 'ar' : 'en'
  const suggestions = ['girlsPresent', 'teachersOnLeave', 'concerns', 'overdue', 'branches'].map((key) => t(`dataChat.examples.${key}`))

  useEffect(() => {
    if (!open) return
    input.current?.focus()
    // Escape closes the panel wherever focus is, unless something else (an open dialog) already used it.
    const onKey = (e: KeyboardEvent) => {
      if (e.key === 'Escape' && !e.defaultPrevented) close()
    }
    document.addEventListener('keydown', onKey)
    return () => document.removeEventListener('keydown', onKey)
  }, [open])

  useEffect(() => {
    if (open) end.current?.scrollIntoView?.({ block: 'end' })
  }, [open, turns, chat.isPending])

  const close = () => {
    setOpen(false)
    launcher.current?.focus()
  }

  const ask = async (text: string) => {
    const asked = text.trim()
    if (!asked || chat.isPending) return
    const history = turns.slice(-HISTORY).map(({ role, content }) => ({ role, content }))
    setTurns((all) => [...all, { role: 'user', content: asked }])
    setQuestion('')
    input.current?.focus()
    try {
      const reply = await chat.mutateAsync({ question: asked, history, language })
      setTurns((all) => [...all, { role: 'assistant', content: reply.answer, reply }])
    } catch {
      // The error is shown under the conversation; the question stays in the transcript so it can be asked again.
    }
  }

  return (
    <>
      {open && (
        <section
          id={panelId}
          role="dialog"
          aria-modal="false"
          aria-labelledby={titleId}
          className="fixed inset-x-4 bottom-28 z-40 flex h-[min(36rem,calc(100dvh-9rem))] flex-col overflow-hidden rounded-lg border bg-white shadow-xl sm:inset-x-auto sm:end-4 sm:w-[26rem]"
        >
          <header className="flex items-center gap-2 border-b px-4 py-3">
            <MessagesSquare className="h-5 w-5 shrink-0 text-primary" aria-hidden="true" />
            <h2 id={titleId} className="flex-1 font-bold">{t('dataChat.title')}</h2>
            {turns.length > 0 && (
              <Button variant="ghost" size="sm" onClick={() => { setTurns([]); chat.reset(); input.current?.focus() }}>
                <Eraser className="h-4 w-4" aria-hidden="true" />
                <span className="sr-only">{t('dataChat.clear')}</span>
              </Button>
            )}
            <Button variant="ghost" size="sm" onClick={close}>
              <X className="h-4 w-4" aria-hidden="true" />
              <span className="sr-only">{t('common.close')}</span>
            </Button>
          </header>

          <div role="log" aria-live="polite" aria-label={t('dataChat.conversation')} className="flex-1 space-y-3 overflow-y-auto p-4">
            {turns.length === 0 && (
              <div className="py-2 text-center">
                <p className="font-semibold">{t('dataChat.emptyTitle')}</p>
                <p className="mb-3 text-sm text-muted-foreground">{t('dataChat.emptyBody')}</p>
                <ul className="flex flex-col items-stretch gap-2" aria-label={t('dataChat.examplesLabel')}>
                  {suggestions.map((s) => (
                    <li key={s}>
                      <Button type="button" variant="secondary" size="sm" className="w-full justify-start text-start" onClick={() => void ask(s)}>{s}</Button>
                    </li>
                  ))}
                </ul>
              </div>
            )}

            {turns.map((turn, i) => (
              <div key={i} className={turn.role === 'user' ? 'flex justify-end' : 'flex justify-start'}>
                <div className={turn.role === 'user' ? 'max-w-[85%] rounded-lg bg-primary px-3 py-2 text-sm text-primary-foreground' : 'max-w-[90%] rounded-lg border bg-white px-3 py-2 text-sm'}>
                  <p className="sr-only">{turn.role === 'user' ? t('dataChat.you') : t('dataChat.assistant')}</p>
                  <p className="whitespace-pre-line leading-relaxed" dir="auto">{turn.content}</p>
                  {turn.reply && turn.reply.lookups.length > 0 && (
                    <details className="mt-2 text-xs text-muted-foreground">
                      <summary className="cursor-pointer select-none font-semibold">{t('dataChat.howWorkedOut', { count: turn.reply.lookups.length })}</summary>
                      <ul className="mt-2 space-y-1" dir="ltr">
                        {turn.reply.lookups.map((l, j) => (
                          <li key={j} className="break-words font-mono text-[11px]">{l.tool}({l.arguments === '{}' ? '' : l.arguments}) → {l.result}</li>
                        ))}
                      </ul>
                    </details>
                  )}
                </div>
              </div>
            ))}

            {chat.isPending && <p className="text-sm text-muted-foreground">{t('dataChat.thinking')}</p>}
            {chat.isError && <Alert tone="danger">{aiErrorText(chat.error, t)}</Alert>}
            <div ref={end} />
          </div>

          <form
            className="flex items-end gap-2 border-t p-3"
            onSubmit={(e) => {
              e.preventDefault()
              void ask(question)
            }}
          >
            <label htmlFor={`${panelId}-question`} className="sr-only">{t('dataChat.questionLabel')}</label>
            <Textarea
              ref={input}
              id={`${panelId}-question`}
              rows={2}
              maxLength={1000}
              value={question}
              placeholder={t('dataChat.placeholder')}
              onChange={(e) => setQuestion(e.target.value)}
              onKeyDown={(e) => {
                if (e.key === 'Enter' && !e.shiftKey) {
                  e.preventDefault()
                  void ask(question)
                }
              }}
              className="flex-1 resize-none"
            />
            <Button type="submit" disabled={chat.isPending || !question.trim()}>
              <Send className="h-4 w-4 rtl:rotate-180" aria-hidden="true" />
              {t('dataChat.send')}
            </Button>
          </form>
          <p className="flex items-start gap-1.5 border-t bg-muted/40 px-3 py-2 text-[11px] text-muted-foreground">
            <ShieldCheck className="mt-0.5 h-3.5 w-3.5 shrink-0" aria-hidden="true" />
            {t('dataChat.privacy')} {t('ai.disclaimer')}
          </p>
        </section>
      )}

      <Button
        ref={launcher}
        size="sm"
        aria-expanded={open}
        aria-controls={open ? panelId : undefined}
        onClick={() => (open ? close() : setOpen(true))}
        className="fixed bottom-16 end-4 z-30 rounded-full shadow-md"
      >
        {open ? <X className="h-4 w-4" aria-hidden="true" /> : <MessagesSquare className="h-4 w-4" aria-hidden="true" />}
        {t('dataChat.title')}
      </Button>
    </>
  )
}

import { Bold, Heading2, Italic, Link2, List, ListOrdered, Underline } from 'lucide-react'
import { useEffect, useRef } from 'react'
import { useTranslation } from 'react-i18next'
import { Button } from '@/components/ui/button'
import { clean } from './SafeHtml'

/**
 * A small rich-text box: bold, italic, underline, heading, lists and links. It keeps simple HTML; the server cleans it again before saving,
 * so nothing unsafe can get through. Change the `key` to replace the text from outside (for example with a draft).
 */
export function RichTextEditor({ id, label, value, onChange, dir }: { id: string; label: string; value: string; onChange: (html: string) => void; dir?: 'rtl' | 'ltr' }) {
  const { t } = useTranslation()
  const box = useRef<HTMLDivElement>(null)
  // The starting text is put in once, cleaned; after that the person types and the text is read back out.
  useEffect(() => {
    if (box.current) box.current.innerHTML = clean(value)
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [])
  const run = (command: string, argument?: string) => {
    box.current?.focus()
    document.execCommand(command, false, argument)
    onChange(box.current?.innerHTML ?? '')
  }
  const link = () => {
    const url = window.prompt(t('richText.linkPrompt'), 'https://')
    if (url && /^(https?:\/\/|mailto:)/i.test(url)) run('createLink', url)
  }

  return (
    <div className="rounded-md border bg-white">
      <div role="toolbar" aria-label={t('richText.toolbar')} className="flex flex-wrap gap-1 border-b p-1">
        <Button type="button" size="sm" variant="ghost" aria-label={t('richText.bold')} onClick={() => run('bold')}><Bold className="h-4 w-4" aria-hidden="true" /></Button>
        <Button type="button" size="sm" variant="ghost" aria-label={t('richText.italic')} onClick={() => run('italic')}><Italic className="h-4 w-4" aria-hidden="true" /></Button>
        <Button type="button" size="sm" variant="ghost" aria-label={t('richText.underline')} onClick={() => run('underline')}><Underline className="h-4 w-4" aria-hidden="true" /></Button>
        <Button type="button" size="sm" variant="ghost" aria-label={t('richText.heading')} onClick={() => run('formatBlock', 'h2')}><Heading2 className="h-4 w-4" aria-hidden="true" /></Button>
        <Button type="button" size="sm" variant="ghost" aria-label={t('richText.bullets')} onClick={() => run('insertUnorderedList')}><List className="h-4 w-4" aria-hidden="true" /></Button>
        <Button type="button" size="sm" variant="ghost" aria-label={t('richText.numbers')} onClick={() => run('insertOrderedList')}><ListOrdered className="h-4 w-4" aria-hidden="true" /></Button>
        <Button type="button" size="sm" variant="ghost" aria-label={t('richText.link')} onClick={link}><Link2 className="h-4 w-4" aria-hidden="true" /></Button>
      </div>
      <div
        id={id}
        ref={box}
        role="textbox"
        aria-multiline="true"
        aria-label={label}
        contentEditable
        suppressContentEditableWarning
        dir={dir}
        className="prose prose-sm min-h-40 max-w-none p-3 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring [&_a]:text-primary-dark [&_a]:underline [&_h2]:text-lg [&_h2]:font-bold [&_ol]:list-decimal [&_ol]:ps-6 [&_ul]:list-disc [&_ul]:ps-6"
        onInput={(e) => onChange((e.target as HTMLDivElement).innerHTML)}
      />
    </div>
  )
}

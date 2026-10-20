import DOMPurify from 'dompurify'
import { useMemo } from 'react'

/** The only formatting newsletters may contain. The server cleans it too; this is the second lock on the door. */
export const SAFE_HTML = { ALLOWED_TAGS: ['p', 'br', 'strong', 'b', 'em', 'i', 'u', 'ul', 'ol', 'li', 'h2', 'h3', 'h4', 'blockquote', 'a'], ALLOWED_ATTR: ['href', 'target', 'rel'] }

export const clean = (html: string) => DOMPurify.sanitize(html, SAFE_HTML)

/** Shows trusted-but-checked HTML (a newsletter) after cleaning it with DOMPurify. */
export function SafeHtml({ html, className, dir }: { html: string; className?: string; dir?: 'rtl' | 'ltr' }) {
  const safe = useMemo(() => clean(html), [html])
  // eslint-disable-next-line no-restricted-syntax -- cleaned with DOMPurify just above
  return <article className={className} dir={dir} dangerouslySetInnerHTML={{ __html: safe }} />
}

import { ChevronDown, Sparkles } from 'lucide-react'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { PageHeader } from '@/components/layout/PageHeader'
import { Button } from '@/components/ui/button'
import { Badge } from '@/components/ui/surfaces'
import { formatDate } from '@/lib/format'
import { releases, type Release } from './releases'

function ReleaseCard({ release, current }: { release: Release; current: boolean }) {
  const { t, i18n } = useTranslation()
  const date = /^\d{4}-\d{2}-\d{2}$/.test(release.label) ? formatDate(release.label, i18n.language) : release.label
  return (
    <article className="nursery-card p-5" aria-labelledby={`release-${release.version}`}>
      <div className="mb-2 flex flex-wrap items-center gap-2">
        <h2 id={`release-${release.version}`} className="font-heading text-lg font-semibold" dir="ltr">
          {t('releaseNotes.version', { version: release.version })}
        </h2>
        {current && <Badge tone="success">{t('releaseNotes.current')}</Badge>}
        {release.preRelease && <Badge tone="neutral">{t('releaseNotes.preRelease')}</Badge>}
        <span className="ms-auto text-xs text-muted-foreground" dir={release.preRelease ? 'ltr' : undefined}>{date}</span>
      </div>
      {release.summary && <p className="mb-2 text-sm text-muted-foreground" dir="ltr" lang="en">{release.summary}</p>}
      <ul className="list-disc space-y-1 ps-5 text-sm" dir="ltr" lang="en">
        {release.changes.map((change) => <li key={change}>{change}</li>)}
      </ul>
    </article>
  )
}

/** Every version of the product and what changed in it, from CHANGELOG.md (ADR-0009). Open to everyone signed in. */
export default function ReleaseNotesPage() {
  const { t, i18n } = useTranslation()
  const [showEarlier, setShowEarlier] = useState(false)
  const numbered = releases.filter((r) => !r.preRelease)
  const earlier = releases.filter((r) => r.preRelease)

  return (
    <>
      <PageHeader title={t('releaseNotes.title')} subtitle={t('releaseNotes.subtitle', { version: __APP_VERSION__ })} />
      {i18n.language === 'ar' && <p className="mb-4 text-xs text-muted-foreground">{t('releaseNotes.englishOnly')}</p>}
      <div className="space-y-4">
        {numbered.map((release) => <ReleaseCard key={release.version} release={release} current={release.version === __APP_VERSION__} />)}
      </div>
      {earlier.length > 0 && (
        <section className="mt-8">
          <div className="mb-3 flex flex-wrap items-center gap-3">
            <Sparkles className="h-4 w-4 text-primary" aria-hidden="true" />
            <h2 className="font-heading text-base font-semibold">{t('releaseNotes.earlierTitle')}</h2>
            <Button variant="secondary" size="sm" aria-expanded={showEarlier} aria-controls="earlier-releases" onClick={() => setShowEarlier((v) => !v)}>
              <ChevronDown className={`h-3.5 w-3.5 transition-transform ${showEarlier ? 'rotate-180' : ''}`} aria-hidden="true" />
              {showEarlier ? t('releaseNotes.hideEarlier') : t('releaseNotes.showEarlier', { count: earlier.length })}
            </Button>
          </div>
          <p className="mb-4 text-xs text-muted-foreground">{t('releaseNotes.earlierNote')}</p>
          {showEarlier && (
            <div id="earlier-releases" className="space-y-4">
              {earlier.map((release) => <ReleaseCard key={release.version} release={release} current={false} />)}
            </div>
          )}
        </section>
      )}
    </>
  )
}

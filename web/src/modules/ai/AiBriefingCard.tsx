import { Sparkles } from 'lucide-react'
import { useTranslation } from 'react-i18next'
import { ApiError } from '@/api/client'
import { useBriefing } from '@/api/hooks'
import type { BriefingArea } from '@/api/types'
import { Button } from '@/components/ui/button'
import { Alert, Card } from '@/components/ui/surfaces'
import { formatDate } from '@/lib/format'

/** An AI Brain briefing for management on one area. The figures it was written from are listed so the advice can be checked. */
export function AiBriefingCard({ area }: { area: BriefingArea }) {
  const { t, i18n } = useTranslation()
  const briefing = useBriefing(area)
  const language = i18n.language === 'ar' ? 'ar' : 'en'
  const result = briefing.data

  return (
    <Card className="mb-6 bg-gradient-to-br from-accent to-white p-5">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div className="flex items-start gap-3">
          <span className="flex h-10 w-10 shrink-0 items-center justify-center rounded-xl bg-white shadow-sm">
            <Sparkles className="h-5 w-5 text-primary-dark" aria-hidden="true" />
          </span>
          <div>
            <h2 className="font-heading text-lg font-semibold">{t(`briefing.${area}.title`)}</h2>
            <p className="text-sm text-muted-foreground">{t(`briefing.${area}.subtitle`)}</p>
          </div>
        </div>
        <Button onClick={() => briefing.mutate(language)} disabled={briefing.isPending}>
          {briefing.isPending ? t('ai.working') : result ? t('ai.again') : t('briefing.run')}
        </Button>
      </div>

      <div aria-live="polite" className="mt-4">
        {briefing.isError && (
          <Alert tone="danger">{briefing.error instanceof ApiError && briefing.error.status === 422 ? t('ai.down') : t('errors.UNKNOWN')}</Alert>
        )}
        {result && (
          <div className="rounded-md border bg-white p-4">
            <p className="whitespace-pre-line text-sm leading-relaxed" dir={result.language === 'ar' ? 'rtl' : 'ltr'}>{result.summary}</p>
            <details className="mt-3 text-xs text-muted-foreground">
              <summary className="cursor-pointer font-semibold">{t('briefing.figures')}</summary>
              <dl className="mt-2 grid gap-x-6 gap-y-1 sm:grid-cols-2">
                {result.facts.map((f) => (
                  <div key={f.label} className="flex justify-between gap-3 border-b border-dashed py-1">
                    <dt>{f.label}</dt>
                    <dd className="font-semibold text-foreground" dir="ltr">{f.value}</dd>
                  </div>
                ))}
              </dl>
            </details>
            <p className="mt-2 text-xs text-muted-foreground">
              {t('ai.made', { model: result.model, when: formatDate(result.generated_at, i18n.language) })} · {t('ai.disclaimer')}
            </p>
          </div>
        )}
      </div>
      <p className="mt-3 text-xs text-muted-foreground">{t('briefing.privacy')}</p>
    </Card>
  )
}

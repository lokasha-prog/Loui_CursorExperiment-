import { Sparkles } from 'lucide-react'
import { useTranslation } from 'react-i18next'
import { aiErrorText } from './aiError'
import { useAttendanceInsights } from '@/api/hooks'
import { Button } from '@/components/ui/button'
import { Alert, Card } from '@/components/ui/surfaces'
import { formatDate } from '@/lib/format'

const DAYS = 14

/** The first AI Brain feature: a manager asks for a written summary of recent attendance. */
export function AiInsightsCard() {
  const { t, i18n } = useTranslation()
  const insights = useAttendanceInsights()
  const language = i18n.language === 'ar' ? 'ar' : 'en'
  const result = insights.data

  return (
    <Card className="mb-8 bg-gradient-to-br from-accent to-white p-5">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div className="flex items-start gap-3">
          <span className="flex h-10 w-10 shrink-0 items-center justify-center rounded-xl bg-white shadow-sm">
            <Sparkles className="h-5 w-5 text-primary-dark" aria-hidden="true" />
          </span>
          <div>
            <h2 className="font-heading text-lg font-semibold">{t('ai.title')}</h2>
            <p className="text-sm text-muted-foreground">{t('ai.subtitle')} {t('ai.last', { days: DAYS })}</p>
          </div>
        </div>
        <Button onClick={() => insights.mutate({ days: DAYS, language })} disabled={insights.isPending}>
          {insights.isPending ? t('ai.working') : result ? t('ai.again') : t('ai.run')}
        </Button>
      </div>

      <div aria-live="polite" className="mt-4">
        {insights.isError && (
          <Alert tone="danger">
            {aiErrorText(insights.error, t)}
          </Alert>
        )}
        {result && (
          <div className="rounded-md border bg-white p-4">
            <p className="whitespace-pre-line text-sm leading-relaxed" dir={result.language === 'ar' ? 'rtl' : 'ltr'}>{result.summary}</p>
            <p className="mt-3 text-xs text-muted-foreground">
              {t('ai.extra', { shortages: result.shortage_events, leaves: result.approved_leave_requests })}
            </p>
            <p className="mt-1 text-xs text-muted-foreground">
              {t('ai.made', { model: result.model, when: formatDate(result.generated_at, i18n.language) })} · {t('ai.disclaimer')}
            </p>
          </div>
        )}
      </div>
      <p className="mt-3 text-xs text-muted-foreground">{t('ai.privacy')}</p>
    </Card>
  )
}

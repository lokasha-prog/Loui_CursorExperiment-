import { useTranslation } from 'react-i18next'
import { Button } from '@/components/ui/button'
import { Alert, Skeleton } from '@/components/ui/surfaces'
import { useApiErrorMessage } from '@/hooks/use-api-error'

export function ListSkeleton({ rows = 5 }: { rows?: number }) {
  const { t } = useTranslation()
  return (
    <div role="status" aria-label={t('common.loading')} className="space-y-3 p-4">
      {Array.from({ length: rows }, (_, i) => (
        <Skeleton key={i} className="h-12 w-full" />
      ))}
    </div>
  )
}

export function ListError({ error, onRetry }: { error: unknown; onRetry: () => void }) {
  const { t } = useTranslation()
  const message = useApiErrorMessage()
  return (
    <div className="space-y-3 p-4">
      <Alert>{message(error)}</Alert>
      <Button variant="secondary" size="sm" onClick={onRetry}>
        {t('common.retry')}
      </Button>
    </div>
  )
}

export function LoadMore({ visible, loading, onClick }: { visible: boolean; loading: boolean; onClick: () => void }) {
  const { t } = useTranslation()
  if (!visible) return null
  return (
    <div className="flex justify-center border-t p-3">
      <Button variant="ghost" size="sm" onClick={onClick} disabled={loading}>
        {loading ? t('common.loading') : t('common.loadMore')}
      </Button>
    </div>
  )
}

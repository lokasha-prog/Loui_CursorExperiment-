import { useTranslation } from 'react-i18next'
import { Badge, type Tone } from '@/components/ui/surfaces'

const tones: Record<string, Tone> = {
  active: 'success',
  signed_up: 'info',
  on_hold: 'warning',
  on_leave: 'warning',
  suspended: 'warning',
  invited: 'info',
  inactive: 'neutral',
  withdrawn: 'neutral',
  transferred: 'purple',
  terminated: 'danger',
}

/** Status pill with a coloured dot; the text always comes from the translation files. */
export function StatusBadge({ status, labelKey }: { status: string; labelKey: string }) {
  const { t } = useTranslation()
  return (
    <Badge tone={tones[status] ?? 'neutral'}>
      <span aria-hidden="true" className="me-1.5 inline-block h-1.5 w-1.5 rounded-full bg-current" />
      {t(`${labelKey}.${status}`)}
    </Badge>
  )
}

import { AlertTriangle, CheckCircle2 } from 'lucide-react'
import { useTranslation } from 'react-i18next'
import { useLiveRatios } from '@/api/hooks'
import { Card, Skeleton } from '@/components/ui/surfaces'
import { cn, localized } from '@/lib/utils'

/** Live staff-to-children cover for every room. A short room is shown with an icon and words, never colour alone. */
export function RatioCards({ enabled = true }: { enabled?: boolean }) {
  const { t, i18n } = useTranslation()
  const ratios = useLiveRatios(enabled)

  if (ratios.isLoading) return <Skeleton className="h-24" />
  if (ratios.isError) return <p className="text-sm text-muted-foreground">{t('attendance.ratio.unavailable')}</p>
  if (!ratios.data?.length) return <p className="text-sm text-muted-foreground">{t('attendance.ratio.noRooms')}</p>

  return (
    <ul className="grid gap-3 sm:grid-cols-2 xl:grid-cols-3" aria-label={t('attendance.ratio.title')}>
      {ratios.data.map((room) => (
        <li key={room.room_id}>
          <Card className={cn('p-4', room.is_breach && 'border-red-300 bg-red-50/60')}>
            <div className="flex items-start justify-between gap-2">
              <p className="font-heading font-semibold">{localized(room.name, i18n.language)}</p>
              {room.is_breach ? (
                <span className="inline-flex items-center gap-1 text-xs font-bold text-red-700">
                  <AlertTriangle className="h-4 w-4" aria-hidden="true" />
                  {t('attendance.ratio.short')}
                </span>
              ) : (
                <span className="inline-flex items-center gap-1 text-xs font-bold text-emerald-700">
                  <CheckCircle2 className="h-4 w-4" aria-hidden="true" />
                  {t('attendance.ratio.covered')}
                </span>
              )}
            </div>
            <p className="mt-2 text-sm text-muted-foreground">
              {t('attendance.ratio.summary', { students: room.students_present, staff: room.staff_present })}
            </p>
            <p className="text-xs text-muted-foreground">
              {t('attendance.ratio.rule', { count: room.ratio_staff_to_student, required: room.staff_required })}
            </p>
          </Card>
        </li>
      ))}
    </ul>
  )
}

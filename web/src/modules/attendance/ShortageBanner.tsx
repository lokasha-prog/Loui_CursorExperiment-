import { AlertTriangle } from 'lucide-react'
import { useTranslation } from 'react-i18next'
import { Link } from 'react-router-dom'
import { useLiveRatios } from '@/api/hooks'
import { localized } from '@/lib/utils'
import { can, useAuth } from '@/stores/auth'

/** A banner across every page while any room has fewer staff than its ratio requires (spec 02 §3.3). */
export function ShortageBanner() {
  const { t, i18n } = useTranslation()
  const me = useAuth((s) => s.me)
  const ratios = useLiveRatios(can(me, 'attendance.write'))
  const short = (ratios.data ?? []).filter((r) => r.is_breach)
  if (short.length === 0) return null

  return (
    <div role="alert" className="border-b border-red-300 bg-red-50 px-4 py-2.5 text-sm text-red-800 lg:px-8">
      <div className="mx-auto flex max-w-6xl flex-wrap items-center gap-x-3 gap-y-1">
        <AlertTriangle className="h-4 w-4 shrink-0" aria-hidden="true" />
        <strong>{t('attendance.banner.title')}</strong>
        <span>
          {short
            .map((r) => t('attendance.banner.room', { room: localized(r.name, i18n.language), required: r.staff_required, present: r.staff_present }))
            .join(' · ')}
        </span>
        <Link to="/attendance" className="ms-auto font-bold underline underline-offset-4">{t('attendance.banner.view')}</Link>
      </div>
    </div>
  )
}

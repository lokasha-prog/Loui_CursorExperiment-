import { Languages } from 'lucide-react'
import { useTranslation } from 'react-i18next'
import { changeLanguage, type Language } from '@/lib/i18n'
import { cn } from '@/lib/utils'

/** Switches between English (left-to-right) and Arabic (right-to-left); the whole page mirrors immediately. */
export function LanguageSwitcher({ className }: { className?: string }) {
  const { t, i18n } = useTranslation()
  const options: { code: Language; label: string }[] = [
    { code: 'en', label: t('common.english') },
    { code: 'ar', label: t('common.arabic') },
  ]
  return (
    <div role="group" aria-label={t('common.language')} className={cn('inline-flex items-center gap-1 rounded-md border bg-white p-0.5', className)}>
      <Languages className="mx-1.5 h-4 w-4 text-muted-foreground" aria-hidden="true" />
      {options.map((option) => (
        <button
          key={option.code}
          type="button"
          lang={option.code}
          aria-pressed={i18n.language === option.code}
          onClick={() => void changeLanguage(option.code)}
          className={cn(
            'rounded px-2.5 py-1 text-xs font-bold transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring',
            i18n.language === option.code ? 'bg-primary text-primary-foreground' : 'text-muted-foreground hover:bg-secondary',
          )}
        >
          {option.label}
        </button>
      ))}
    </div>
  )
}

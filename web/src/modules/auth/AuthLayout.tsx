import { HeartHandshake, Languages, ShieldCheck } from 'lucide-react'
import type { ReactNode } from 'react'
import { useTranslation } from 'react-i18next'
import { LanguageSwitcher } from '@/components/layout/LanguageSwitcher'

/** Split layout from the approved login mock-up: brand story on one side, form card on the other. Mirrors in Arabic. */
export function AuthLayout({ children }: { children: ReactNode }) {
  const { t } = useTranslation()
  const points = [
    { icon: Languages, text: t('auth.hero.bilingual') },
    { icon: ShieldCheck, text: t('auth.hero.secure') },
    { icon: HeartHandshake, text: t('auth.hero.caring') },
  ]
  return (
    <div className="relative min-h-screen overflow-hidden bg-gradient-to-br from-accent via-background to-sage/20">
      <div className="absolute end-4 top-4 z-10">
        <LanguageSwitcher />
      </div>
      <div className="mx-auto grid min-h-screen max-w-6xl items-center gap-10 px-6 py-16 lg:grid-cols-2 lg:gap-16">
        <section className="space-y-7">
          <div className="flex items-center gap-3">
            <span className="flex h-11 w-11 items-center justify-center rounded-xl bg-slate-900" aria-hidden="true">
              <span className="flex h-5 w-5 items-center justify-center rounded-full border-2 border-amber">
                <span className="h-1.5 w-1.5 rounded-full bg-primary" />
              </span>
            </span>
            <div className="leading-tight">
              <p className="font-heading text-lg font-bold">{t('common.appName')}</p>
              <p className="font-mono text-[10px] text-muted-foreground">{t('common.appTagline')}</p>
            </div>
          </div>
          <h1 className="font-heading text-4xl font-bold leading-tight lg:text-5xl">
            {t('auth.hero.headlineStart')} <span className="text-primary-dark">{t('auth.hero.headlineAccent')}</span>{' '}
            {t('auth.hero.headlineEnd')}
          </h1>
          <p className="max-w-md text-base text-muted-foreground">{t('auth.hero.body')}</p>
          <ul className="space-y-3">
            {points.map(({ icon: Icon, text }) => (
              <li key={text} className="flex items-center gap-3 text-sm font-semibold">
                <span className="flex h-8 w-8 items-center justify-center rounded-lg bg-white shadow-sm">
                  <Icon className="h-4 w-4 text-primary-dark" aria-hidden="true" />
                </span>
                {text}
              </li>
            ))}
          </ul>
        </section>
        <section className="nursery-card w-full max-w-md justify-self-center p-7 shadow-xl lg:justify-self-end">{children}</section>
      </div>
    </div>
  )
}

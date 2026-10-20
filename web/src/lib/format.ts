import type { TFunction } from 'i18next'

/** Whole months between a date of birth (yyyy-mm-dd) and today. */
export function ageInMonths(dateOfBirth: string, today = new Date()): number {
  const [year, month, day] = dateOfBirth.split('-').map(Number)
  let months = (today.getFullYear() - year) * 12 + (today.getMonth() + 1 - month)
  if (today.getDate() < day) months -= 1
  return Math.max(months, 0)
}

export function ageLabel(dateOfBirth: string, t: TFunction, today = new Date()): string {
  const months = ageInMonths(dateOfBirth, today)
  if (months < 24) return t('students.age.months', { count: months })
  const years = Math.floor(months / 12)
  const rest = months % 12
  return rest === 0 ? t('students.age.years', { count: years }) : t('students.age.yearsMonths', { years, months: rest })
}

/** Formats a date for the active language (Arabic uses Arabic month names but Western digits, as UAE centres expect). */
export function formatDate(value: string | null | undefined, language: string): string {
  if (!value) return ''
  const date = new Date(value.length === 10 ? `${value}T00:00:00` : value)
  return new Intl.DateTimeFormat(language === 'ar' ? 'ar-AE-u-nu-latn' : 'en-GB', {
    day: 'numeric',
    month: 'short',
    year: 'numeric',
  }).format(date)
}

export function formatTime(value: string): string {
  return value.slice(0, 5)
}

/** Money is stored as integer fils (1 AED = 100 fils). */
export function formatAed(fils: number, language: string): string {
  return new Intl.NumberFormat(language === 'ar' ? 'ar-AE-u-nu-latn' : 'en-AE', {
    style: 'currency',
    currency: 'AED',
  }).format(fils / 100)
}

export function emptyToNull(value: string | undefined | null): string | null {
  const trimmed = value?.trim()
  return trimmed ? trimmed : null
}

export function todayIso(): string {
  const now = new Date()
  const month = String(now.getMonth() + 1).padStart(2, '0')
  const day = String(now.getDate()).padStart(2, '0')
  return `${now.getFullYear()}-${month}-${day}`
}

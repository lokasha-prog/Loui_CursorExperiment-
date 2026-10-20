import { describe, expect, it } from 'vitest'
import ar from '@/locales/ar.json'
import en from '@/locales/en.json'
import i18n, { applyDocumentLanguage, changeLanguage, directionOf } from './i18n'
import { ageInMonths, ageLabel, emptyToNull, formatAed, formatDate, formatTime, todayIso } from './format'
import { cn, initials, localized } from './utils'

function flatten(object: Record<string, unknown>, prefix = ''): Record<string, string> {
  return Object.entries(object).reduce<Record<string, string>>((all, [key, value]) => {
    const path = prefix ? `${prefix}.${key}` : key
    return typeof value === 'object' && value !== null
      ? { ...all, ...flatten(value as Record<string, unknown>, path) }
      : { ...all, [path]: String(value) }
  }, {})
}

const placeholders = (text: string) => [...text.matchAll(/{{\s*(\w+)\s*}}/g)].map((m) => m[1]).sort()

describe('translations (English and Arabic must always match)', () => {
  const english = flatten(en)
  const arabic = flatten(ar)

  it('has exactly the same keys in both languages', () => {
    expect(Object.keys(arabic).sort()).toEqual(Object.keys(english).sort())
  })

  it('has no empty text', () => {
    for (const [key, value] of [...Object.entries(english), ...Object.entries(arabic)]) {
      expect(value.trim(), key).not.toBe('')
    }
  })

  it('uses the same placeholders in both languages', () => {
    for (const key of Object.keys(english)) {
      expect(placeholders(arabic[key]), key).toEqual(placeholders(english[key]))
    }
  })

  it('actually translates (Arabic text is not just the English text)', () => {
    const untranslated = Object.keys(english).filter(
      (key) => english[key] === arabic[key] && /[A-Za-z]{4,}/.test(english[key].replace(/{{\s*\w+\s*}}/g, '')) && !key.endsWith('english') && !key.endsWith('arabic'),
    )
    expect(untranslated).toEqual([])
  })

  it('contains Arabic script in the Arabic file', () => {
    expect(arabic['common.signOut']).toMatch(/[؀-ۿ]/)
  })
})

describe('language and direction', () => {
  it('maps Arabic to right-to-left and English to left-to-right', () => {
    expect(directionOf('ar')).toBe('rtl')
    expect(directionOf('en')).toBe('ltr')
  })

  it('switches the whole document when the language changes', async () => {
    await changeLanguage('ar')
    expect(document.documentElement.dir).toBe('rtl')
    expect(document.documentElement.lang).toBe('ar')
    expect(i18n.t('nav.students')).toBe(ar.nav.students)

    await changeLanguage('en')
    expect(document.documentElement.dir).toBe('ltr')
    expect(i18n.t('nav.students')).toBe(en.nav.students)
  })

  it('remembers the choice', async () => {
    await changeLanguage('ar')
    expect(localStorage.getItem('nms.language')).toBe('ar')
    await changeLanguage('en')
  })

  it('applyDocumentLanguage sets lang and dir', () => {
    applyDocumentLanguage('ar')
    expect(document.documentElement.getAttribute('dir')).toBe('rtl')
    applyDocumentLanguage('en')
    expect(document.documentElement.getAttribute('dir')).toBe('ltr')
  })
})

describe('utils', () => {
  it('cn merges and de-duplicates classes', () => {
    expect(cn('p-2', 'p-4', false && 'hidden', 'text-sm')).toBe('p-4 text-sm')
  })

  it('localized falls back to English, then anything', () => {
    expect(localized({ en: 'Room', ar: 'غرفة' }, 'ar')).toBe('غرفة')
    expect(localized({ en: 'Room' }, 'ar')).toBe('Room')
    expect(localized({ fr: 'Salle' }, 'ar')).toBe('Salle')
    expect(localized(null, 'en')).toBe('')
  })

  it('initials handles missing names', () => {
    expect(initials('layla', 'hassan')).toBe('LH')
    expect(initials(null, null)).toBe('?')
  })
})

describe('format', () => {
  const today = new Date(2026, 9, 20)

  it('counts whole months of age', () => {
    expect(ageInMonths('2026-09-20', today)).toBe(1)
    expect(ageInMonths('2026-09-21', today)).toBe(0)
    expect(ageInMonths('2024-10-20', today)).toBe(24)
    expect(ageInMonths('2030-01-01', today)).toBe(0)
  })

  it('describes age in months, years, or both', () => {
    expect(ageLabel('2026-02-20', i18n.t, today)).toBe('8 months')
    expect(ageLabel('2024-10-20', i18n.t, today)).toBe('2 years')
    expect(ageLabel('2023-07-20', i18n.t, today)).toBe('3 y 3 m')
  })

  it('formats dates for each language', () => {
    expect(formatDate('2026-10-20', 'en')).toContain('2026')
    expect(formatDate('2026-10-20', 'ar')).toContain('2026')
    expect(formatDate(null, 'en')).toBe('')
  })

  it('formats times, money and blanks', () => {
    expect(formatTime('07:30:00')).toBe('07:30')
    expect(formatAed(650000, 'en')).toMatch(/6,500/)
    expect(emptyToNull('  ')).toBeNull()
    expect(emptyToNull(' a ')).toBe('a')
    expect(todayIso()).toMatch(/^\d{4}-\d{2}-\d{2}$/)
  })
})

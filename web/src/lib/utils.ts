import { clsx, type ClassValue } from 'clsx'
import { twMerge } from 'tailwind-merge'

export function cn(...inputs: ClassValue[]) {
  return twMerge(clsx(inputs))
}

/** Picks the text for the active language, falling back to English (spec 07 §1.4). */
export function localized(text: Record<string, string> | null | undefined, language: string): string {
  if (!text) return ''
  return text[language] || text.en || Object.values(text)[0] || ''
}

export function initials(first?: string | null, last?: string | null): string {
  return `${first?.[0] ?? ''}${last?.[0] ?? ''}`.toUpperCase() || '?'
}

import { useCallback } from 'react'
import { useTranslation } from 'react-i18next'
import { ApiError } from '@/api/client'

/** Turns any failure into a message in the active language. Codes are translated; raw server text is never shown. */
export function useApiErrorMessage() {
  const { t, i18n } = useTranslation()
  return useCallback(
    (error: unknown): string => {
      const code = error instanceof ApiError ? error.code : 'UNKNOWN'
      const key = `errors.${code}`
      return i18n.exists(key) ? t(key) : t('errors.UNKNOWN')
    },
    [t, i18n],
  )
}

/** Field-level errors from the server, keyed by field name, for forms to attach to their inputs. */
export function serverFieldErrors(error: unknown): Record<string, string> {
  if (!(error instanceof ApiError)) return {}
  const fields: Record<string, string> = {}
  for (const item of error.errors) {
    if (item.field) fields[item.field] = item.code
  }
  return fields
}

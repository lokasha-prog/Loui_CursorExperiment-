import type { TFunction } from 'i18next'
import { ApiError } from '@/api/client'

/** What an AI card says when the AI Brain could not answer: not set up on this server, down for now, or something else. */
export function aiErrorText(error: unknown, t: TFunction): string {
  if (error instanceof ApiError && error.code === 'AI_NOT_SET_UP') return t('errors.AI_NOT_SET_UP')
  if (error instanceof ApiError && error.status === 422) return t('ai.down')
  return t('errors.UNKNOWN')
}

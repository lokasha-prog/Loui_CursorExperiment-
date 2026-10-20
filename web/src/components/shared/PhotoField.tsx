import { Camera, Trash2 } from 'lucide-react'
import { useId, useRef } from 'react'
import { useTranslation } from 'react-i18next'
import { useUploadMedia } from '@/api/hooks'
import { Button } from '@/components/ui/button'
import { toast } from '@/components/ui/toaster'
import { useApiErrorMessage } from '@/hooks/use-api-error'
import { ProtectedMedia } from './ProtectedMedia'

/** Pick a photo (from files or the camera), upload it at once, and keep only its id in the form. */
export function PhotoField({
  label,
  hint,
  value,
  onChange,
}: {
  label: string
  hint?: string
  value: string | null
  onChange: (id: string | null) => void
}) {
  const { t } = useTranslation()
  const upload = useUploadMedia()
  const message = useApiErrorMessage()
  const input = useRef<HTMLInputElement>(null)
  const id = useId()

  const pick = async (file?: File) => {
    if (!file) return
    try {
      const media = await upload.mutateAsync(file)
      onChange(media.id)
    } catch (error) {
      toast.error(message(error))
    }
  }

  return (
    <div>
      <label htmlFor={id} className="mb-1.5 block text-xs font-bold">{label}</label>
      <div className="flex items-center gap-3">
        <div className="h-20 w-20 shrink-0 overflow-hidden rounded-md border bg-slate-50">
          {value ? (
            <ProtectedMedia key={value} id={value} contentType="image/*" alt={label} className="h-20 w-20 rounded-none object-cover" />
          ) : (
            <div className="flex h-full items-center justify-center text-muted-foreground"><Camera className="h-6 w-6" aria-hidden="true" /></div>
          )}
        </div>
        <div className="space-y-1">
          <input
            ref={input}
            id={id}
            type="file"
            accept="image/jpeg,image/png,image/webp,image/gif"
            capture="environment"
            className="sr-only"
            onChange={(e) => void pick(e.target.files?.[0])}
          />
          <div className="flex gap-2">
            <Button type="button" size="sm" variant="secondary" disabled={upload.isPending} onClick={() => input.current?.click()}>
              {upload.isPending ? t('common.saving') : value ? t('photo.replace') : t('photo.choose')}
            </Button>
            {value && (
              <Button type="button" size="sm" variant="ghost" onClick={() => onChange(null)} aria-label={t('photo.remove')}>
                <Trash2 className="h-3.5 w-3.5" aria-hidden="true" />
              </Button>
            )}
          </div>
          {hint && <p className="text-[11px] text-muted-foreground">{hint}</p>}
        </div>
      </div>
    </div>
  )
}

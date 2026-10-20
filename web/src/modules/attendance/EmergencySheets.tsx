import { FileDown, Siren } from 'lucide-react'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { api } from '@/api/client'
import { useRoomOptions } from '@/api/hooks'
import { Button } from '@/components/ui/button'
import { Field, Select } from '@/components/ui/form-controls'
import { Dialog, DialogContent, DialogHeader } from '@/components/ui/surfaces'
import { toast } from '@/components/ui/toaster'
import { localized } from '@/lib/utils'

/** One-click printable emergency sheets for a whole room (a page per child). Opening one is recorded in the audit log. */
export function EmergencySheets() {
  const { t, i18n } = useTranslation()
  const [open, setOpen] = useState(false)
  const [roomId, setRoomId] = useState('')
  const [busy, setBusy] = useState(false)
  const rooms = useRoomOptions(open)

  const download = async () => {
    setBusy(true)
    try {
      const blob = await api.blob(`/rooms/${roomId}/emergency-report?language=${i18n.language === 'ar' ? 'ar' : 'en'}`)
      const url = URL.createObjectURL(blob)
      window.open(url, '_blank', 'noopener')
      setTimeout(() => URL.revokeObjectURL(url), 60_000)
    } catch {
      toast.error(t('emergency.failed'))
    } finally {
      setBusy(false)
    }
  }

  return (
    <>
      <Button variant="secondary" onClick={() => setOpen(true)}>
        <Siren className="h-4 w-4" aria-hidden="true" />{t('emergency.button')}
      </Button>
      <Dialog open={open} onOpenChange={setOpen}>
        <DialogContent closeLabel={t('common.close')} className="max-w-md">
          <DialogHeader title={t('emergency.title')} description={t('emergency.description')} />
          <Field label={t('emergency.room')} htmlFor="em-room">
            <Select id="em-room" value={roomId} onChange={(e) => setRoomId(e.target.value)}>
              <option value="">{t('emergency.pick')}</option>
              {(rooms.data ?? []).map((r) => <option key={r.id} value={r.id}>{localized(r.name, i18n.language)}</option>)}
            </Select>
          </Field>
          <div className="mt-4 flex justify-end">
            <Button onClick={() => void download()} disabled={!roomId || busy}><FileDown className="h-4 w-4" aria-hidden="true" />{t('emergency.download')}</Button>
          </div>
        </DialogContent>
      </Dialog>
    </>
  )
}

import { Download, Trash2 } from 'lucide-react'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { api } from '@/api/client'
import { usePurgeAudit, useRetention, useSaveRetention } from '@/api/hooks'
import { Button } from '@/components/ui/button'
import { Field, Input } from '@/components/ui/form-controls'
import { Alert, Card, Dialog, DialogContent, DialogHeader } from '@/components/ui/surfaces'
import { toast } from '@/components/ui/toaster'
import { useApiErrorMessage } from '@/hooks/use-api-error'

/** How long change history is kept: an automatic daily clean-up, a manual clean-up, and a copy to keep first. */
export function RetentionPanel() {
  const { t, i18n } = useTranslation()
  const policy = useRetention()
  const save = useSaveRetention()
  const purge = usePurgeAudit()
  const message = useApiErrorMessage()
  const [days, setDays] = useState<string | null>(null)
  const [confirming, setConfirming] = useState(false)
  const date = (v: string) => new Intl.DateTimeFormat(i18n.language === 'ar' ? 'ar-AE-u-nu-latn' : 'en-GB', { dateStyle: 'medium', timeStyle: 'short' }).format(new Date(v))

  if (policy.isLoading || !policy.data) return null
  const p = policy.data
  const value = days ?? String(p.retention_days)
  const tooShort = Number(value) < p.min_retention_days

  const apply = async (patch: { retention_days?: number; auto_purge_enabled?: boolean }) => {
    try {
      await save.mutateAsync({ retention_days: patch.retention_days ?? p.retention_days, auto_purge_enabled: patch.auto_purge_enabled ?? p.auto_purge_enabled })
      setDays(null)
      toast.success(t('audit.retention.saved'))
    } catch (error) {
      toast.error(message(error))
    }
  }

  const download = async () => {
    try {
      const blob = await api.blob('/audit-logs/export')
      const url = URL.createObjectURL(blob)
      const link = document.createElement('a')
      link.href = url
      link.download = `audit-log-${new Date().toISOString().slice(0, 10)}.csv`
      link.click()
      URL.revokeObjectURL(url)
    } catch (error) {
      toast.error(message(error))
    }
  }

  const runPurge = async () => {
    try {
      const run = await purge.mutateAsync()
      toast.success(t('audit.retention.purged', { count: run.rows_deleted }))
      setConfirming(false)
    } catch (error) {
      toast.error(message(error))
    }
  }

  return (
    <Card className="mb-6 p-4" aria-labelledby="retention-h">
      <h2 id="retention-h" className="font-heading text-lg font-semibold">{t('audit.retention.title')}</h2>
      <p className="mb-4 text-sm text-muted-foreground">{t('audit.retention.intro', { min: p.min_retention_days })}</p>
      <div className="grid gap-4 lg:grid-cols-2">
        <div className="space-y-3">
          <Field label={t('audit.retention.days')} htmlFor="rt-days" hint={t('audit.retention.daysHint', { min: p.min_retention_days })} error={tooShort ? t('audit.retention.tooShort', { min: p.min_retention_days }) : undefined}>
            <div className="flex gap-2">
              <Input id="rt-days" type="number" min={p.min_retention_days} dir="ltr" className="max-w-40 text-start" value={value} onChange={(e) => setDays(e.target.value)} />
              <Button variant="secondary" disabled={tooShort || days === null || save.isPending} onClick={() => void apply({ retention_days: Number(value) })}>{t('common.save')}</Button>
            </div>
          </Field>
          <label className="flex items-start gap-2 text-sm">
            <input type="checkbox" className="mt-1 h-4 w-4 accent-primary" checked={p.auto_purge_enabled} onChange={(e) => void apply({ auto_purge_enabled: e.target.checked })} />
            <span>{t('audit.retention.auto')}</span>
          </label>
          <div className="flex flex-wrap gap-2">
            <Button variant="secondary" onClick={() => void download()}><Download className="h-4 w-4" aria-hidden="true" />{t('audit.retention.download')}</Button>
            <Button variant="destructive" disabled={p.eligible_for_purge === 0} onClick={() => setConfirming(true)}><Trash2 className="h-4 w-4" aria-hidden="true" />{t('audit.retention.purgeNow')}</Button>
          </div>
        </div>
        <dl className="grid grid-cols-2 gap-3 text-sm">
          <div><dt className="text-xs text-muted-foreground">{t('audit.retention.total')}</dt><dd className="font-bold">{p.total_entries}</dd></div>
          <div><dt className="text-xs text-muted-foreground">{t('audit.retention.oldest')}</dt><dd className="font-bold">{p.oldest_entry_at ? date(p.oldest_entry_at) : '—'}</dd></div>
          <div><dt className="text-xs text-muted-foreground">{t('audit.retention.eligible')}</dt><dd className="font-bold">{p.eligible_for_purge}</dd></div>
          <div><dt className="text-xs text-muted-foreground">{t('audit.retention.cutoff')}</dt><dd className="font-bold">{date(p.cutoff)}</dd></div>
          <div className="col-span-2">
            <dt className="mb-1 text-xs text-muted-foreground">{t('audit.retention.runs')}</dt>
            <dd>
              {p.recent_runs.length === 0 ? '—' : (
                <ul className="space-y-1 text-xs">
                  {p.recent_runs.slice(0, 4).map((r) => (
                    <li key={r.run_at}>{date(r.run_at)} · {t(`audit.retention.mode.${r.mode}`)} · {t('audit.retention.removed', { count: r.rows_deleted })}</li>
                  ))}
                </ul>
              )}
            </dd>
          </div>
        </dl>
      </div>

      <Dialog open={confirming} onOpenChange={setConfirming}>
        <DialogContent closeLabel={t('common.close')} className="max-w-md">
          <DialogHeader title={t('audit.retention.confirmTitle')} description={t('audit.retention.confirmBody', { count: p.eligible_for_purge })} />
          <Alert tone="info">{t('audit.retention.confirmHint')}</Alert>
          <div className="mt-4 flex justify-end gap-2">
            <Button variant="secondary" onClick={() => setConfirming(false)}>{t('common.cancel')}</Button>
            <Button variant="destructive" disabled={purge.isPending} onClick={() => void runPurge()}>{t('audit.retention.confirmButton')}</Button>
          </div>
        </DialogContent>
      </Dialog>
    </Card>
  )
}

import * as TabsPrimitive from '@radix-ui/react-tabs'
import { Plus, Trash2 } from 'lucide-react'
import { useEffect, useMemo, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Link } from 'react-router-dom'
import { useCentreSettings, useDeleteRole, usePermissionCatalogue, useRoles, useSaveCentreSettings, useSaveRole } from '@/api/hooks'
import type { CentreSettings, RoleItem } from '@/api/types'
import { PageHeader } from '@/components/layout/PageHeader'
import { ListError, ListSkeleton } from '@/components/shared/ListState'
import { PhotoField } from '@/components/shared/PhotoField'
import { Button } from '@/components/ui/button'
import { Field, Input, Textarea } from '@/components/ui/form-controls'
import { Alert, Badge, Card, Dialog, DialogContent, DialogHeader, Skeleton } from '@/components/ui/surfaces'
import { toast } from '@/components/ui/toaster'
import { useApiErrorMessage } from '@/hooks/use-api-error'
import { can, useAuth } from '@/stores/auth'
import { confirmAction } from '@/components/shared/confirm'

const DAYS = ['mon', 'tue', 'wed', 'thu', 'fri', 'sat', 'sun']

function CentreForm({ settings, canWrite }: { settings: CentreSettings; canWrite: boolean }) {
  const { t } = useTranslation()
  const save = useSaveCentreSettings()
  const message = useApiErrorMessage()
  const [draft, setDraft] = useState(settings)
  const [failure, setFailure] = useState<string | null>(null)
  useEffect(() => setDraft(settings), [settings])
  const set = (patch: Partial<CentreSettings>) => setDraft((d) => ({ ...d, ...patch }))
  const num = (text: string) => Number(text.replace(/\D/g, '')) || 0

  const submit = async () => {
    if (!draft.name.en?.trim()) return setFailure(t('validation.required'))
    setFailure(null)
    try {
      await save.mutateAsync(draft)
      toast.success(t('settings.saved'))
    } catch (error) {
      setFailure(message(error))
    }
  }

  return (
    <form onSubmit={(e) => { e.preventDefault(); void submit() }} className="space-y-6" noValidate>
      {failure && <Alert>{failure}</Alert>}
      <fieldset disabled={!canWrite} className="space-y-6">
        <Card className="space-y-4 p-4">
          <h2 className="font-heading text-lg font-semibold">{t('settings.profile.title')}</h2>
          <div className="grid gap-3 sm:grid-cols-2">
            <Field label={t('settings.profile.nameEn')} htmlFor="cs-name-en" required><Input id="cs-name-en" value={draft.name.en ?? ''} onChange={(e) => set({ name: { ...draft.name, en: e.target.value } })} /></Field>
            <Field label={t('settings.profile.nameAr')} htmlFor="cs-name-ar"><Input id="cs-name-ar" dir="rtl" lang="ar" value={draft.name.ar ?? ''} onChange={(e) => set({ name: { ...draft.name, ar: e.target.value } })} /></Field>
            <Field label={t('settings.profile.email')} htmlFor="cs-email"><Input id="cs-email" type="email" dir="ltr" className="text-start" value={draft.contact_email ?? ''} onChange={(e) => set({ contact_email: e.target.value })} /></Field>
            <Field label={t('settings.profile.phone')} htmlFor="cs-phone"><Input id="cs-phone" type="tel" dir="ltr" className="text-start" value={draft.contact_phone ?? ''} onChange={(e) => set({ contact_phone: e.target.value })} /></Field>
            <Field label={t('settings.profile.licence')} htmlFor="cs-licence"><Input id="cs-licence" dir="ltr" className="text-start" value={draft.khda_license_number ?? ''} onChange={(e) => set({ khda_license_number: e.target.value })} /></Field>
            <Field label={t('settings.profile.website')} htmlFor="cs-web"><Input id="cs-web" type="url" dir="ltr" className="text-start" value={draft.website ?? ''} onChange={(e) => set({ website: e.target.value })} /></Field>
            <Field label={t('settings.profile.address')} htmlFor="cs-address" className="sm:col-span-2"><Input id="cs-address" value={draft.address ?? ''} onChange={(e) => set({ address: e.target.value })} /></Field>
            <Field label={t('settings.profile.colour')} htmlFor="cs-colour" hint={t('settings.profile.colourHint')}>
              <div className="flex items-center gap-2">
                <Input id="cs-colour" dir="ltr" className="text-start" placeholder="#0E7C86" value={draft.brand_color ?? ''} onChange={(e) => set({ brand_color: e.target.value })} />
                {/^#[0-9a-fA-F]{6}$/.test(draft.brand_color ?? '') && <span className="h-8 w-8 shrink-0 rounded-md border" style={{ backgroundColor: draft.brand_color! }} aria-hidden="true" />}
              </div>
            </Field>
            <PhotoField label={t('settings.profile.logo')} hint={t('settings.profile.logoHint')} value={draft.logo_media_id} onChange={(id) => set({ logo_media_id: id })} />
          </div>
        </Card>

        <Card className="space-y-4 p-4">
          <h2 className="font-heading text-lg font-semibold">{t('settings.rules.title')}</h2>
          <div className="grid gap-3 sm:grid-cols-2">
            <Field label={t('settings.rules.late')} htmlFor="cs-late" hint={t('settings.rules.lateHint')}><Input id="cs-late" inputMode="numeric" dir="ltr" className="text-start" value={draft.late_grace_minutes} onChange={(e) => set({ late_grace_minutes: num(e.target.value) })} /></Field>
            <Field label={t('settings.rules.early')} htmlFor="cs-early" hint={t('settings.rules.earlyHint')}><Input id="cs-early" inputMode="numeric" dir="ltr" className="text-start" value={draft.early_leave_grace_minutes} onChange={(e) => set({ early_leave_grace_minutes: num(e.target.value) })} /></Field>
          </div>
          <fieldset>
            <legend className="mb-1.5 text-xs font-bold">{t('settings.rules.weekend')}</legend>
            <div className="flex flex-wrap gap-3">
              {DAYS.map((d) => (
                <label key={d} className="flex items-center gap-1.5 text-sm">
                  <input
                    type="checkbox"
                    className="h-4 w-4 accent-primary"
                    checked={draft.weekend_days.includes(d)}
                    onChange={(e) => set({ weekend_days: e.target.checked ? [...draft.weekend_days, d] : draft.weekend_days.filter((x) => x !== d) })}
                  />
                  {t(`settings.days.${d}`)}
                </label>
              ))}
            </div>
          </fieldset>
          <p className="text-xs text-muted-foreground">{t('settings.rules.timezone', { zone: draft.timezone })}</p>
        </Card>

        <Card className="space-y-3 p-4">
          <h2 className="font-heading text-lg font-semibold">{t('settings.portal.title')}</h2>
          <label className="flex items-start gap-2 text-sm">
            <input type="checkbox" className="mt-0.5 h-4 w-4 accent-primary" checked={draft.parents_see_invoices} onChange={(e) => set({ parents_see_invoices: e.target.checked })} />
            <span><span className="font-semibold">{t('settings.portal.invoices')}</span><span className="block text-xs text-muted-foreground">{t('settings.portal.invoicesHint')}</span></span>
          </label>
        </Card>

        <Card className="space-y-3 p-4">
          <h2 className="font-heading text-lg font-semibold">{t('settings.sms.title')}</h2>
          <p className="text-xs text-muted-foreground">{t('settings.sms.body')}</p>
          {!draft.sms_available && <Alert>{t('settings.sms.notSetUp')}</Alert>}
          <label className="flex items-center gap-2 text-sm font-semibold">
            <input type="checkbox" className="h-4 w-4 accent-primary" checked={draft.sms_enabled} onChange={(e) => set({ sms_enabled: e.target.checked })} />
            {t('settings.sms.enabled')}
          </label>
          <div className="grid gap-3 sm:grid-cols-2">
            <Field label={t('settings.sms.quota')} htmlFor="cs-sms-quota" hint={t('settings.sms.quotaHint')}><Input id="cs-sms-quota" inputMode="numeric" dir="ltr" className="text-start" value={draft.sms_monthly_quota} onChange={(e) => set({ sms_monthly_quota: num(e.target.value) })} /></Field>
            <p className="self-end pb-2 text-sm text-muted-foreground">{t('settings.sms.used', { count: draft.sms_used_this_month })}</p>
          </div>
        </Card>
      </fieldset>
      {canWrite && <div className="flex justify-end"><Button type="submit" disabled={save.isPending}>{t('settings.save')}</Button></div>}

      <Card className="space-y-2 p-4">
        <h2 className="font-heading text-lg font-semibold">{t('settings.elsewhere.title')}</h2>
        <ul className="list-inside list-disc text-sm text-muted-foreground">
          <li><Link className="font-semibold text-primary-dark underline-offset-4 hover:underline" to="/billing">{t('settings.elsewhere.billing')}</Link> — {t('settings.elsewhere.billingBody')}</li>
          <li><Link className="font-semibold text-primary-dark underline-offset-4 hover:underline" to="/audit">{t('settings.elsewhere.audit')}</Link> — {t('settings.elsewhere.auditBody')}</li>
          <li><Link className="font-semibold text-primary-dark underline-offset-4 hover:underline" to="/rooms">{t('settings.elsewhere.rooms')}</Link> — {t('settings.elsewhere.roomsBody')}</li>
        </ul>
      </Card>
    </form>
  )
}

function RoleDialog({ role, onClose }: { role: RoleItem | null; onClose: () => void }) {
  const { t, i18n } = useTranslation()
  const catalogue = usePermissionCatalogue()
  const save = useSaveRole(role?.id)
  const message = useApiErrorMessage()
  const [en, setEn] = useState(role?.display_name.en ?? '')
  const [ar, setAr] = useState(role?.display_name.ar ?? '')
  const [description, setDescription] = useState(role?.description ?? '')
  const [picked, setPicked] = useState<string[]>(role?.permissions ?? [])
  const [failure, setFailure] = useState<string | null>(null)
  const groups = useMemo(() => {
    const map = new Map<string, string[]>()
    for (const p of catalogue.data ?? []) map.set(p.split('.')[0], [...(map.get(p.split('.')[0]) ?? []), p])
    return [...map.entries()]
  }, [catalogue.data])
  const toggle = (p: string, on: boolean) => setPicked((all) => (on ? [...all, p] : all.filter((x) => x !== p)))

  const submit = async () => {
    if (!en.trim()) return setFailure(t('validation.required'))
    if (picked.length === 0) return setFailure(t('settings.roles.needPermission'))
    setFailure(null)
    try {
      await save.mutateAsync({ display_name: ar.trim() ? { en: en.trim(), ar: ar.trim() } : { en: en.trim() }, description: description.trim() || null, permissions: picked })
      toast.success(t('settings.roles.saved'))
      onClose()
    } catch (error) {
      setFailure(message(error))
    }
  }

  return (
    <Dialog open onOpenChange={(o) => !o && onClose()}>
      <DialogContent closeLabel={t('common.close')} className="max-w-2xl">
        <DialogHeader title={t(role ? 'settings.roles.edit' : 'settings.roles.add')} />
        <form onSubmit={(e) => { e.preventDefault(); void submit() }} className="space-y-4" noValidate>
          {failure && <Alert>{failure}</Alert>}
          <div className="grid gap-3 sm:grid-cols-2">
            <Field label={t('settings.roles.nameEn')} htmlFor="rl-en" required><Input id="rl-en" value={en} maxLength={80} onChange={(e) => setEn(e.target.value)} /></Field>
            <Field label={t('settings.roles.nameAr')} htmlFor="rl-ar"><Input id="rl-ar" dir="rtl" lang="ar" value={ar} maxLength={80} onChange={(e) => setAr(e.target.value)} /></Field>
            <Field label={t('settings.roles.description')} htmlFor="rl-desc" className="sm:col-span-2"><Textarea id="rl-desc" rows={2} maxLength={300} value={description} onChange={(e) => setDescription(e.target.value)} /></Field>
          </div>
          <fieldset className="space-y-3">
            <legend className="mb-1 text-sm font-bold">{t('settings.roles.permissions')}</legend>
            {catalogue.isLoading ? <Skeleton className="h-32" /> : groups.map(([module, perms]) => (
              <div key={module} className="rounded-md border p-3">
                <p className="mb-1.5 text-xs font-bold uppercase tracking-wide text-muted-foreground">{t(`settings.modules.${module}`, { defaultValue: module.replace(/_/g, ' ') })}</p>
                <div className="flex flex-wrap gap-x-4 gap-y-1.5">
                  {perms.map((p) => (
                    <label key={p} className="flex items-center gap-1.5 text-sm">
                      <input type="checkbox" className="h-4 w-4 accent-primary" checked={picked.includes(p)} onChange={(e) => toggle(p, e.target.checked)} />
                      <span dir="ltr">{p.split('.')[1].replace(/_/g, ' ')}</span>
                    </label>
                  ))}
                </div>
              </div>
            ))}
          </fieldset>
          <p className="text-xs text-muted-foreground">{t('settings.roles.refreshNote', { lang: i18n.language })}</p>
          <div className="flex justify-end gap-2 border-t pt-4">
            <Button type="button" variant="secondary" onClick={onClose}>{t('common.cancel')}</Button>
            <Button type="submit" disabled={save.isPending}>{t('settings.roles.save')}</Button>
          </div>
        </form>
      </DialogContent>
    </Dialog>
  )
}

function RolesTab() {
  const { t, i18n } = useTranslation()
  const roles = useRoles()
  const remove = useDeleteRole()
  const message = useApiErrorMessage()
  const [editing, setEditing] = useState<{ role: RoleItem | null } | null>(null)

  const del = async (r: RoleItem) => {
    if (!(await confirmAction({ title: t('settings.roles.confirmDelete'), confirmLabel: t('confirm.remove') }))) return
    try {
      await remove.mutateAsync(r.id)
    } catch (error) {
      toast.error(message(error))
    }
  }

  return (
    <>
      <div className="mb-4 flex items-center justify-between gap-3">
        <p className="text-sm text-muted-foreground">{t('settings.roles.intro')}</p>
        <Button onClick={() => setEditing({ role: null })}><Plus className="h-4 w-4" aria-hidden="true" />{t('settings.roles.add')}</Button>
      </div>
      <Card className="overflow-hidden">
        {roles.isLoading ? <ListSkeleton /> : roles.isError ? <ListError error={roles.error} onRetry={() => void roles.refetch()} /> : (
          <ul className="divide-y">
            {(roles.data ?? []).map((r) => (
              <li key={r.id} className="flex flex-wrap items-center gap-3 px-4 py-3 text-sm">
                {r.is_system_role ? (
                  <span className="font-bold">{t(`roles.${r.name}`, { defaultValue: r.name })}</span>
                ) : (
                  <button type="button" className="font-bold text-primary-dark underline-offset-4 hover:underline focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring" onClick={() => setEditing({ role: r })}>
                    {r.display_name[i18n.language] ?? r.display_name.en}
                  </button>
                )}
                <Badge tone={r.is_system_role ? 'neutral' : 'info'}>{t(r.is_system_role ? 'settings.roles.builtIn' : 'settings.roles.custom')}</Badge>
                <span className="text-xs text-muted-foreground">{t('settings.roles.permissionCount', { count: r.permissions.length })} · {t('settings.roles.people', { count: r.user_count })}</span>
                {!r.is_system_role && <Button size="sm" variant="ghost" className="ms-auto" aria-label={t('common.delete')} onClick={() => void del(r)}><Trash2 className="h-4 w-4" aria-hidden="true" /></Button>}
              </li>
            ))}
          </ul>
        )}
      </Card>
      {editing && <RoleDialog key={editing.role?.id ?? 'new'} role={editing.role} onClose={() => setEditing(null)} />}
    </>
  )
}

export default function SettingsPage() {
  const { t } = useTranslation()
  const me = useAuth((s) => s.me)
  const settings = useCentreSettings()
  const tabs = ['centre', ...(can(me, 'user_management.manage') ? ['roles'] : [])]
  const [tab, setTab] = useState('centre')

  return (
    <>
      <PageHeader title={t('settings.title')} subtitle={t('settings.subtitle')} />
      <TabsPrimitive.Root value={tab} onValueChange={setTab}>
        {tabs.length > 1 && (
          <TabsPrimitive.List className="mb-5 flex gap-1 overflow-x-auto border-b">
            {tabs.map((key) => (
              <TabsPrimitive.Trigger
                key={key}
                value={key}
                className="whitespace-nowrap border-b-2 border-transparent px-4 py-2.5 text-sm font-semibold text-muted-foreground transition-colors hover:text-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring data-[state=active]:border-primary data-[state=active]:font-bold data-[state=active]:text-primary-dark"
              >
                {t(`settings.tabs.${key}`)}
              </TabsPrimitive.Trigger>
            ))}
          </TabsPrimitive.List>
        )}
        <TabsPrimitive.Content value="centre">
          {settings.isLoading ? <Skeleton className="h-96" /> : settings.isError || !settings.data ? <ListError error={settings.error} onRetry={() => void settings.refetch()} /> : <CentreForm settings={settings.data} canWrite={can(me, 'settings.write')} />}
        </TabsPrimitive.Content>
        {can(me, 'user_management.manage') && <TabsPrimitive.Content value="roles"><RolesTab /></TabsPrimitive.Content>}
      </TabsPrimitive.Root>
    </>
  )
}

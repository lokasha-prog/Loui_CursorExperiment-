import { useQueryClient } from '@tanstack/react-query'
import { Download } from 'lucide-react'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { useNavigate } from 'react-router-dom'
import { api } from '@/api/client'
import {
  useAssignStaffToCentre, useGroupOverview, useGroupReport, useMyCentres, useProvisionCentre, useStudentOptions, useTransferStudent,
} from '@/api/hooks'
import type { CentreFigures, Me } from '@/api/types'
import { PageHeader } from '@/components/layout/PageHeader'
import { Button } from '@/components/ui/button'
import { Field, Input, Select } from '@/components/ui/form-controls'
import { Alert, Badge, Card, Skeleton, Table, Td, Th } from '@/components/ui/surfaces'
import { toast } from '@/components/ui/toaster'
import { useApiErrorMessage } from '@/hooks/use-api-error'
import { localized } from '@/lib/utils'
import { can, useAuth } from '@/stores/auth'

const STAFF_ROLES = ['centre_admin', 'teacher', 'receptionist', 'accounts', 'marketing', 'it'] as const
const REPORTS = ['attendance', 'revenue', 'enrolment'] as const

const money = (fils: number, lang: string) => new Intl.NumberFormat(lang === 'ar' ? 'ar-AE-u-nu-latn' : 'en-AE', { maximumFractionDigits: 0 }).format(fils / 100)

interface SwitchedSession { access_token: string; me: Me }

/** The whole group of centres in one place: every centre side by side, reports across all of them, and the tools to grow the group. */
export default function GroupPage() {
  const { t, i18n } = useTranslation()
  const lang = i18n.language
  const me = useAuth((s) => s.me)
  const manage = can(me, 'group.manage')
  const message = useApiErrorMessage()
  const navigate = useNavigate()
  const client = useQueryClient()
  const overview = useGroupOverview()
  const mine = useMyCentres()
  const [report, setReport] = useState<(typeof REPORTS)[number]>('attendance')
  const preview = useGroupReport(report, lang === 'ar' ? 'ar' : 'en')
  const [failure, setFailure] = useState<string | null>(null)

  const switchTo = async (centreId: string) => {
    setFailure(null)
    try {
      const session = await api.post<SwitchedSession>('/group/switch', { centre_id: centreId })
      useAuth.getState().setSession(session.access_token, session.me)
      client.clear()
      navigate('/dashboard')
    } catch (error) {
      setFailure(message(error))
    }
  }

  const download = async (format: 'excel' | 'pdf') => {
    const file = await api.blob(`/group/reports/${report}?format=${format}&lang=${lang === 'ar' ? 'ar' : 'en'}`)
    const url = URL.createObjectURL(file)
    const a = document.createElement('a')
    a.href = url
    a.download = `group-${report}.${format === 'excel' ? 'xlsx' : 'pdf'}`
    a.click()
    window.setTimeout(() => URL.revokeObjectURL(url), 30_000)
  }

  const row = (r: CentreFigures) => (
    <tr key={r.centre_id}>
      <Td className="font-semibold">{localized(r.name, lang)}{r.is_head && <Badge tone="info" className="ms-2">{t('group.head')}</Badge>}</Td>
      <Td>{r.students}</Td><Td>{r.staff}</Td><Td>{r.present_today}</Td>
      <Td>{money(r.billed_fils, lang)}</Td><Td>{money(r.collected_fils, lang)}</Td>
      <Td className={r.overdue_fils > 0 ? 'font-semibold text-red-700' : ''}>{money(r.overdue_fils, lang)}</Td>
      <Td>{r.open_enquiries}</Td>
    </tr>
  )

  return (
    <>
      <PageHeader title={t('group.title')} subtitle={t('group.subtitle')} />
      {failure && <Alert>{failure}</Alert>}

      <section className="mb-8" aria-labelledby="group-centres">
        <h2 id="group-centres" className="mb-2 font-heading text-lg font-semibold">{t('group.yourCentres')}</h2>
        <div className="flex flex-wrap gap-2">
          {(mine.data ?? []).map((c) => (
            <Button key={c.id} variant={c.slug === me?.tenant.slug ? 'default' : 'secondary'} disabled={c.slug === me?.tenant.slug} onClick={() => void switchTo(c.id)}>
              {localized(c.name, lang)}{c.slug === me?.tenant.slug ? ` · ${t('group.here')}` : ''}
            </Button>
          ))}
        </div>
        <p className="mt-2 text-sm text-muted-foreground">{t('group.switchHint')}</p>
      </section>

      <section className="mb-8" aria-labelledby="group-overview">
        <h2 id="group-overview" className="mb-2 font-heading text-lg font-semibold">{t('group.overview')}</h2>
        {overview.isLoading ? <Skeleton className="h-40" /> : overview.data && (
          <Card className="overflow-x-auto">
            <Table>
              <thead>
                <tr>
                  <Th>{t('group.centre')}</Th><Th>{t('group.children')}</Th><Th>{t('group.staff')}</Th><Th>{t('group.present')}</Th>
                  <Th>{t('group.billed')}</Th><Th>{t('group.collected')}</Th><Th>{t('group.overdue')}</Th><Th>{t('group.enquiries')}</Th>
                </tr>
              </thead>
              <tbody>
                {overview.data.centres.map(row)}
                <tr className="bg-muted/40 font-bold">
                  <Td>{t('group.total')}</Td>
                  <Td>{overview.data.total.students}</Td><Td>{overview.data.total.staff}</Td><Td>{overview.data.total.present_today}</Td>
                  <Td>{money(overview.data.total.billed_fils, lang)}</Td><Td>{money(overview.data.total.collected_fils, lang)}</Td>
                  <Td>{money(overview.data.total.overdue_fils, lang)}</Td><Td>{overview.data.total.open_enquiries}</Td>
                </tr>
              </tbody>
            </Table>
            <p className="p-3 text-xs text-muted-foreground">{t('group.currencyNote')}</p>
          </Card>
        )}
      </section>

      <section className="mb-8" aria-labelledby="group-reports">
        <h2 id="group-reports" className="mb-2 font-heading text-lg font-semibold">{t('group.reports')}</h2>
        <Card className="p-4">
          <div className="mb-3 flex flex-wrap items-end gap-3">
            <Field label={t('group.reportKind')} htmlFor="gr-kind">
              <Select id="gr-kind" value={report} onChange={(e) => setReport(e.target.value as (typeof REPORTS)[number])}>
                {REPORTS.map((k) => <option key={k} value={k}>{t(`group.kinds.${k}`)}</option>)}
              </Select>
            </Field>
            <Button variant="secondary" onClick={() => void download('excel')}><Download className="h-4 w-4" aria-hidden="true" />Excel</Button>
            <Button variant="secondary" onClick={() => void download('pdf')}><Download className="h-4 w-4" aria-hidden="true" />PDF</Button>
          </div>
          {preview.data && (
            <div className="overflow-x-auto">
              <Table>
                <thead><tr>{preview.data.columns.map((c) => <Th key={c}>{c}</Th>)}</tr></thead>
                <tbody>{preview.data.rows.map((r, i) => <tr key={i}>{r.map((v, j) => <Td key={j}>{v ?? ''}</Td>)}</tr>)}</tbody>
              </Table>
            </div>
          )}
        </Card>
      </section>

      {manage && <ManageTools onError={setFailure} />}
    </>
  )
}

function ManageTools({ onError }: { onError: (text: string | null) => void }) {
  const { t, i18n } = useTranslation()
  const lang = i18n.language
  const message = useApiErrorMessage()
  const me = useAuth((s) => s.me)
  const mine = useMyCentres()
  const students = useStudentOptions()
  const add = useProvisionCentre()
  const assign = useAssignStaffToCentre()
  const transfer = useTransferStudent()
  const [name, setName] = useState('')
  const [nameAr, setNameAr] = useState('')
  const [slug, setSlug] = useState('')
  const [email, setEmail] = useState('')
  const [role, setRole] = useState<string>('teacher')
  const [staffCentre, setStaffCentre] = useState('')
  const [student, setStudent] = useState('')
  const [toCentre, setToCentre] = useState('')
  const others = (mine.data ?? []).filter((c) => c.slug !== me?.tenant.slug)

  const run = async (work: () => Promise<unknown>, done: string) => {
    onError(null)
    try {
      await work()
      toast.success(done)
    } catch (error) {
      onError(message(error))
    }
  }

  return (
    <section aria-labelledby="group-manage" className="grid gap-4 lg:grid-cols-3">
      <h2 id="group-manage" className="sr-only">{t('group.manage')}</h2>
      <Card className="p-4">
        <h3 className="mb-3 font-heading font-semibold">{t('group.addCentre')}</h3>
        <div className="space-y-3">
          <Field label={t('group.nameEn')} htmlFor="gc-name" required><Input id="gc-name" value={name} onChange={(e) => setName(e.target.value)} /></Field>
          <Field label={t('group.nameAr')} htmlFor="gc-ar"><Input id="gc-ar" dir="rtl" value={nameAr} onChange={(e) => setNameAr(e.target.value)} /></Field>
          <Field label={t('group.slug')} htmlFor="gc-slug" hint={t('group.slugHint')} required><Input id="gc-slug" dir="ltr" value={slug} onChange={(e) => setSlug(e.target.value.toLowerCase())} /></Field>
          <Button
            disabled={!name.trim() || slug.length < 3 || add.isPending}
            onClick={() => void run(async () => { await add.mutateAsync({ name: name.trim(), name_arabic: nameAr.trim() || undefined, slug }); setName(''); setNameAr(''); setSlug('') }, t('group.centreAdded'))}
          >{t('group.addCentreGo')}</Button>
        </div>
      </Card>

      <Card className="p-4">
        <h3 className="mb-3 font-heading font-semibold">{t('group.addStaff')}</h3>
        <p className="mb-3 text-sm text-muted-foreground">{t('group.addStaffHint')}</p>
        <div className="space-y-3">
          <Field label={t('group.staffEmail')} htmlFor="gs-email" required><Input id="gs-email" type="email" dir="ltr" value={email} onChange={(e) => setEmail(e.target.value)} /></Field>
          <Field label={t('group.toCentre')} htmlFor="gs-centre" required>
            <Select id="gs-centre" value={staffCentre} onChange={(e) => setStaffCentre(e.target.value)}><option value="">—</option>{others.map((c) => <option key={c.id} value={c.id}>{localized(c.name, lang)}</option>)}</Select>
          </Field>
          <Field label={t('group.roleThere')} htmlFor="gs-role">
            <Select id="gs-role" value={role} onChange={(e) => setRole(e.target.value)}>{STAFF_ROLES.map((r) => <option key={r} value={r}>{t(`group.roles.${r}`)}</option>)}</Select>
          </Field>
          <Button disabled={!email || !staffCentre || assign.isPending} onClick={() => void run(() => assign.mutateAsync({ email, centre_id: staffCentre, role }), t('group.staffAdded'))}>{t('group.addStaffGo')}</Button>
        </div>
      </Card>

      <Card className="p-4">
        <h3 className="mb-3 font-heading font-semibold">{t('group.moveChild')}</h3>
        <p className="mb-3 text-sm text-muted-foreground">{t('group.moveChildHint')}</p>
        <div className="space-y-3">
          <Field label={t('group.child')} htmlFor="gm-child" required>
            <Select id="gm-child" value={student} onChange={(e) => setStudent(e.target.value)}><option value="">—</option>{(students.data ?? []).map((s) => <option key={s.id} value={s.id}>{s.first_name} {s.last_name}</option>)}</Select>
          </Field>
          <Field label={t('group.toCentre')} htmlFor="gm-centre" required>
            <Select id="gm-centre" value={toCentre} onChange={(e) => setToCentre(e.target.value)}><option value="">—</option>{others.map((c) => <option key={c.id} value={c.id}>{localized(c.name, lang)}</option>)}</Select>
          </Field>
          <Button
            disabled={!student || !toCentre || transfer.isPending}
            onClick={() => void run(async () => { await transfer.mutateAsync({ studentId: student, toCentreId: toCentre }); setStudent('') }, t('group.childMoved'))}
          >{t('group.moveChildGo')}</Button>
        </div>
      </Card>
    </section>
  )
}

import { zodResolver } from '@hookform/resolvers/zod'
import { CalendarOff, Check, Plus, Settings2, X } from 'lucide-react'
import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { useTranslation } from 'react-i18next'
import { z } from 'zod'
import { ApiError } from '@/api/client'
import {
  flatten,
  useCreateLeave,
  useCreateLeaveType,
  useLeaveAction,
  useLeaves,
  useLeaveTypes,
  useStaffOptions,
  useStudentOptions,
} from '@/api/hooks'
import type { LeaveRequest } from '@/api/types'
import { PageHeader } from '@/components/layout/PageHeader'
import { ListError, ListSkeleton, LoadMore } from '@/components/shared/ListState'
import { Button } from '@/components/ui/button'
import { Field, Input, Select } from '@/components/ui/form-controls'
import { Alert, Badge, Card, Dialog, DialogContent, DialogHeader, EmptyState, Table, Td, Th, type Tone } from '@/components/ui/surfaces'
import { toast } from '@/components/ui/toaster'
import { useApiErrorMessage } from '@/hooks/use-api-error'
import { formatDate } from '@/lib/format'
import { localized } from '@/lib/utils'
import { can, useAuth } from '@/stores/auth'

const statusTone: Record<LeaveRequest['status'], Tone> = {
  submitted: 'info',
  approved: 'success',
  rejected: 'danger',
  cancelled: 'neutral',
}

function useFailure() {
  const message = useApiErrorMessage()
  const { t } = useTranslation()
  const [failure, setFailure] = useState<string | null>(null)
  const fail = (error: unknown) => {
    if (error instanceof ApiError && error.status === 409) setFailure(t('leave.errors.conflict'))
    else setFailure(message(error))
  }
  return { failure, setFailure, fail }
}

// ── New request ─────────────────────────────────────────────────────────────

const requestSchema = z
  .object({
    personType: z.enum(['student', 'staff']),
    personId: z.string().min(1, 'validation.required'),
    leaveTypeId: z.string().min(1, 'validation.required'),
    startDate: z.string().min(1, 'validation.required'),
    endDate: z.string().min(1, 'validation.required'),
    reason: z.string().optional(),
  })
  .refine((v) => !v.startDate || !v.endDate || v.endDate >= v.startDate, { path: ['endDate'], message: 'leave.errors.dateOrder' })

function NewRequestDialog({ open, onOpenChange, canStaff }: { open: boolean; onOpenChange: (open: boolean) => void; canStaff: boolean }) {
  const { t, i18n } = useTranslation()
  const create = useCreateLeave()
  const types = useLeaveTypes(open)
  const students = useStudentOptions(open)
  const staff = useStaffOptions(open && canStaff)
  const { failure, setFailure, fail } = useFailure()
  const {
    register,
    handleSubmit,
    reset,
    watch,
    formState: { errors, isSubmitting },
  } = useForm<z.input<typeof requestSchema>>({ resolver: zodResolver(requestSchema), defaultValues: { personType: 'student' } })
  const personType = watch('personType')
  const err = (key?: string) => (key ? t(key) : undefined)
  const typeChoices = (types.data ?? []).filter((x) => x.applies_to === 'both' || x.applies_to === personType)

  const submit = handleSubmit(async (values) => {
    setFailure(null)
    try {
      await create.mutateAsync({
        person_type: values.personType,
        person_id: values.personId,
        leave_type_id: values.leaveTypeId,
        start_date: values.startDate,
        end_date: values.endDate,
        reason: values.reason?.trim() || undefined,
      })
      toast.success(t('leave.created'))
      reset({ personType: 'student' })
      onOpenChange(false)
    } catch (error) {
      fail(error)
    }
  })

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent closeLabel={t('common.close')} className="max-w-lg">
        <DialogHeader title={t('leave.form.title')} description={t('leave.form.description')} />
        <form onSubmit={(e) => { e.preventDefault(); void submit() }} noValidate className="space-y-4">
          {failure && <Alert>{failure}</Alert>}
          {types.isSuccess && types.data.length === 0 && <Alert tone="info">{t('leave.form.noTypes')}</Alert>}
          <div className="grid gap-3 sm:grid-cols-2">
            {canStaff && (
              <Field label={t('leave.form.personType')} htmlFor="lv-kind" className="sm:col-span-2">
                <Select id="lv-kind" {...register('personType')}>
                  <option value="student">{t('attendance.type.student')}</option>
                  <option value="staff">{t('attendance.type.staff')}</option>
                </Select>
              </Field>
            )}
            <Field label={t('leave.form.person')} htmlFor="lv-person" required error={err(errors.personId?.message)} className="sm:col-span-2">
              <Select id="lv-person" aria-invalid={!!errors.personId} {...register('personId')}>
                <option value="">—</option>
                {personType === 'student'
                  ? (students.data ?? []).map((s) => <option key={s.id} value={s.id}>{s.first_name} {s.last_name}</option>)
                  : (staff.data ?? []).map((s) => <option key={s.id} value={s.id}>{s.first_name} {s.last_name}</option>)}
              </Select>
            </Field>
            <Field label={t('leave.form.type')} htmlFor="lv-type" required error={err(errors.leaveTypeId?.message)} className="sm:col-span-2">
              <Select id="lv-type" aria-invalid={!!errors.leaveTypeId} {...register('leaveTypeId')}>
                <option value="">—</option>
                {typeChoices.map((x) => <option key={x.id} value={x.id}>{localized(x.name, i18n.language)}</option>)}
              </Select>
            </Field>
            <Field label={t('leave.form.start')} htmlFor="lv-start" required error={err(errors.startDate?.message)}>
              <Input id="lv-start" type="date" dir="ltr" className="text-start" aria-invalid={!!errors.startDate} {...register('startDate')} />
            </Field>
            <Field label={t('leave.form.end')} htmlFor="lv-end" required error={err(errors.endDate?.message)}>
              <Input id="lv-end" type="date" dir="ltr" className="text-start" aria-invalid={!!errors.endDate} {...register('endDate')} />
            </Field>
            <Field label={t('leave.form.reason')} htmlFor="lv-reason" className="sm:col-span-2">
              <Input id="lv-reason" maxLength={1000} {...register('reason')} />
            </Field>
          </div>
          <div className="flex justify-end gap-2 border-t pt-4">
            <Button type="button" variant="secondary" onClick={() => onOpenChange(false)}>{t('common.cancel')}</Button>
            <Button type="submit" disabled={isSubmitting}>{isSubmitting ? t('common.saving') : t('leave.form.submit')}</Button>
          </div>
        </form>
      </DialogContent>
    </Dialog>
  )
}

// ── Leave types ─────────────────────────────────────────────────────────────

const typeSchema = z.object({
  nameEn: z.string().trim().min(1, 'validation.required'),
  nameAr: z.string().optional(),
  appliesTo: z.enum(['student', 'staff', 'both']),
  requiresApproval: z.boolean(),
  affectsAttendance: z.boolean(),
  affectsRatio: z.boolean(),
})

function LeaveTypesDialog({ open, onOpenChange }: { open: boolean; onOpenChange: (open: boolean) => void }) {
  const { t, i18n } = useTranslation()
  const types = useLeaveTypes(open)
  const create = useCreateLeaveType()
  const { failure, setFailure, fail } = useFailure()
  const {
    register,
    handleSubmit,
    reset,
    formState: { errors, isSubmitting },
  } = useForm<z.input<typeof typeSchema>>({
    resolver: zodResolver(typeSchema),
    defaultValues: { appliesTo: 'both', requiresApproval: true, affectsAttendance: true, affectsRatio: false },
  })

  const submit = handleSubmit(async (values) => {
    setFailure(null)
    try {
      await create.mutateAsync({
        name: values.nameAr?.trim() ? { en: values.nameEn.trim(), ar: values.nameAr.trim() } : { en: values.nameEn.trim() },
        applies_to: values.appliesTo,
        requires_approval: values.requiresApproval,
        affects_attendance: values.affectsAttendance,
        affects_ratio: values.affectsRatio,
      })
      toast.success(t('leave.types.created'))
      reset()
    } catch (error) {
      fail(error)
    }
  })

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent closeLabel={t('common.close')} className="max-w-xl">
        <DialogHeader title={t('leave.types.title')} description={t('leave.types.description')} />
        <ul className="mb-4 flex flex-wrap gap-2">
          {(types.data ?? []).map((x) => (
            <li key={x.id}><Badge tone="info">{localized(x.name, i18n.language)} · {t(`leave.types.appliesTo.${x.applies_to}`)}</Badge></li>
          ))}
          {types.isSuccess && types.data.length === 0 && <li className="text-sm text-muted-foreground">{t('leave.types.none')}</li>}
        </ul>
        <form onSubmit={(e) => { e.preventDefault(); void submit() }} noValidate className="space-y-3 border-t pt-4">
          {failure && <Alert>{failure}</Alert>}
          <div className="grid gap-3 sm:grid-cols-2">
            <Field label={t('leave.types.nameEn')} htmlFor="lt-en" required error={errors.nameEn && t('validation.required')}>
              <Input id="lt-en" aria-invalid={!!errors.nameEn} {...register('nameEn')} />
            </Field>
            <Field label={t('leave.types.nameAr')} htmlFor="lt-ar">
              <Input id="lt-ar" dir="rtl" lang="ar" {...register('nameAr')} />
            </Field>
            <Field label={t('leave.types.appliesToLabel')} htmlFor="lt-applies" className="sm:col-span-2">
              <Select id="lt-applies" {...register('appliesTo')}>
                <option value="both">{t('leave.types.appliesTo.both')}</option>
                <option value="student">{t('leave.types.appliesTo.student')}</option>
                <option value="staff">{t('leave.types.appliesTo.staff')}</option>
              </Select>
            </Field>
          </div>
          {(['requiresApproval', 'affectsAttendance', 'affectsRatio'] as const).map((key) => (
            <label key={key} className="flex items-start gap-2 text-sm">
              <input type="checkbox" className="mt-1 h-4 w-4 accent-primary" {...register(key)} />
              <span>{t(`leave.types.${key}`)}</span>
            </label>
          ))}
          <div className="flex justify-end gap-2 pt-2">
            <Button type="button" variant="secondary" onClick={() => onOpenChange(false)}>{t('common.close')}</Button>
            <Button type="submit" disabled={isSubmitting}>{t('leave.types.add')}</Button>
          </div>
        </form>
      </DialogContent>
    </Dialog>
  )
}

// ── Page ────────────────────────────────────────────────────────────────────

const filters = ['', 'submitted', 'approved', 'rejected', 'cancelled'] as const

export default function LeavesPage() {
  const { t, i18n } = useTranslation()
  const me = useAuth((s) => s.me)
  const canWrite = can(me, 'attendance.write')
  const canDecide = can(me, 'attendance.override')
  const [status, setStatus] = useState<string>('')
  const [requestOpen, setRequestOpen] = useState(false)
  const [typesOpen, setTypesOpen] = useState(false)
  const list = useLeaves({ status: status || undefined })
  const action = useLeaveAction()
  const message = useApiErrorMessage()
  const rows = flatten(list.data)

  const act = async (row: LeaveRequest, kind: 'approve' | 'reject' | 'cancel') => {
    try {
      await action.mutateAsync({ id: row.id, action: kind })
      toast.success(t(`leave.done.${kind}`))
    } catch (error) {
      toast.error(message(error))
    }
  }

  return (
    <>
      <PageHeader
        title={t('leave.title')}
        subtitle={t('leave.subtitle')}
        action={
          <div className="flex gap-2">
            {canDecide && (
              <Button variant="secondary" onClick={() => setTypesOpen(true)}>
                <Settings2 className="h-4 w-4" aria-hidden="true" />{t('leave.types.manage')}
              </Button>
            )}
            {canWrite && (
              <Button onClick={() => setRequestOpen(true)}>
                <Plus className="h-4 w-4" aria-hidden="true" />{t('leave.add')}
              </Button>
            )}
          </div>
        }
      />

      <div className="mb-4 flex flex-wrap gap-2" role="group" aria-label={t('leave.filter')}>
        {filters.map((f) => (
          <Button key={f || 'all'} size="sm" variant={status === f ? 'default' : 'secondary'} aria-pressed={status === f} onClick={() => setStatus(f)}>
            {f ? t(`leave.status.${f}`) : t('leave.status.all')}
          </Button>
        ))}
      </div>

      <Card className="overflow-hidden">
        {list.isLoading ? (
          <ListSkeleton />
        ) : list.isError ? (
          <ListError error={list.error} onRetry={() => void list.refetch()} />
        ) : rows.length === 0 ? (
          <EmptyState icon={<CalendarOff className="h-7 w-7" aria-hidden="true" />} title={t('leave.empty')} description={t('leave.emptyBody')} />
        ) : (
          <>
            <Table>
              <caption className="sr-only">{t('leave.title')}</caption>
              <thead>
                <tr>
                  <Th>{t('leave.columns.person')}</Th>
                  <Th>{t('leave.columns.type')}</Th>
                  <Th>{t('leave.columns.dates')}</Th>
                  <Th>{t('leave.columns.status')}</Th>
                  {(canDecide || canWrite) && <Th><span className="sr-only">{t('common.actions')}</span></Th>}
                </tr>
              </thead>
              <tbody>
                {rows.map((row) => (
                  <tr key={row.id} className="hover:bg-slate-50/70">
                    <Td>
                      <p className="font-bold">{row.person_name}</p>
                      <p className="text-xs text-muted-foreground">{t(`attendance.type.${row.person_type}`)}</p>
                    </Td>
                    <Td>{localized(row.leave_type_name, i18n.language)}</Td>
                    <Td>{formatDate(row.start_date, i18n.language)} – {formatDate(row.end_date, i18n.language)}</Td>
                    <Td><Badge tone={statusTone[row.status]}>{t(`leave.status.${row.status}`)}</Badge></Td>
                    {(canDecide || canWrite) && (
                      <Td>
                        <div className="flex gap-2">
                          {canDecide && row.status === 'submitted' && (
                            <>
                              <Button size="sm" onClick={() => void act(row, 'approve')}><Check className="h-3.5 w-3.5" aria-hidden="true" />{t('leave.approve')}</Button>
                              <Button size="sm" variant="secondary" onClick={() => void act(row, 'reject')}><X className="h-3.5 w-3.5" aria-hidden="true" />{t('leave.reject')}</Button>
                            </>
                          )}
                          {canWrite && (row.status === 'submitted' || row.status === 'approved') && (
                            <Button size="sm" variant="ghost" onClick={() => void act(row, 'cancel')}>{t('leave.cancel')}</Button>
                          )}
                        </div>
                      </Td>
                    )}
                  </tr>
                ))}
              </tbody>
            </Table>
            <LoadMore visible={!!list.hasNextPage} loading={list.isFetchingNextPage} onClick={() => void list.fetchNextPage()} />
          </>
        )}
      </Card>

      {canWrite && <NewRequestDialog open={requestOpen} onOpenChange={setRequestOpen} canStaff={can(me, 'staff.read')} />}
      {canDecide && <LeaveTypesDialog open={typesOpen} onOpenChange={setTypesOpen} />}
    </>
  )
}

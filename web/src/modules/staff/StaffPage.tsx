import { zodResolver } from '@hookform/resolvers/zod'
import { Plus, Search, UsersRound } from 'lucide-react'
import { useEffect, useState } from 'react'
import { useForm } from 'react-hook-form'
import { useTranslation } from 'react-i18next'
import { z } from 'zod'
import { ApiError } from '@/api/client'
import { flatten, useCreateStaff, useStaff } from '@/api/hooks'
import { PageHeader } from '@/components/layout/PageHeader'
import { ListError, ListSkeleton, LoadMore } from '@/components/shared/ListState'
import { StatusBadge } from '@/components/shared/StatusBadge'
import { Button } from '@/components/ui/button'
import { Field, Input, Select } from '@/components/ui/form-controls'
import { Alert, Avatar, Card, Dialog, DialogContent, DialogHeader, EmptyState, Table, Td, Th } from '@/components/ui/surfaces'
import { toast } from '@/components/ui/toaster'
import { useApiErrorMessage } from '@/hooks/use-api-error'
import { emptyToNull, formatDate, todayIso } from '@/lib/format'
import { initials } from '@/lib/utils'
import { can, useAuth } from '@/stores/auth'

const STATUSES = ['active', 'on_leave', 'inactive', 'terminated'] as const
const GENDERS = ['male', 'female', 'other', 'unspecified'] as const

const schema = z.object({
  firstName: z.string().trim().min(1, 'validation.required'),
  lastName: z.string().trim().min(1, 'validation.required'),
  email: z.string().trim().min(1, 'validation.required').email('validation.email'),
  phone: z.string().optional(),
  dateOfBirth: z.string().optional(),
  gender: z.string().optional(),
  jobTitle: z.string().optional(),
  hireDate: z.string().min(1, 'validation.required'),
  status: z.string(),
  emiratesId: z.string().optional(),
  passportNumber: z.string().optional(),
  salary: z
    .string()
    .optional()
    .refine((v) => !v?.trim() || (Number.isFinite(Number(v)) && Number(v) >= 0), 'validation.number'),
})

type Values = z.infer<typeof schema>

function useDebounced<T>(value: T, delay = 300): T {
  const [debounced, setDebounced] = useState(value)
  useEffect(() => {
    const id = setTimeout(() => setDebounced(value), delay)
    return () => clearTimeout(id)
  }, [value, delay])
  return debounced
}

function CreateStaffDialog({ open, onOpenChange }: { open: boolean; onOpenChange: (open: boolean) => void }) {
  const { t } = useTranslation()
  const create = useCreateStaff()
  const message = useApiErrorMessage()
  const [failure, setFailure] = useState<string | null>(null)
  const {
    register,
    handleSubmit,
    reset,
    setError,
    formState: { errors, isSubmitting },
  } = useForm<Values>({ resolver: zodResolver(schema), defaultValues: { status: 'active', hireDate: todayIso() } })
  const err = (key?: string) => (key ? t(key) : undefined)

  const onSubmit = handleSubmit(async (values) => {
    setFailure(null)
    try {
      await create.mutateAsync({
        staff: {
          first_name: values.firstName.trim(),
          last_name: values.lastName.trim(),
          date_of_birth: emptyToNull(values.dateOfBirth),
          gender: emptyToNull(values.gender),
          email: values.email.trim(),
          phone: emptyToNull(values.phone),
          job_title: emptyToNull(values.jobTitle),
          hire_date: values.hireDate,
          end_date: null,
          status: values.status,
          emirates_id: emptyToNull(values.emiratesId),
          passport_number: emptyToNull(values.passportNumber),
          // Money is stored as whole fils: AED × 100, never a floating-point amount.
          salary_amount_fils: values.salary?.trim() ? Math.round(Number(values.salary) * 100) : null,
        },
      })
      toast.success(t('staff.form.created'))
      reset({ status: 'active', hireDate: todayIso() })
      onOpenChange(false)
    } catch (error) {
      if (error instanceof ApiError && error.status === 409) {
        setError('email', { message: 'errors.DUPLICATE_RESOURCE' })
      } else {
        setFailure(message(error))
      }
    }
  })

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent closeLabel={t('common.close')}>
        <DialogHeader title={t('staff.form.createTitle')} description={t('staff.form.createDescription')} />
        <form onSubmit={onSubmit} noValidate className="space-y-6">
          {failure && <Alert>{failure}</Alert>}
          <fieldset className="space-y-3">
            <legend className="mb-2 text-[11px] font-bold uppercase tracking-wide text-muted-foreground">{t('staff.form.sectionPerson')}</legend>
            <div className="grid gap-3 sm:grid-cols-2">
              <Field label={t('staff.form.firstName')} htmlFor="s-first" required error={err(errors.firstName?.message)}>
                <Input id="s-first" aria-invalid={!!errors.firstName} {...register('firstName')} />
              </Field>
              <Field label={t('staff.form.lastName')} htmlFor="s-last" required error={err(errors.lastName?.message)}>
                <Input id="s-last" aria-invalid={!!errors.lastName} {...register('lastName')} />
              </Field>
              <Field label={t('staff.form.email')} htmlFor="s-email" required error={err(errors.email?.message)}>
                <Input id="s-email" type="email" dir="ltr" className="text-start" aria-invalid={!!errors.email} {...register('email')} />
              </Field>
              <Field label={t('staff.form.phone')} htmlFor="s-phone">
                <Input id="s-phone" type="tel" dir="ltr" className="text-start" {...register('phone')} />
              </Field>
              <Field label={t('staff.form.dateOfBirth')} htmlFor="s-dob">
                <Input id="s-dob" type="date" max={todayIso()} dir="ltr" className="text-start" {...register('dateOfBirth')} />
              </Field>
              <Field label={t('staff.form.gender')} htmlFor="s-gender">
                <Select id="s-gender" {...register('gender')}>
                  <option value="">—</option>
                  {GENDERS.map((g) => (
                    <option key={g} value={g}>{t(`students.gender.${g}`)}</option>
                  ))}
                </Select>
              </Field>
            </div>
          </fieldset>
          <fieldset className="space-y-3">
            <legend className="mb-2 text-[11px] font-bold uppercase tracking-wide text-muted-foreground">{t('staff.form.sectionEmployment')}</legend>
            <div className="grid gap-3 sm:grid-cols-3">
              <Field label={t('staff.form.jobTitle')} htmlFor="s-job">
                <Input id="s-job" {...register('jobTitle')} />
              </Field>
              <Field label={t('staff.form.hireDate')} htmlFor="s-hire" required error={err(errors.hireDate?.message)}>
                <Input id="s-hire" type="date" dir="ltr" className="text-start" aria-invalid={!!errors.hireDate} {...register('hireDate')} />
              </Field>
              <Field label={t('staff.form.status')} htmlFor="s-status">
                <Select id="s-status" {...register('status')}>
                  {STATUSES.map((s) => (
                    <option key={s} value={s}>{t(`staff.statuses.${s}`)}</option>
                  ))}
                </Select>
              </Field>
            </div>
          </fieldset>
          <fieldset className="space-y-3">
            <legend className="mb-1 text-[11px] font-bold uppercase tracking-wide text-muted-foreground">{t('staff.form.sectionProtected')}</legend>
            <p className="text-[11px] text-muted-foreground">{t('staff.form.protectedNote')}</p>
            <div className="grid gap-3 sm:grid-cols-3">
              <Field label={t('staff.form.emiratesId')} htmlFor="s-eid">
                <Input id="s-eid" dir="ltr" className="text-start" {...register('emiratesId')} />
              </Field>
              <Field label={t('staff.form.passport')} htmlFor="s-passport">
                <Input id="s-passport" dir="ltr" className="text-start" {...register('passportNumber')} />
              </Field>
              <Field label={t('staff.form.salary')} htmlFor="s-salary" error={err(errors.salary?.message)}>
                <Input id="s-salary" inputMode="decimal" dir="ltr" className="text-start" aria-invalid={!!errors.salary} {...register('salary')} />
              </Field>
            </div>
          </fieldset>
          <div className="flex justify-end gap-2 border-t pt-4">
            <Button type="button" variant="secondary" onClick={() => onOpenChange(false)}>{t('common.cancel')}</Button>
            <Button type="submit" disabled={isSubmitting}>{isSubmitting ? t('common.saving') : t('common.create')}</Button>
          </div>
        </form>
      </DialogContent>
    </Dialog>
  )
}

export default function StaffPage() {
  const { t, i18n } = useTranslation()
  const me = useAuth((s) => s.me)
  const canCreate = can(me, 'staff.write')
  const [search, setSearch] = useState('')
  const [status, setStatus] = useState('')
  const [createOpen, setCreateOpen] = useState(false)
  const query = useStaff({ search: useDebounced(search), status })
  const staff = flatten(query.data)

  return (
    <>
      <PageHeader
        title={t('staff.title')}
        subtitle={t('staff.subtitle')}
        action={
          canCreate && (
            <Button onClick={() => setCreateOpen(true)}>
              <Plus className="h-4 w-4" aria-hidden="true" />
              {t('staff.add')}
            </Button>
          )
        }
      />
      <div className="mb-4 flex flex-wrap gap-3">
        <div className="relative min-w-[220px] flex-1">
          <Search className="pointer-events-none absolute start-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground" aria-hidden="true" />
          <Input type="search" value={search} onChange={(e) => setSearch(e.target.value)} placeholder={t('staff.searchPlaceholder')} aria-label={t('common.search')} className="ps-9" />
        </div>
        <Select value={status} onChange={(e) => setStatus(e.target.value)} aria-label={t('common.status')} className="w-44">
          <option value="">{t('common.status')}</option>
          {STATUSES.map((s) => (
            <option key={s} value={s}>{t(`staff.statuses.${s}`)}</option>
          ))}
        </Select>
      </div>
      <Card className="overflow-hidden">
        {query.isLoading ? (
          <ListSkeleton />
        ) : query.isError ? (
          <ListError error={query.error} onRetry={() => void query.refetch()} />
        ) : staff.length === 0 ? (
          <EmptyState
            icon={<UsersRound className="h-7 w-7" aria-hidden="true" />}
            title={t('staff.empty')}
            description={t('staff.emptyBody')}
            action={canCreate && !search && !status ? <Button onClick={() => setCreateOpen(true)}><Plus className="h-4 w-4" aria-hidden="true" />{t('staff.add')}</Button> : undefined}
          />
        ) : (
          <>
            <Table>
              <caption className="sr-only">{t('staff.title')}</caption>
              <thead>
                <tr>
                  <Th>{t('staff.columns.member')}</Th>
                  <Th>{t('staff.columns.jobTitle')}</Th>
                  <Th>{t('staff.columns.hired')}</Th>
                  <Th>{t('staff.columns.status')}</Th>
                  <Th className="hidden md:table-cell">{t('staff.columns.barcode')}</Th>
                </tr>
              </thead>
              <tbody>
                {staff.map((member) => (
                  <tr key={member.id} className="hover:bg-slate-50/70">
                    <Td>
                      <div className="flex items-center gap-3">
                        <Avatar text={initials(member.first_name, member.last_name)} />
                        <div>
                          <p className="font-bold">{member.first_name} {member.last_name}</p>
                          <p className="text-xs text-muted-foreground" dir="ltr">{member.email}</p>
                        </div>
                      </div>
                    </Td>
                    <Td>{member.job_title ?? '—'}</Td>
                    <Td>{formatDate(member.hire_date, i18n.language)}</Td>
                    <Td><StatusBadge status={member.status} labelKey="staff.statuses" /></Td>
                    <Td className="hidden font-mono text-xs md:table-cell" dir="ltr">{member.barcode}</Td>
                  </tr>
                ))}
              </tbody>
            </Table>
            <LoadMore visible={!!query.hasNextPage} loading={query.isFetchingNextPage} onClick={() => void query.fetchNextPage()} />
          </>
        )}
      </Card>
      {canCreate && <CreateStaffDialog open={createOpen} onOpenChange={setCreateOpen} />}
    </>
  )
}

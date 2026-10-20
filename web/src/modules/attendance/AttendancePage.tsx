import { zodResolver } from '@hookform/resolvers/zod'
import { LogIn, LogOut, ScanLine, UserX } from 'lucide-react'
import { useEffect, useRef, useState } from 'react'
import { useForm } from 'react-hook-form'
import { useTranslation } from 'react-i18next'
import { z } from 'zod'
import { ApiError, api } from '@/api/client'
import {
  flatten,
  useAttendance,
  useCheckIn,
  useCheckOut,
  useGuardians,
  useMarkAbsent,
  useRoomOptions,
  useStudentOptions,
} from '@/api/hooks'
import type { AttendanceRow, StudentSummary } from '@/api/types'
import { PageHeader } from '@/components/layout/PageHeader'
import { ListError, ListSkeleton, LoadMore } from '@/components/shared/ListState'
import { Button } from '@/components/ui/button'
import { Field, Input, Select } from '@/components/ui/form-controls'
import { Alert, Badge, Card, Dialog, DialogContent, DialogHeader, EmptyState, Table, Td, Th, type Tone } from '@/components/ui/surfaces'
import { toast } from '@/components/ui/toaster'
import { localized } from '@/lib/utils'
import { can, isParentOnly, useAuth } from '@/stores/auth'
import { EmergencySheets } from './EmergencySheets'
import { RatioCards } from './RatioCards'

const statusTone: Record<AttendanceRow['status'], Tone> = {
  present: 'success',
  late: 'warning',
  early_leave: 'warning',
  absent: 'danger',
  pending: 'neutral',
}

function timeOf(value: string | null, language: string) {
  if (!value) return '—'
  return new Intl.DateTimeFormat(language === 'ar' ? 'ar-AE-u-nu-latn' : 'en-GB', { hour: '2-digit', minute: '2-digit' }).format(new Date(value))
}

/** Plain-language message for a failed scan, in the active language. */
function useScanMessage() {
  const { t } = useTranslation()
  return (error: unknown): string => {
    if (!(error instanceof ApiError)) return t('errors.NETWORK')
    if (error.status === 404) return t('attendance.scan.notFound')
    if (error.status === 409) return t('attendance.scan.already')
    if (error.status === 422) return t('attendance.scan.notAllowed')
    if (error.status === 403) return t('errors.INSUFFICIENT_PERMISSION')
    return t('errors.UNKNOWN')
  }
}

// ── Pick-up (check-out of a child) ──────────────────────────────────────────

const pickupSchema = z.object({
  guardianId: z.string().min(1, 'validation.required'),
  verified: z.literal(true, { message: 'attendance.pickup.verifyRequired' }),
})

function PickupDialog({ row, onClose }: { row: { personId: string; name: string } | null; onClose: () => void }) {
  const { t } = useTranslation()
  const guardians = useGuardians(row?.personId ?? '')
  const checkOut = useCheckOut()
  const scanMessage = useScanMessage()
  const [failure, setFailure] = useState<string | null>(null)
  const {
    register,
    handleSubmit,
    reset,
    formState: { errors, isSubmitting },
  } = useForm<z.input<typeof pickupSchema>>({ resolver: zodResolver(pickupSchema) })

  const submit = handleSubmit(async (values) => {
    if (!row) return
    setFailure(null)
    try {
      await checkOut.mutateAsync({
        person_type: 'student',
        person_id: row.personId,
        guardian_id: values.guardianId,
        pickup_identity_verified: true,
      })
      toast.success(t('attendance.scan.checkedOut', { name: row.name }))
      reset()
      onClose()
    } catch (error) {
      setFailure(scanMessage(error))
    }
  })

  const allowed = (guardians.data ?? []).filter((g) => g.can_pickup)

  return (
    <Dialog open={!!row} onOpenChange={(open) => !open && onClose()}>
      <DialogContent closeLabel={t('common.close')} className="max-w-md">
        <DialogHeader title={t('attendance.pickup.title', { name: row?.name ?? '' })} description={t('attendance.pickup.description')} />
        <form onSubmit={(e) => { e.preventDefault(); void submit() }} noValidate className="space-y-4">
          {failure && <Alert>{failure}</Alert>}
          <Field label={t('attendance.pickup.guardian')} htmlFor="pk-guardian" required error={errors.guardianId && t(errors.guardianId.message!)}>
            <Select id="pk-guardian" aria-invalid={!!errors.guardianId} {...register('guardianId')}>
              <option value="">—</option>
              {allowed.map((g) => (
                <option key={g.id} value={g.id}>{g.first_name} {g.last_name} ({t(`students.guardians.relationship.${g.relationship}`, { defaultValue: g.relationship })})</option>
              ))}
            </Select>
          </Field>
          {guardians.isSuccess && allowed.length === 0 && <Alert tone="info">{t('attendance.pickup.noneAllowed')}</Alert>}
          <label className="flex items-start gap-2 text-sm">
            <input type="checkbox" className="mt-1 h-4 w-4 accent-primary" {...register('verified')} />
            <span>{t('attendance.pickup.verified')}</span>
          </label>
          {errors.verified && <p role="alert" className="text-xs font-semibold text-red-700">{t('attendance.pickup.verifyRequired')}</p>}
          <div className="flex justify-end gap-2 border-t pt-4">
            <Button type="button" variant="secondary" onClick={onClose}>{t('common.cancel')}</Button>
            <Button type="submit" disabled={isSubmitting}>{t('attendance.pickup.confirm')}</Button>
          </div>
        </form>
      </DialogContent>
    </Dialog>
  )
}

// ── Mark absent ─────────────────────────────────────────────────────────────

function AbsentDialog({ open, onOpenChange }: { open: boolean; onOpenChange: (open: boolean) => void }) {
  const { t } = useTranslation()
  const students = useStudentOptions(open)
  const markAbsent = useMarkAbsent()
  const scanMessage = useScanMessage()
  const [failure, setFailure] = useState<string | null>(null)
  const { register, handleSubmit, reset, formState: { errors, isSubmitting } } = useForm<{ studentId: string; notes?: string }>()

  const submit = handleSubmit(async (values) => {
    setFailure(null)
    if (!values.studentId) {
      setFailure(t('validation.required'))
      return
    }
    try {
      await markAbsent.mutateAsync({ student_id: values.studentId, notes: values.notes?.trim() || undefined })
      toast.success(t('attendance.absent.done'))
      reset()
      onOpenChange(false)
    } catch (error) {
      setFailure(scanMessage(error))
    }
  })

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent closeLabel={t('common.close')} className="max-w-md">
        <DialogHeader title={t('attendance.absent.title')} description={t('attendance.absent.description')} />
        <form onSubmit={(e) => { e.preventDefault(); void submit() }} noValidate className="space-y-4">
          {failure && <Alert>{failure}</Alert>}
          <Field label={t('attendance.absent.student')} htmlFor="ab-student" required error={errors.studentId && t('validation.required')}>
            <Select id="ab-student" {...register('studentId')}>
              <option value="">—</option>
              {(students.data ?? []).map((s: StudentSummary) => (
                <option key={s.id} value={s.id}>{s.first_name} {s.last_name}</option>
              ))}
            </Select>
          </Field>
          <Field label={t('attendance.absent.notes')} htmlFor="ab-notes">
            <Input id="ab-notes" maxLength={500} {...register('notes')} />
          </Field>
          <div className="flex justify-end gap-2 border-t pt-4">
            <Button type="button" variant="secondary" onClick={() => onOpenChange(false)}>{t('common.cancel')}</Button>
            <Button type="submit" disabled={isSubmitting}>{t('attendance.absent.confirm')}</Button>
          </div>
        </form>
      </DialogContent>
    </Dialog>
  )
}

// ── Scan box ────────────────────────────────────────────────────────────────

type Mode = 'in' | 'out'

function ScanBox({ onPickup }: { onPickup: (target: { personId: string; name: string }) => void }) {
  const { t } = useTranslation()
  const checkIn = useCheckIn()
  const checkOut = useCheckOut()
  const scanMessage = useScanMessage()
  const input = useRef<HTMLInputElement>(null)
  const [mode, setMode] = useState<Mode>('in')
  const [code, setCode] = useState('')
  const [result, setResult] = useState<{ tone: 'success' | 'danger' | 'info'; text: string } | null>(null)

  // A USB barcode scanner types like a keyboard, so the box keeps focus ready for the next scan.
  useEffect(() => input.current?.focus(), [mode])

  const submit = async () => {
    const barcode = code.trim()
    if (!barcode) return
    try {
      if (mode === 'in') {
        const row = await checkIn.mutateAsync({ barcode })
        setResult({ tone: 'success', text: t('attendance.scan.checkedIn', { name: row.person_name ?? barcode }) })
      } else {
        const row = await checkOut.mutateAsync({ barcode })
        setResult({ tone: 'success', text: t('attendance.scan.checkedOut', { name: row.person_name ?? barcode }) })
      }
    } catch (error) {
      if (mode === 'out' && error instanceof ApiError && error.status === 400) {
        // A child needs a named guardian: find who this barcode belongs to and ask.
        const found = await api.page<StudentSummary>('/students', { search: barcode, limit: 1 })
        const student = found.items[0]
        if (student) {
          onPickup({ personId: student.id, name: `${student.first_name} ${student.last_name}` })
          setResult({ tone: 'info', text: t('attendance.scan.needPickup', { name: `${student.first_name} ${student.last_name}` }) })
        } else {
          setResult({ tone: 'danger', text: t('attendance.scan.notFound') })
        }
      } else {
        setResult({ tone: 'danger', text: scanMessage(error) })
      }
    } finally {
      setCode('')
      input.current?.focus()
    }
  }

  return (
    <Card className="p-5">
      <div className="mb-4 flex flex-wrap items-center gap-2" role="group" aria-label={t('attendance.scan.mode')}>
        <Button type="button" variant={mode === 'in' ? 'default' : 'secondary'} aria-pressed={mode === 'in'} onClick={() => setMode('in')}>
          <LogIn className="h-4 w-4 rtl:rotate-180" aria-hidden="true" />{t('attendance.scan.modeIn')}
        </Button>
        <Button type="button" variant={mode === 'out' ? 'default' : 'secondary'} aria-pressed={mode === 'out'} onClick={() => setMode('out')}>
          <LogOut className="h-4 w-4 rtl:rotate-180" aria-hidden="true" />{t('attendance.scan.modeOut')}
        </Button>
      </div>
      <form onSubmit={(e) => { e.preventDefault(); void submit() }} className="flex gap-2">
        <div className="relative flex-1">
          <ScanLine className="pointer-events-none absolute start-3 top-1/2 h-5 w-5 -translate-y-1/2 text-muted-foreground" aria-hidden="true" />
          <Input
            ref={input}
            value={code}
            onChange={(e) => setCode(e.target.value)}
            aria-label={t('attendance.scan.label')}
            placeholder={t('attendance.scan.placeholder')}
            autoComplete="off"
            dir="ltr"
            className="h-12 ps-11 text-start text-lg"
          />
        </div>
        <Button type="submit" size="lg" disabled={!code.trim()}>{mode === 'in' ? t('attendance.scan.submitIn') : t('attendance.scan.submitOut')}</Button>
      </form>
      <div aria-live="polite" className="mt-3 min-h-10">
        {result && <Alert tone={result.tone}>{result.text}</Alert>}
      </div>
    </Card>
  )
}

// ── Page ────────────────────────────────────────────────────────────────────

export default function AttendancePage() {
  const { t, i18n } = useTranslation()
  const me = useAuth((s) => s.me)
  const canWrite = can(me, 'attendance.write')
  const [personType, setPersonType] = useState('')
  const [roomId, setRoomId] = useState('')
  const [absentOpen, setAbsentOpen] = useState(false)
  const [pickup, setPickup] = useState<{ personId: string; name: string } | null>(null)
  const rooms = useRoomOptions(can(me, 'centre_structure.read'))
  const list = useAttendance({ person_type: personType || undefined, room_id: roomId || undefined })
  const rows = flatten(list.data)
  const checkOut = useCheckOut()
  const scanMessage = useScanMessage()

  const staffCheckOut = async (row: AttendanceRow) => {
    try {
      await checkOut.mutateAsync({ person_type: 'staff', person_id: row.person_id })
      toast.success(t('attendance.scan.checkedOut', { name: row.person_name ?? '' }))
    } catch (error) {
      toast.error(scanMessage(error))
    }
  }

  return (
    <>
      <PageHeader
        title={t('attendance.title')}
        subtitle={t('attendance.subtitle')}
        action={canWrite && (
          <div className="flex flex-wrap gap-2">
            <EmergencySheets />
            <Button variant="secondary" onClick={() => setAbsentOpen(true)}>
              <UserX className="h-4 w-4" aria-hidden="true" />{t('attendance.absent.open')}
            </Button>
          </div>
        )}
      />

      {canWrite && (
        <div className="mb-6 space-y-6">
          <ScanBox onPickup={setPickup} />
          <section>
            <h2 className="mb-3 font-heading text-lg font-semibold">{t('attendance.ratio.title')}</h2>
            <RatioCards />
          </section>
        </div>
      )}

      <div className="mb-4 flex flex-wrap items-end gap-3">
        <h2 className="me-auto font-heading text-lg font-semibold">{t('attendance.today')}</h2>
        {!isParentOnly(me) && (
          <>
            <Field label={t('attendance.filters.type')} htmlFor="f-type">
              <Select id="f-type" value={personType} onChange={(e) => setPersonType(e.target.value)}>
                <option value="">{t('attendance.filters.all')}</option>
                <option value="student">{t('attendance.type.student')}</option>
                <option value="staff">{t('attendance.type.staff')}</option>
              </Select>
            </Field>
            {rooms.data && (
              <Field label={t('attendance.filters.room')} htmlFor="f-room">
                <Select id="f-room" value={roomId} onChange={(e) => setRoomId(e.target.value)}>
                  <option value="">{t('attendance.filters.all')}</option>
                  {rooms.data.map((r) => <option key={r.id} value={r.id}>{localized(r.name, i18n.language)}</option>)}
                </Select>
              </Field>
            )}
          </>
        )}
      </div>

      <Card className="overflow-hidden">
        {list.isLoading ? (
          <ListSkeleton />
        ) : list.isError ? (
          <ListError error={list.error} onRetry={() => void list.refetch()} />
        ) : rows.length === 0 ? (
          <EmptyState icon={<ScanLine className="h-7 w-7" aria-hidden="true" />} title={t('attendance.empty')} description={t('attendance.emptyBody')} />
        ) : (
          <>
            <Table>
              <caption className="sr-only">{t('attendance.today')}</caption>
              <thead>
                <tr>
                  <Th>{t('attendance.columns.name')}</Th>
                  <Th>{t('attendance.columns.type')}</Th>
                  <Th>{t('attendance.columns.in')}</Th>
                  <Th>{t('attendance.columns.out')}</Th>
                  <Th>{t('attendance.columns.status')}</Th>
                  {canWrite && <Th><span className="sr-only">{t('common.actions')}</span></Th>}
                </tr>
              </thead>
              <tbody>
                {rows.map((row) => (
                  <tr key={row.id} className="hover:bg-slate-50/70">
                    <Td className="font-bold">{row.person_name}</Td>
                    <Td>{t(`attendance.type.${row.person_type}`)}</Td>
                    <Td dir="ltr" className="text-start">{timeOf(row.check_in_at, i18n.language)}</Td>
                    <Td dir="ltr" className="text-start">{timeOf(row.check_out_at, i18n.language)}</Td>
                    <Td><Badge tone={statusTone[row.status]}>{t(`attendance.status.${row.status}`)}</Badge></Td>
                    {canWrite && (
                      <Td>
                        {row.check_in_at && !row.check_out_at && (
                          <Button
                            size="sm"
                            variant="secondary"
                            onClick={() =>
                              row.person_type === 'student'
                                ? setPickup({ personId: row.person_id, name: row.person_name ?? '' })
                                : void staffCheckOut(row)
                            }
                          >
                            <LogOut className="h-3.5 w-3.5 rtl:rotate-180" aria-hidden="true" />{t('attendance.checkOut')}
                          </Button>
                        )}
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

      {canWrite && (
        <>
          <PickupDialog row={pickup} onClose={() => setPickup(null)} />
          <AbsentDialog open={absentOpen} onOpenChange={setAbsentOpen} />
        </>
      )}
    </>
  )
}

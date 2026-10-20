import * as TabsPrimitive from '@radix-ui/react-tabs'
import { zodResolver } from '@hookform/resolvers/zod'
import { Plus, School } from 'lucide-react'
import { useState, type ReactNode } from 'react'
import { useForm } from 'react-hook-form'
import { useTranslation } from 'react-i18next'
import { z } from 'zod'
import { ApiError } from '@/api/client'
import {
  flatten,
  useCreateGroup,
  useCreateLabel,
  useCreateProgram,
  useCreateRoom,
  useGroups,
  useLabels,
  usePrograms,
  useRoomOptions,
  useRooms,
} from '@/api/hooks'
import { PageHeader } from '@/components/layout/PageHeader'
import { ListError, ListSkeleton, LoadMore } from '@/components/shared/ListState'
import { Button } from '@/components/ui/button'
import { Field, Input, Select } from '@/components/ui/form-controls'
import { Alert, Badge, Card, Dialog, DialogContent, DialogHeader, EmptyState, Table, Td, Th } from '@/components/ui/surfaces'
import { toast } from '@/components/ui/toaster'
import { useApiErrorMessage } from '@/hooks/use-api-error'
import { formatTime } from '@/lib/format'
import { cn, localized } from '@/lib/utils'
import { can, useAuth } from '@/stores/auth'

type TabKey = 'rooms' | 'programs' | 'groups' | 'labels'

function names(en: string, ar?: string): Record<string, string> {
  return ar?.trim() ? { en: en.trim(), ar: ar.trim() } : { en: en.trim() }
}

function Tabs({ value, onChange, children }: { value: TabKey; onChange: (v: TabKey) => void; children: ReactNode }) {
  return (
    <TabsPrimitive.Root value={value} onValueChange={(v) => onChange(v as TabKey)}>
      {children}
    </TabsPrimitive.Root>
  )
}

function TabList({ labels }: { labels: Record<TabKey, string> }) {
  return (
    <TabsPrimitive.List className="mb-5 flex gap-1 overflow-x-auto border-b">
      {(Object.keys(labels) as TabKey[]).map((key) => (
        <TabsPrimitive.Trigger
          key={key}
          value={key}
          className={cn(
            'whitespace-nowrap border-b-2 border-transparent px-4 py-2.5 text-sm font-semibold text-muted-foreground transition-colors hover:text-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring',
            'data-[state=active]:border-primary data-[state=active]:font-bold data-[state=active]:text-primary-dark',
          )}
        >
          {labels[key]}
        </TabsPrimitive.Trigger>
      ))}
    </TabsPrimitive.List>
  )
}

/** Shared dialog chrome: title, error banner, cancel/create buttons. */
function FormDialog({
  open,
  onOpenChange,
  title,
  description,
  failure,
  submitting,
  onSubmit,
  children,
}: {
  open: boolean
  onOpenChange: (open: boolean) => void
  title: string
  description?: string
  failure: string | null
  submitting: boolean
  onSubmit: () => void
  children: ReactNode
}) {
  const { t } = useTranslation()
  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent closeLabel={t('common.close')} className="max-w-lg">
        <DialogHeader title={title} description={description} />
        <form onSubmit={(e) => { e.preventDefault(); onSubmit() }} noValidate className="space-y-4">
          {failure && <Alert>{failure}</Alert>}
          {children}
          <div className="flex justify-end gap-2 border-t pt-4">
            <Button type="button" variant="secondary" onClick={() => onOpenChange(false)}>{t('common.cancel')}</Button>
            <Button type="submit" disabled={submitting}>{submitting ? t('common.saving') : t('common.create')}</Button>
          </div>
        </form>
      </DialogContent>
    </Dialog>
  )
}

function useFailure() {
  const message = useApiErrorMessage()
  const { t } = useTranslation()
  const [failure, setFailure] = useState<string | null>(null)
  const fail = (error: unknown) =>
    setFailure(error instanceof ApiError && error.status === 409 ? t('errors.DUPLICATE_RESOURCE') : message(error))
  return { failure, setFailure, fail }
}

// ── Rooms ────────────────────────────────────────────────────────────────────

const roomSchema = z
  .object({
    nameEn: z.string().trim().min(1, 'validation.required'),
    nameAr: z.string().optional(),
    capacity: z.coerce.number({ message: 'validation.number' }).int().min(1, 'validation.positive'),
    minAge: z.coerce.number({ message: 'validation.number' }).int().min(0, 'validation.number'),
    maxAge: z.coerce.number({ message: 'validation.number' }).int().min(0, 'validation.number'),
    ratio: z.coerce.number({ message: 'validation.number' }).int().min(1, 'validation.positive'),
  })
  .refine((v) => v.maxAge >= v.minAge, { path: ['maxAge'], message: 'validation.ageRange' })

type RoomValues = z.input<typeof roomSchema>

function CreateRoomDialog({ open, onOpenChange }: { open: boolean; onOpenChange: (open: boolean) => void }) {
  const { t } = useTranslation()
  const create = useCreateRoom()
  const { failure, setFailure, fail } = useFailure()
  const {
    register,
    handleSubmit,
    reset,
    formState: { errors, isSubmitting },
  } = useForm<RoomValues, unknown, z.output<typeof roomSchema>>({ resolver: zodResolver(roomSchema), defaultValues: { minAge: 0, maxAge: 36 } })
  const err = (key?: string) => (key ? t(key) : undefined)

  const submit = handleSubmit(async (values) => {
    setFailure(null)
    try {
      await create.mutateAsync({
        name: names(values.nameEn, values.nameAr),
        capacity: values.capacity,
        min_age_months: values.minAge,
        max_age_months: values.maxAge,
        ratio_staff_to_student: values.ratio,
        is_active: true,
      })
      toast.success(t('rooms.form.created'))
      reset({ minAge: 0, maxAge: 36 })
      onOpenChange(false)
    } catch (error) {
      fail(error)
    }
  })

  return (
    <FormDialog open={open} onOpenChange={onOpenChange} title={t('rooms.form.createTitle')} description={t('rooms.form.createDescription')} failure={failure} submitting={isSubmitting} onSubmit={() => void submit()}>
      <div className="grid gap-3 sm:grid-cols-2">
        <Field label={t('rooms.form.nameEn')} htmlFor="r-en" required error={err(errors.nameEn?.message)}>
          <Input id="r-en" aria-invalid={!!errors.nameEn} {...register('nameEn')} />
        </Field>
        <Field label={t('rooms.form.nameAr')} htmlFor="r-ar">
          <Input id="r-ar" dir="rtl" lang="ar" {...register('nameAr')} />
        </Field>
        <Field label={t('rooms.form.capacity')} htmlFor="r-cap" required error={err(errors.capacity?.message)}>
          <Input id="r-cap" type="number" inputMode="numeric" min={1} dir="ltr" className="text-start" aria-invalid={!!errors.capacity} {...register('capacity')} />
        </Field>
        <Field label={t('rooms.form.ratio')} htmlFor="r-ratio" hint={t('rooms.form.ratioHint')} required error={err(errors.ratio?.message)}>
          <Input id="r-ratio" type="number" inputMode="numeric" min={1} dir="ltr" className="text-start" aria-invalid={!!errors.ratio} {...register('ratio')} />
        </Field>
        <Field label={t('rooms.form.minAge')} htmlFor="r-min" error={err(errors.minAge?.message)}>
          <Input id="r-min" type="number" inputMode="numeric" min={0} dir="ltr" className="text-start" aria-invalid={!!errors.minAge} {...register('minAge')} />
        </Field>
        <Field label={t('rooms.form.maxAge')} htmlFor="r-max" error={err(errors.maxAge?.message)}>
          <Input id="r-max" type="number" inputMode="numeric" min={0} dir="ltr" className="text-start" aria-invalid={!!errors.maxAge} {...register('maxAge')} />
        </Field>
      </div>
    </FormDialog>
  )
}

function RoomsTab({ canWrite, onAdd }: { canWrite: boolean; onAdd: () => void }) {
  const { t, i18n } = useTranslation()
  const query = useRooms()
  const rooms = flatten(query.data)
  if (query.isLoading) return <ListSkeleton />
  if (query.isError) return <ListError error={query.error} onRetry={() => void query.refetch()} />
  if (rooms.length === 0) {
    return (
      <EmptyState
        icon={<School className="h-7 w-7" aria-hidden="true" />}
        title={t('rooms.empty')}
        description={t('rooms.emptyBody')}
        action={canWrite ? <Button onClick={onAdd}><Plus className="h-4 w-4" aria-hidden="true" />{t('rooms.add')}</Button> : undefined}
      />
    )
  }
  return (
    <>
      <Table>
        <caption className="sr-only">{t('rooms.tabs.rooms')}</caption>
        <thead>
          <tr>
            <Th>{t('rooms.columns.room')}</Th>
            <Th>{t('rooms.columns.capacity')}</Th>
            <Th>{t('rooms.columns.ages')}</Th>
            <Th>{t('rooms.columns.ratio')}</Th>
            <Th>{t('rooms.columns.students')}</Th>
            <Th>{t('rooms.columns.status')}</Th>
          </tr>
        </thead>
        <tbody>
          {rooms.map((room) => (
            <tr key={room.id} className="hover:bg-slate-50/70">
              <Td className="font-bold">{localized(room.name, i18n.language)}</Td>
              <Td>{room.capacity}</Td>
              <Td>{t('rooms.ageRange', { min: room.min_age_months, max: room.max_age_months })}</Td>
              <Td>{t('rooms.ratioValue', { count: room.ratio_staff_to_student })}</Td>
              <Td>{room.student_count} / {room.capacity}</Td>
              <Td><Badge tone={room.is_active ? 'success' : 'neutral'}>{room.is_active ? t('rooms.active') : t('rooms.inactive')}</Badge></Td>
            </tr>
          ))}
        </tbody>
      </Table>
      <LoadMore visible={!!query.hasNextPage} loading={query.isFetchingNextPage} onClick={() => void query.fetchNextPage()} />
    </>
  )
}

// ── Programs ─────────────────────────────────────────────────────────────────

const programSchema = z
  .object({
    nameEn: z.string().trim().min(1, 'validation.required'),
    nameAr: z.string().optional(),
    days: z.coerce.number({ message: 'validation.number' }).int().min(1, 'validation.positive').max(7, 'validation.number'),
    start: z.string().min(1, 'validation.required'),
    end: z.string().min(1, 'validation.required'),
    ageGroup: z.string().trim().min(1, 'validation.required'),
  })
  .refine((v) => !v.start || !v.end || v.end > v.start, { path: ['end'], message: 'validation.timeOrder' })

function CreateProgramDialog({ open, onOpenChange }: { open: boolean; onOpenChange: (open: boolean) => void }) {
  const { t } = useTranslation()
  const create = useCreateProgram()
  const { failure, setFailure, fail } = useFailure()
  const {
    register,
    handleSubmit,
    reset,
    formState: { errors, isSubmitting },
  } = useForm<z.input<typeof programSchema>, unknown, z.output<typeof programSchema>>({ resolver: zodResolver(programSchema), defaultValues: { days: 5 } })
  const err = (key?: string) => (key ? t(key) : undefined)

  const submit = handleSubmit(async (values) => {
    setFailure(null)
    try {
      await create.mutateAsync({
        name: names(values.nameEn, values.nameAr),
        description: null,
        days_per_week: values.days,
        start_time: `${values.start}:00`,
        end_time: `${values.end}:00`,
        age_group: values.ageGroup.trim(),
        is_active: true,
      })
      toast.success(t('rooms.program.created'))
      reset({ days: 5 })
      onOpenChange(false)
    } catch (error) {
      fail(error)
    }
  })

  return (
    <FormDialog open={open} onOpenChange={onOpenChange} title={t('rooms.program.createTitle')} failure={failure} submitting={isSubmitting} onSubmit={() => void submit()}>
      <div className="grid gap-3 sm:grid-cols-2">
        <Field label={t('rooms.program.name')} htmlFor="p-en" required error={err(errors.nameEn?.message)}>
          <Input id="p-en" aria-invalid={!!errors.nameEn} {...register('nameEn')} />
        </Field>
        <Field label={t('rooms.program.nameAr')} htmlFor="p-ar">
          <Input id="p-ar" dir="rtl" lang="ar" {...register('nameAr')} />
        </Field>
        <Field label={t('rooms.program.ageGroup')} htmlFor="p-age" required error={err(errors.ageGroup?.message)}>
          <Input id="p-age" aria-invalid={!!errors.ageGroup} {...register('ageGroup')} />
        </Field>
        <Field label={t('rooms.program.days')} htmlFor="p-days" required error={err(errors.days?.message)}>
          <Input id="p-days" type="number" min={1} max={7} dir="ltr" className="text-start" aria-invalid={!!errors.days} {...register('days')} />
        </Field>
        <Field label={t('rooms.program.start')} htmlFor="p-start" required error={err(errors.start?.message)}>
          <Input id="p-start" type="time" dir="ltr" className="text-start" aria-invalid={!!errors.start} {...register('start')} />
        </Field>
        <Field label={t('rooms.program.end')} htmlFor="p-end" required error={err(errors.end?.message)}>
          <Input id="p-end" type="time" dir="ltr" className="text-start" aria-invalid={!!errors.end} {...register('end')} />
        </Field>
      </div>
    </FormDialog>
  )
}

function ProgramsTab() {
  const { t, i18n } = useTranslation()
  const query = usePrograms()
  const items = flatten(query.data)
  if (query.isLoading) return <ListSkeleton />
  if (query.isError) return <ListError error={query.error} onRetry={() => void query.refetch()} />
  if (items.length === 0) return <EmptyState icon={<School className="h-7 w-7" aria-hidden="true" />} title={t('rooms.program.empty')} />
  return (
    <>
      <Table>
        <caption className="sr-only">{t('rooms.tabs.programs')}</caption>
        <thead>
          <tr>
            <Th>{t('rooms.program.columns.program')}</Th>
            <Th>{t('rooms.program.columns.ageGroup')}</Th>
            <Th>{t('rooms.program.columns.days')}</Th>
            <Th>{t('rooms.program.columns.hours')}</Th>
          </tr>
        </thead>
        <tbody>
          {items.map((p) => (
            <tr key={p.id} className="hover:bg-slate-50/70">
              <Td className="font-bold">{localized(p.name, i18n.language)}</Td>
              <Td>{p.age_group}</Td>
              <Td>{p.days_per_week}</Td>
              <Td dir="ltr" className="text-start">{formatTime(p.start_time)} – {formatTime(p.end_time)}</Td>
            </tr>
          ))}
        </tbody>
      </Table>
      <LoadMore visible={!!query.hasNextPage} loading={query.isFetchingNextPage} onClick={() => void query.fetchNextPage()} />
    </>
  )
}

// ── Groups ───────────────────────────────────────────────────────────────────

const groupSchema = z.object({
  nameEn: z.string().trim().min(1, 'validation.required'),
  nameAr: z.string().optional(),
  roomId: z.string().min(1, 'validation.required'),
})

type GroupValues = z.infer<typeof groupSchema>

function CreateGroupDialog({ open, onOpenChange }: { open: boolean; onOpenChange: (open: boolean) => void }) {
  const { t, i18n } = useTranslation()
  const create = useCreateGroup()
  const rooms = useRoomOptions(open)
  const { failure, setFailure, fail } = useFailure()
  const {
    register,
    handleSubmit,
    reset,
    formState: { errors, isSubmitting },
  } = useForm<GroupValues>({ resolver: zodResolver(groupSchema) })
  const err = (key?: string) => (key ? t(key) : undefined)

  const submit = handleSubmit(async (values) => {
    setFailure(null)
    try {
      await create.mutateAsync({ name: names(values.nameEn, values.nameAr), room_id: values.roomId })
      toast.success(t('rooms.group.created'))
      reset()
      onOpenChange(false)
    } catch (error) {
      fail(error)
    }
  })

  return (
    <FormDialog open={open} onOpenChange={onOpenChange} title={t('rooms.group.createTitle')} failure={failure} submitting={isSubmitting} onSubmit={() => void submit()}>
      <div className="grid gap-3 sm:grid-cols-2">
        <Field label={t('rooms.group.name')} htmlFor="g-en" required error={err(errors.nameEn?.message)}>
          <Input id="g-en" aria-invalid={!!errors.nameEn} {...register('nameEn')} />
        </Field>
        <Field label={t('rooms.group.nameAr')} htmlFor="g-ar">
          <Input id="g-ar" dir="rtl" lang="ar" {...register('nameAr')} />
        </Field>
        <Field label={t('rooms.group.room')} htmlFor="g-room" required error={err(errors.roomId?.message)} className="sm:col-span-2">
          <Select id="g-room" aria-invalid={!!errors.roomId} {...register('roomId')}>
            <option value="">—</option>
            {rooms.data?.map((room) => (
              <option key={room.id} value={room.id}>{localized(room.name, i18n.language)}</option>
            ))}
          </Select>
        </Field>
      </div>
    </FormDialog>
  )
}

function GroupsTab() {
  const { t, i18n } = useTranslation()
  const query = useGroups()
  const rooms = useRoomOptions()
  const items = flatten(query.data)
  if (query.isLoading) return <ListSkeleton />
  if (query.isError) return <ListError error={query.error} onRetry={() => void query.refetch()} />
  if (items.length === 0) return <EmptyState icon={<School className="h-7 w-7" aria-hidden="true" />} title={t('rooms.group.empty')} />
  return (
    <>
      <Table>
        <caption className="sr-only">{t('rooms.tabs.groups')}</caption>
        <thead>
          <tr>
            <Th>{t('rooms.group.columns.group')}</Th>
            <Th>{t('rooms.group.columns.room')}</Th>
          </tr>
        </thead>
        <tbody>
          {items.map((g) => (
            <tr key={g.id} className="hover:bg-slate-50/70">
              <Td className="font-bold">{localized(g.name, i18n.language)}</Td>
              <Td>{localized(rooms.data?.find((r) => r.id === g.room_id)?.name, i18n.language)}</Td>
            </tr>
          ))}
        </tbody>
      </Table>
      <LoadMore visible={!!query.hasNextPage} loading={query.isFetchingNextPage} onClick={() => void query.fetchNextPage()} />
    </>
  )
}

// ── Labels ───────────────────────────────────────────────────────────────────

const labelSchema = z.object({
  nameEn: z.string().trim().min(1, 'validation.required'),
  nameAr: z.string().optional(),
  color: z.string().regex(/^#[0-9A-Fa-f]{6}$/, 'validation.color'),
})

type LabelValues = z.infer<typeof labelSchema>

function CreateLabelDialog({ open, onOpenChange }: { open: boolean; onOpenChange: (open: boolean) => void }) {
  const { t } = useTranslation()
  const create = useCreateLabel()
  const { failure, setFailure, fail } = useFailure()
  const {
    register,
    handleSubmit,
    reset,
    formState: { errors, isSubmitting },
  } = useForm<LabelValues>({ resolver: zodResolver(labelSchema), defaultValues: { color: '#E07A5F' } })
  const err = (key?: string) => (key ? t(key) : undefined)

  const submit = handleSubmit(async (values) => {
    setFailure(null)
    try {
      await create.mutateAsync({ name: names(values.nameEn, values.nameAr), color: values.color })
      toast.success(t('rooms.label.created'))
      reset({ color: '#E07A5F' })
      onOpenChange(false)
    } catch (error) {
      fail(error)
    }
  })

  return (
    <FormDialog open={open} onOpenChange={onOpenChange} title={t('rooms.label.createTitle')} failure={failure} submitting={isSubmitting} onSubmit={() => void submit()}>
      <div className="grid gap-3 sm:grid-cols-2">
        <Field label={t('rooms.label.name')} htmlFor="l-en" required error={err(errors.nameEn?.message)}>
          <Input id="l-en" aria-invalid={!!errors.nameEn} {...register('nameEn')} />
        </Field>
        <Field label={t('rooms.label.nameAr')} htmlFor="l-ar">
          <Input id="l-ar" dir="rtl" lang="ar" {...register('nameAr')} />
        </Field>
        <Field label={t('rooms.label.color')} htmlFor="l-color" required error={err(errors.color?.message)}>
          <Input id="l-color" dir="ltr" className="text-start" aria-invalid={!!errors.color} {...register('color')} />
        </Field>
      </div>
    </FormDialog>
  )
}

function LabelsTab() {
  const { t, i18n } = useTranslation()
  const query = useLabels()
  const items = flatten(query.data)
  if (query.isLoading) return <ListSkeleton />
  if (query.isError) return <ListError error={query.error} onRetry={() => void query.refetch()} />
  if (items.length === 0) return <EmptyState icon={<School className="h-7 w-7" aria-hidden="true" />} title={t('rooms.label.empty')} />
  return (
    <>
      <Table>
        <caption className="sr-only">{t('rooms.tabs.labels')}</caption>
        <thead>
          <tr>
            <Th>{t('rooms.label.columns.label')}</Th>
            <Th>{t('rooms.label.columns.color')}</Th>
          </tr>
        </thead>
        <tbody>
          {items.map((l) => (
            <tr key={l.id} className="hover:bg-slate-50/70">
              <Td className="font-bold">{localized(l.name, i18n.language)}</Td>
              <Td>
                <span className="inline-flex items-center gap-2 font-mono text-xs" dir="ltr">
                  <span aria-hidden="true" className="h-4 w-4 rounded-full border" style={{ backgroundColor: l.color }} />
                  {l.color}
                </span>
              </Td>
            </tr>
          ))}
        </tbody>
      </Table>
      <LoadMore visible={!!query.hasNextPage} loading={query.isFetchingNextPage} onClick={() => void query.fetchNextPage()} />
    </>
  )
}

// ── Page ─────────────────────────────────────────────────────────────────────

export default function RoomsPage() {
  const { t } = useTranslation()
  const me = useAuth((s) => s.me)
  const canWrite = can(me, 'centre_structure.write')
  const [tab, setTab] = useState<TabKey>('rooms')
  const [dialog, setDialog] = useState<TabKey | null>(null)
  const addLabels: Record<TabKey, string> = {
    rooms: t('rooms.add'),
    programs: t('rooms.program.add'),
    groups: t('rooms.group.add'),
    labels: t('rooms.label.add'),
  }

  return (
    <>
      <PageHeader
        title={t('rooms.title')}
        subtitle={t('rooms.subtitle')}
        action={
          canWrite && (
            <Button onClick={() => setDialog(tab)}>
              <Plus className="h-4 w-4" aria-hidden="true" />
              {addLabels[tab]}
            </Button>
          )
        }
      />
      <Tabs value={tab} onChange={setTab}>
        <TabList labels={{ rooms: t('rooms.tabs.rooms'), programs: t('rooms.tabs.programs'), groups: t('rooms.tabs.groups'), labels: t('rooms.tabs.labels') }} />
        <Card className="overflow-hidden">
          <TabsPrimitive.Content value="rooms"><RoomsTab canWrite={canWrite} onAdd={() => setDialog('rooms')} /></TabsPrimitive.Content>
          <TabsPrimitive.Content value="programs"><ProgramsTab /></TabsPrimitive.Content>
          <TabsPrimitive.Content value="groups"><GroupsTab /></TabsPrimitive.Content>
          <TabsPrimitive.Content value="labels"><LabelsTab /></TabsPrimitive.Content>
        </Card>
      </Tabs>
      {canWrite && (
        <>
          <CreateRoomDialog open={dialog === 'rooms'} onOpenChange={(o) => setDialog(o ? 'rooms' : null)} />
          <CreateProgramDialog open={dialog === 'programs'} onOpenChange={(o) => setDialog(o ? 'programs' : null)} />
          <CreateGroupDialog open={dialog === 'groups'} onOpenChange={(o) => setDialog(o ? 'groups' : null)} />
          <CreateLabelDialog open={dialog === 'labels'} onOpenChange={(o) => setDialog(o ? 'labels' : null)} />
        </>
      )}
    </>
  )
}

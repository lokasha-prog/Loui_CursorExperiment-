import { Baby, ChevronRight, Plus, Search } from 'lucide-react'
import { useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Link, useSearchParams } from 'react-router-dom'
import { flatten, useRoomOptions, useStudents } from '@/api/hooks'
import { ListError, ListSkeleton, LoadMore } from '@/components/shared/ListState'
import { StatusBadge } from '@/components/shared/StatusBadge'
import { PageHeader } from '@/components/layout/PageHeader'
import { Button } from '@/components/ui/button'
import { Input, Select } from '@/components/ui/form-controls'
import { Avatar, Card, EmptyState, Table, Td, Th } from '@/components/ui/surfaces'
import { ageLabel } from '@/lib/format'
import { initials, localized } from '@/lib/utils'
import { can, isParentOnly, useAuth } from '@/stores/auth'
import { CreateStudentDialog } from './StudentForm'

const STATUSES = ['signed_up', 'active', 'on_hold', 'inactive', 'withdrawn', 'transferred']

function useDebounced<T>(value: T, delay = 300): T {
  const [debounced, setDebounced] = useState(value)
  useEffect(() => {
    const id = setTimeout(() => setDebounced(value), delay)
    return () => clearTimeout(id)
  }, [value, delay])
  return debounced
}

export default function StudentsPage() {
  const { t, i18n } = useTranslation()
  const me = useAuth((s) => s.me)
  const parent = isParentOnly(me)
  const canCreate = can(me, 'students.write')
  const [params, setParams] = useSearchParams()
  const [search, setSearch] = useState('')
  const [status, setStatus] = useState('')
  const [roomId, setRoomId] = useState('')
  const [createOpen, setCreateOpen] = useState(params.get('new') === '1' && canCreate)
  const debouncedSearch = useDebounced(search)
  const query = useStudents({ search: debouncedSearch, status, roomId })
  const rooms = useRoomOptions(!parent && can(me, 'centre_structure.read'))
  const students = flatten(query.data)
  const roomName = (id: string | null) => localized(rooms.data?.find((r) => r.id === id)?.name, i18n.language) || t('students.unassigned')

  useEffect(() => {
    if (params.get('new')) {
      params.delete('new')
      setParams(params, { replace: true })
    }
  }, [params, setParams])

  return (
    <>
      <PageHeader
        title={parent ? t('students.myChildrenTitle') : t('students.title')}
        subtitle={t('students.subtitle')}
        action={
          canCreate && (
            <Button onClick={() => setCreateOpen(true)}>
              <Plus className="h-4 w-4" aria-hidden="true" />
              {t('students.add')}
            </Button>
          )
        }
      />

      {!parent && (
        <div className="mb-4 flex flex-wrap gap-3">
          <div className="relative min-w-[220px] flex-1">
            <Search className="pointer-events-none absolute start-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground" aria-hidden="true" />
            <Input
              type="search"
              value={search}
              onChange={(e) => setSearch(e.target.value)}
              placeholder={t('students.searchPlaceholder')}
              aria-label={t('common.search')}
              className="ps-9"
            />
          </div>
          <Select value={status} onChange={(e) => setStatus(e.target.value)} aria-label={t('students.filterStatus')} className="w-44">
            <option value="">{t('students.filterStatus')}</option>
            {STATUSES.map((s) => (
              <option key={s} value={s}>{t(`students.status.${s}`)}</option>
            ))}
          </Select>
          {(rooms.data?.length ?? 0) > 0 && (
            <Select value={roomId} onChange={(e) => setRoomId(e.target.value)} aria-label={t('students.filterRoom')} className="w-44">
              <option value="">{t('students.filterRoom')}</option>
              {rooms.data?.map((room) => (
                <option key={room.id} value={room.id}>{localized(room.name, i18n.language)}</option>
              ))}
            </Select>
          )}
        </div>
      )}

      <Card className="overflow-hidden">
        {query.isLoading ? (
          <ListSkeleton />
        ) : query.isError ? (
          <ListError error={query.error} onRetry={() => void query.refetch()} />
        ) : students.length === 0 ? (
          <EmptyState
            icon={<Baby className="h-7 w-7" aria-hidden="true" />}
            title={parent ? t('students.emptyParent') : t('students.empty')}
            description={parent ? undefined : t('students.emptyBody')}
            action={
              canCreate && !search && !status && !roomId ? (
                <Button onClick={() => setCreateOpen(true)}>
                  <Plus className="h-4 w-4" aria-hidden="true" />
                  {t('students.add')}
                </Button>
              ) : undefined
            }
          />
        ) : (
          <>
            <Table>
              <caption className="sr-only">{t('students.title')}</caption>
              <thead>
                <tr>
                  <Th>{t('students.columns.student')}</Th>
                  <Th>{t('students.columns.age')}</Th>
                  <Th>{t('students.columns.room')}</Th>
                  <Th>{t('students.columns.status')}</Th>
                  <Th className="hidden md:table-cell">{t('students.columns.barcode')}</Th>
                  <Th><span className="sr-only">{t('common.actions')}</span></Th>
                </tr>
              </thead>
              <tbody>
                {students.map((student) => {
                  const arabicName = [student.first_name_arabic, student.last_name_arabic].filter(Boolean).join(' ')
                  return (
                    <tr key={student.id} className="transition-colors hover:bg-slate-50/70">
                      <Td>
                        <Link to={`/students/${student.id}`} className="flex items-center gap-3 font-bold hover:text-primary-dark">
                          <Avatar text={initials(student.first_name, student.last_name)} />
                          <span>
                            {i18n.language === 'ar' && arabicName ? arabicName : `${student.first_name} ${student.last_name}`}
                            {arabicName && i18n.language !== 'ar' && (
                              <span lang="ar" dir="rtl" className="block text-xs font-normal text-muted-foreground">{arabicName}</span>
                            )}
                          </span>
                        </Link>
                      </Td>
                      <Td>{ageLabel(student.date_of_birth, t)}</Td>
                      <Td>{roomName(student.room_id)}</Td>
                      <Td><StatusBadge status={student.status} labelKey="students.status" /></Td>
                      <Td className="hidden font-mono text-xs md:table-cell" dir="ltr">{student.barcode}</Td>
                      <Td className="text-end">
                        <Link to={`/students/${student.id}`} aria-label={`${t('common.view')}: ${student.first_name} ${student.last_name}`} className="inline-flex text-muted-foreground hover:text-foreground">
                          <ChevronRight className="h-4 w-4 rtl:rotate-180" aria-hidden="true" />
                        </Link>
                      </Td>
                    </tr>
                  )
                })}
              </tbody>
            </Table>
            <LoadMore visible={!!query.hasNextPage} loading={query.isFetchingNextPage} onClick={() => void query.fetchNextPage()} />
          </>
        )}
      </Card>

      {canCreate && <CreateStudentDialog open={createOpen} onOpenChange={setCreateOpen} />}
    </>
  )
}

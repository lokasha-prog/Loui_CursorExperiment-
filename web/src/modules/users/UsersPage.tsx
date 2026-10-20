import { Plus, ShieldCheck } from 'lucide-react'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { flatten, useSetUserStatus, useUsers } from '@/api/hooks'
import { PageHeader } from '@/components/layout/PageHeader'
import { ListError, ListSkeleton, LoadMore } from '@/components/shared/ListState'
import { StatusBadge } from '@/components/shared/StatusBadge'
import { Button } from '@/components/ui/button'
import { Avatar, Badge, Card, EmptyState, Table, Td, Th } from '@/components/ui/surfaces'
import { toast } from '@/components/ui/toaster'
import { useApiErrorMessage } from '@/hooks/use-api-error'
import { formatDate } from '@/lib/format'
import { initials } from '@/lib/utils'
import { useAuth } from '@/stores/auth'
import { CreateUserDialog } from './UserForm'

export default function UsersPage() {
  const { t, i18n } = useTranslation()
  const me = useAuth((s) => s.me)
  const query = useUsers()
  const setStatus = useSetUserStatus()
  const message = useApiErrorMessage()
  const [open, setOpen] = useState(false)
  const users = flatten(query.data)

  const toggle = async (id: string, status: string) => {
    try {
      await setStatus.mutateAsync({ id, status: status === 'suspended' ? 'active' : 'suspended' })
      toast.success(t('users.statusChanged'))
    } catch (error) {
      toast.error(message(error))
    }
  }

  return (
    <>
      <PageHeader
        title={t('users.title')}
        subtitle={t('users.subtitle')}
        action={
          <Button onClick={() => setOpen(true)}>
            <Plus className="h-4 w-4" aria-hidden="true" />
            {t('users.add')}
          </Button>
        }
      />
      <Card className="overflow-hidden">
        {query.isLoading ? (
          <ListSkeleton />
        ) : query.isError ? (
          <ListError error={query.error} onRetry={() => void query.refetch()} />
        ) : users.length === 0 ? (
          <EmptyState icon={<ShieldCheck className="h-7 w-7" aria-hidden="true" />} title={t('users.empty')} />
        ) : (
          <>
            <Table>
              <caption className="sr-only">{t('users.title')}</caption>
              <thead>
                <tr>
                  <Th>{t('users.columns.person')}</Th>
                  <Th>{t('users.columns.roles')}</Th>
                  <Th>{t('users.columns.status')}</Th>
                  <Th className="hidden md:table-cell">{t('users.columns.lastSignIn')}</Th>
                  <Th><span className="sr-only">{t('common.actions')}</span></Th>
                </tr>
              </thead>
              <tbody>
                {users.map((user) => (
                  <tr key={user.id} className="hover:bg-slate-50/70">
                    <Td>
                      <div className="flex items-center gap-3">
                        <Avatar text={initials(user.first_name, user.last_name)} />
                        <div>
                          <p className="font-bold">{user.first_name} {user.last_name}</p>
                          <p className="text-xs text-muted-foreground" dir="ltr">{user.email}</p>
                        </div>
                      </div>
                    </Td>
                    <Td>
                      <div className="flex flex-wrap gap-1">
                        {user.roles.map((role) => (
                          <Badge key={role} tone="info">{t(`roles.${role}`)}</Badge>
                        ))}
                      </div>
                    </Td>
                    <Td><StatusBadge status={user.status} labelKey="users.status" /></Td>
                    <Td className="hidden text-muted-foreground md:table-cell">
                      {user.last_login_at ? formatDate(user.last_login_at, i18n.language) : t('users.never')}
                    </Td>
                    <Td className="text-end">
                      {user.id !== me?.id && (
                        <Button variant="ghost" size="sm" onClick={() => void toggle(user.id, user.status)}>
                          {user.status === 'suspended' ? t('users.reactivate') : t('users.suspend')}
                        </Button>
                      )}
                    </Td>
                  </tr>
                ))}
              </tbody>
            </Table>
            <LoadMore visible={!!query.hasNextPage} loading={query.isFetchingNextPage} onClick={() => void query.fetchNextPage()} />
          </>
        )}
      </Card>
      <CreateUserDialog open={open} onOpenChange={setOpen} />
    </>
  )
}

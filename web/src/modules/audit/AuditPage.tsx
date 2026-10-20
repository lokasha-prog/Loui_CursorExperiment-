import { History } from 'lucide-react'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { flatten, useAuditLogs } from '@/api/hooks'
import { PageHeader } from '@/components/layout/PageHeader'
import { ListError, ListSkeleton, LoadMore } from '@/components/shared/ListState'
import { Field, Input, Select } from '@/components/ui/form-controls'
import { RetentionPanel } from './RetentionPanel'
import { Badge, Card, EmptyState, Table, Td, Th, type Tone } from '@/components/ui/surfaces'

const ACTIONS = ['create', 'update', 'delete', 'restore', 'export', 'login', 'logout', 'permission_change']
const tone: Record<string, Tone> = { create: 'success', update: 'info', delete: 'danger', export: 'warning', login: 'neutral', logout: 'neutral' }

/** Read-only view of the append-only change history, for super administrators. */
export default function AuditPage() {
  const { t, i18n } = useTranslation()
  const [action, setAction] = useState('')
  const [entity, setEntity] = useState('')
  const [from, setFrom] = useState('')
  const [to, setTo] = useState('')
  const list = useAuditLogs({ action: action || undefined, entity_type: entity.trim() || undefined, from: from || undefined, to: to || undefined })
  const rows = flatten(list.data)
  const when = (v: string) => new Intl.DateTimeFormat(i18n.language === 'ar' ? 'ar-AE-u-nu-latn' : 'en-GB', { dateStyle: 'medium', timeStyle: 'medium' }).format(new Date(v))

  return (
    <>
      <PageHeader title={t('audit.title')} subtitle={t('audit.subtitle')} />
      <RetentionPanel />
      <div className="mb-4 flex flex-wrap items-end gap-3">
        <Field label={t('audit.action')} htmlFor="au-action">
          <Select id="au-action" value={action} onChange={(e) => setAction(e.target.value)}>
            <option value="">{t('audit.all')}</option>
            {ACTIONS.map((a) => <option key={a} value={a}>{t(`audit.actions.${a}`)}</option>)}
          </Select>
        </Field>
        <Field label={t('audit.entity')} htmlFor="au-entity">
          <Input id="au-entity" dir="ltr" className="text-start" value={entity} onChange={(e) => setEntity(e.target.value)} placeholder="Student" />
        </Field>
        <Field label={t('audit.from')} htmlFor="au-from"><Input id="au-from" type="date" dir="ltr" className="text-start" value={from} onChange={(e) => setFrom(e.target.value)} /></Field>
        <Field label={t('audit.to')} htmlFor="au-to"><Input id="au-to" type="date" dir="ltr" className="text-start" value={to} onChange={(e) => setTo(e.target.value)} /></Field>
      </div>
      <Card className="overflow-hidden">
        {list.isLoading ? <ListSkeleton /> : list.isError ? <ListError error={list.error} onRetry={() => void list.refetch()} /> :
          rows.length === 0 ? <EmptyState icon={<History className="h-7 w-7" aria-hidden="true" />} title={t('audit.empty')} /> : (
            <>
              <Table>
                <caption className="sr-only">{t('audit.title')}</caption>
                <thead>
                  <tr>
                    <Th>{t('audit.columns.when')}</Th><Th>{t('audit.columns.who')}</Th><Th>{t('audit.columns.action')}</Th><Th>{t('audit.columns.record')}</Th><Th>{t('audit.columns.changes')}</Th>
                  </tr>
                </thead>
                <tbody>
                  {rows.map((r) => (
                    <tr key={r.id} className="hover:bg-slate-50/70">
                      <Td className="whitespace-nowrap text-xs">{when(r.occurred_at)}</Td>
                      <Td dir="ltr" className="text-start text-xs">{r.user_email ?? t('audit.system')}</Td>
                      <Td><Badge tone={tone[r.action] ?? 'neutral'}>{i18n.exists(`audit.actions.${r.action}`) ? t(`audit.actions.${r.action}`) : r.action}</Badge></Td>
                      <Td dir="ltr" className="text-start font-mono text-xs">{r.entity_type}</Td>
                      <Td className="text-xs" dir="ltr">
                        {r.changed_fields ? (
                          <span title={Object.keys(r.changed_fields).join(', ')}>{Object.keys(r.changed_fields).slice(0, 3).join(', ')}{Object.keys(r.changed_fields).length > 3 ? '…' : ''} ({t('audit.fields', { count: Object.keys(r.changed_fields).length })})</span>
                        ) : '—'}
                      </Td>
                    </tr>
                  ))}
                </tbody>
              </Table>
              <LoadMore visible={!!list.hasNextPage} loading={list.isFetchingNextPage} onClick={() => void list.fetchNextPage()} />
            </>
          )}
      </Card>
    </>
  )
}

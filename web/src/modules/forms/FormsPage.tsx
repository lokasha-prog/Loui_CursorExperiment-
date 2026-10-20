import * as TabsPrimitive from '@radix-ui/react-tabs'
import { ClipboardList, Plus } from 'lucide-react'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { useSearchParams } from 'react-router-dom'
import { flatten, useForms, useStudentOptions } from '@/api/hooks'
import { PageHeader } from '@/components/layout/PageHeader'
import { ListError, ListSkeleton, LoadMore } from '@/components/shared/ListState'
import { Button } from '@/components/ui/button'
import { Badge, Card, EmptyState, type Tone } from '@/components/ui/surfaces'
import { localized } from '@/lib/utils'
import { can, isParentOnly, useAuth } from '@/stores/auth'
import { AdmissionsTab, MyChildAdmission } from './AdmissionsTab'
import { FillDialog } from './FillDialog'
import { FormBuilder } from './FormBuilder'
import { SubmissionsDialog } from './SubmissionsDialog'

const statusTone: Record<string, Tone> = { draft: 'warning', live: 'success', archived: 'neutral' }

function FormsList({ manage }: { manage: boolean }) {
  const { t, i18n } = useTranslation()
  const [params, setParams] = useSearchParams()
  const [status, setStatus] = useState('')
  const list = useForms(manage ? status : 'live')
  const [builder, setBuilder] = useState<{ id: string | null } | null>(null)
  const [results, setResults] = useState<string | null>(null)
  const fill = params.get('fill')
  const forms = flatten(list.data)

  return (
    <>
      <div className="mb-4 flex flex-wrap items-center gap-2">
        {manage && (
          <div className="flex flex-wrap gap-2" role="group" aria-label={t('forms.tabs.forms')}>
            {['', 'draft', 'live', 'archived'].map((s) => (
              <Button key={s || 'all'} size="sm" variant={status === s ? 'default' : 'secondary'} aria-pressed={status === s} onClick={() => setStatus(s)}>{s ? t(`forms.status.${s}`) : t('forms.status.all')}</Button>
            ))}
          </div>
        )}
        {manage && <Button className="ms-auto" onClick={() => setBuilder({ id: null })}><Plus className="h-4 w-4" aria-hidden="true" />{t('forms.builder.new')}</Button>}
      </div>
      <Card className="overflow-hidden">
        {list.isLoading ? <ListSkeleton /> : list.isError ? <ListError error={list.error} onRetry={() => void list.refetch()} /> :
          forms.length === 0 ? <EmptyState icon={<ClipboardList className="h-7 w-7" aria-hidden="true" />} title={t('forms.empty')} description={t(manage ? 'forms.emptyBody' : 'forms.emptyParent')} /> : (
            <>
              <ul className="divide-y">
                {forms.map((f) => (
                  <li key={f.id} className="flex flex-wrap items-center gap-3 px-4 py-3 text-sm">
                    <span className="font-bold">{localized(f.name, i18n.language)}</span>
                    <Badge tone="info">{t(`forms.types.${f.type}`)}</Badge>
                    {manage && <Badge tone={statusTone[f.status]}>{t(`forms.status.${f.status}`)}</Badge>}
                    {manage && f.is_public && <Badge>{t('forms.public')}</Badge>}
                    {manage && <span className="text-xs text-muted-foreground">{t('forms.submissionCount', { count: f.submissions })}</span>}
                    <span className="ms-auto flex flex-wrap gap-1">
                      {f.published_version > 0 && f.status === 'live' && <Button size="sm" onClick={() => setParams({ fill: f.id })}>{t('forms.fillIn')}</Button>}
                      {manage && <Button size="sm" variant="secondary" onClick={() => setResults(f.id)}>{t('forms.results')}</Button>}
                      {manage && <Button size="sm" variant="secondary" onClick={() => setBuilder({ id: f.id })}>{t('forms.edit')}</Button>}
                    </span>
                  </li>
                ))}
              </ul>
              <LoadMore visible={!!list.hasNextPage} loading={list.isFetchingNextPage} onClick={() => void list.fetchNextPage()} />
            </>
          )}
      </Card>
      {builder && <FormBuilder key={builder.id ?? 'new'} id={builder.id} onClose={() => setBuilder(null)} />}
      {results && <SubmissionsDialog formId={results} onClose={() => setResults(null)} />}
      {fill && <FillDialog key={fill} formId={fill} studentId={params.get('student')} onClose={() => setParams({})} />}
    </>
  )
}

function ParentProgress() {
  const students = useStudentOptions(true, '')
  return <div className="mt-6 space-y-3">{(students.data ?? []).map((s) => <MyChildAdmission key={s.id} studentId={s.id} name={`${s.first_name} ${s.last_name}`} />)}</div>
}

export default function FormsPage() {
  const { t } = useTranslation()
  const me = useAuth((s) => s.me)
  const manage = can(me, 'forms.write')
  const parent = isParentOnly(me)
  const [tab, setTab] = useState('forms')
  const tabs = ['forms', ...(manage ? ['admissions'] : [])]

  return (
    <>
      <PageHeader title={t('forms.title')} subtitle={t(manage ? 'forms.subtitle' : 'forms.parentSubtitle')} />
      <TabsPrimitive.Root value={tab} onValueChange={setTab}>
        {tabs.length > 1 && (
          <TabsPrimitive.List className="mb-5 flex gap-1 overflow-x-auto border-b">
            {tabs.map((key) => (
              <TabsPrimitive.Trigger
                key={key}
                value={key}
                className="whitespace-nowrap border-b-2 border-transparent px-4 py-2.5 text-sm font-semibold text-muted-foreground transition-colors hover:text-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring data-[state=active]:border-primary data-[state=active]:font-bold data-[state=active]:text-primary-dark"
              >
                {t(`forms.tabs.${key}`)}
              </TabsPrimitive.Trigger>
            ))}
          </TabsPrimitive.List>
        )}
        <TabsPrimitive.Content value="forms"><FormsList manage={manage} />{parent && <ParentProgress />}</TabsPrimitive.Content>
        {manage && <TabsPrimitive.Content value="admissions"><AdmissionsTab /></TabsPrimitive.Content>}
      </TabsPrimitive.Root>
    </>
  )
}

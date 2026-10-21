import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { useStudentOptions } from '@/api/hooks'
import { PageHeader } from '@/components/layout/PageHeader'
import { Select } from '@/components/ui/form-controls'
import { EmptyState } from '@/components/ui/surfaces'
import { JourneyView } from './JourneyView'

/** What a family sees: the moments and approved reports their child's teachers shared. */
export default function JourneyPage() {
  const { t } = useTranslation()
  const students = useStudentOptions()
  const list = students.data ?? []
  const [chosen, setChosen] = useState('')
  const current = chosen || list[0]?.id || ''
  return (
    <>
      <PageHeader title={t('journeyPage.title')} subtitle={t('journeyPage.subtitle')} />
      {students.isSuccess && list.length === 0 ? <EmptyState title={t('journeyPage.none')} /> : (
        <>
          {list.length > 1 && <div className="mb-4"><Select value={current} onChange={(e) => setChosen(e.target.value)} aria-label={t('learning.obs.child')} className="w-64">{list.map((s) => <option key={s.id} value={s.id}>{s.first_name} {s.last_name}</option>)}</Select></div>}
          <JourneyView studentId={current} />
        </>
      )}
    </>
  )
}

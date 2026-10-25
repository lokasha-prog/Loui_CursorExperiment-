import { Plus } from 'lucide-react'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { useStudentGallery } from '@/api/hooks'
import { Button } from '@/components/ui/button'
import { Card } from '@/components/ui/surfaces'
import { PhotoGrid, UploadDialog } from '@/modules/gallery/GalleryParts'
import { can, isParentOnly, useAuth } from '@/stores/auth'

/** Photos and videos shared about one child, plus whole-nursery ones. Staff who post activities can add to it; the family sees it read-only. */
export function StudentGallery({ studentId, childName }: { studentId: string; childName: string }) {
  const { t } = useTranslation()
  const me = useAuth((s) => s.me)
  const parent = isParentOnly(me)
  const canAdd = !parent && can(me, 'activities.write')
  const gallery = useStudentGallery(studentId)
  const [uploadOpen, setUploadOpen] = useState(false)

  return (
    <Card className="p-5">
      <div className="mb-1 flex items-center justify-between gap-2">
        <h2 className="font-heading text-lg font-semibold">{t('students.gallery.title')}</h2>
        {canAdd && (
          <Button size="sm" variant="secondary" onClick={() => setUploadOpen(true)}>
            <Plus className="h-3.5 w-3.5" aria-hidden="true" />
            {t('students.gallery.add')}
          </Button>
        )}
      </div>
      <p className="mb-4 text-xs text-muted-foreground">{t(parent ? 'students.gallery.parentSubtitle' : 'students.gallery.subtitle', { name: childName })}</p>
      <PhotoGrid list={gallery} emptyDescription={t(parent ? 'students.gallery.emptyParent' : 'students.gallery.emptyStaff')} />
      {canAdd && <UploadDialog studentId={studentId} open={uploadOpen} onOpenChange={setUploadOpen} />}
    </Card>
  )
}

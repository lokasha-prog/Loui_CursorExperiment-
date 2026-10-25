import { Plus } from 'lucide-react'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { useGallery } from '@/api/hooks'
import { PageHeader } from '@/components/layout/PageHeader'
import { Button } from '@/components/ui/button'
import { Card } from '@/components/ui/surfaces'
import { can, isParentOnly, useAuth } from '@/stores/auth'
import { PhotoGrid, UploadDialog } from './GalleryParts'

/** Every photo and video the signed-in person may see: a family their own children's and the whole nursery's; staff those of their rooms. */
export default function GalleryPage() {
  const { t } = useTranslation()
  const me = useAuth((s) => s.me)
  const parent = isParentOnly(me)
  // Choosing who sees an upload (a room, a group, other centres...) is for administrators.
  const canAdd = !parent && can(me, 'activities.approve')
  const gallery = useGallery()
  const [uploadOpen, setUploadOpen] = useState(false)

  return (
    <>
      <PageHeader
        title={t(parent ? 'nav.myGallery' : 'nav.gallery')}
        subtitle={t(parent ? 'gallery.parentSubtitle' : 'gallery.subtitle')}
        action={canAdd && (
          <Button onClick={() => setUploadOpen(true)}>
            <Plus className="h-4 w-4" aria-hidden="true" />
            {t('gallery.add')}
          </Button>
        )}
      />
      <Card className="p-5">
        <PhotoGrid list={gallery} emptyDescription={t(parent ? 'gallery.emptyParent' : 'gallery.emptyStaff')} columns="grid-cols-2 sm:grid-cols-3 lg:grid-cols-4" />
      </Card>
      {canAdd && <UploadDialog open={uploadOpen} onOpenChange={setUploadOpen} />}
    </>
  )
}
